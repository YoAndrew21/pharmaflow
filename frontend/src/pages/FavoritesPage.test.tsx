import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest'
import type { CatalogReader } from '../data/catalogReader'
import { localCatalogReader } from '../data/localCatalogReader'
import type { Medication } from '../domain/models'
import { FavoritesProvider } from '../features/favorites/FavoritesContext'
import {
  FAVORITES_KEY,
  type FavoriteStorage,
} from '../features/favorites/storage'
import { memoryStorage } from '../test/favoriteStorage'
import { AppLayout } from '../layouts/AppLayout'
import { FavoritesPage } from './FavoritesPage'
import { CatalogPage } from './CatalogPage'
import { MedicationDetailPage } from './MedicationDetailPage'

const envelope = (ids: readonly string[]) =>
  JSON.stringify({ version: 1, medicationIds: ids })
beforeAll(() =>
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => ({ matches: false })),
  ),
)
afterAll(() => vi.unstubAllGlobals())
function setup(
  url = '/favorites',
  storage: FavoriteStorage | null = memoryStorage(),
  reader: CatalogReader = localCatalogReader,
) {
  return render(
    <FavoritesProvider storage={storage}>
      <MemoryRouter initialEntries={[url]}>
        <Routes>
          <Route element={<AppLayout />}>
            <Route
              path="/favorites"
              element={<FavoritesPage reader={reader} />}
            />
            <Route
              path="/catalog"
              element={<CatalogPage reader={localCatalogReader} />}
            />
            <Route
              path="/medications/:slug"
              element={<MedicationDetailPage reader={localCatalogReader} />}
            />
          </Route>
        </Routes>
      </MemoryRouter>
    </FavoritesProvider>,
  )
}
function pending<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((yes) => {
    resolve = yes
  })
  return { promise, resolve }
}

describe('local favorites workflow', () => {
  it('shows an intentional empty state, privacy explanation, and catalog recovery', async () => {
    setup()
    expect(
      screen.getByRole('heading', { level: 1, name: 'Favorites' }),
    ).toBeInTheDocument()
    expect(screen.getByText('No bookmarks yet')).toBeInTheDocument()
    expect(
      screen.getByText(/Other people using this browser profile/),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'Browse catalog' }),
    ).toHaveAttribute('href', '/catalog')
    expect(
      screen.queryByRole('button', { name: 'Clear favorites' }),
    ).not.toBeInTheDocument()
    await screen.findByRole('link', {
      name: 'Favorites, 0 saved presentations',
    })
  })
  it('saves/removes from Catalog and shares the exact presentation state with Detail and Favorites', async () => {
    const storage = memoryStorage()
    setup('/catalog', storage)
    await screen.findByText('9 presentations found')
    const card = screen.getByRole('article', { name: 'Paracetamol · 500 mg' })
    const save = within(card).getByRole('button', {
      name: /Save Paracetamol · 500 mg · Demo pack · 20 tablets to favorites/,
    })
    expect(save).toHaveAttribute('aria-pressed', 'false')
    expect(save).toHaveTextContent('Save')
    expect(save.getAttribute('aria-label')).toContain(save.textContent)
    await userEvent.click(save)
    expect(save).toHaveAttribute('aria-pressed', 'true')
    expect(save).toHaveTextContent('Remove')
    expect(save.getAttribute('aria-label')).toContain(save.textContent)
    expect(
      screen.getByRole('link', { name: 'Favorites, 1 saved presentation' }),
    ).toBeInTheDocument()
    const other = screen.getByRole('article', { name: 'Paracetamol · 1000 mg' })
    expect(within(other).getByRole('button')).toHaveAttribute(
      'aria-pressed',
      'false',
    )
    await userEvent.click(
      within(card).getByRole('link', { name: 'Paracetamol · 500 mg' }),
    )
    await screen.findByRole('heading', { level: 1, name: 'Paracetamol' })
    const toggle = screen.getByRole('button', {
      name: /Remove Paracetamol · 500 mg/,
    })
    expect(toggle).toHaveAttribute('aria-pressed', 'true')
    await userEvent.click(toggle)
    expect(storage.getItem(FAVORITES_KEY)).toBeNull()
    await userEvent.click(
      screen.getByRole('button', { name: /Save Paracetamol · 500 mg/ }),
    )
    await userEvent.click(screen.getByRole('link', { name: 'Back to catalog' }))
    await screen.findByText('9 presentations found')
    expect(
      within(
        screen.getByRole('article', { name: 'Paracetamol · 500 mg' }),
      ).getByRole('button'),
    ).toHaveAttribute('aria-pressed', 'true')
    await userEvent.click(
      screen.getByRole('link', { name: 'Favorites, 1 saved presentation' }),
    )
    await screen.findByRole('list', { name: 'Saved medication presentations' })
    expect(screen.getAllByRole('article')).toHaveLength(1)
    expect(
      screen.getByRole('link', { name: 'Paracetamol · 500 mg' }),
    ).toHaveAttribute('href', '/medications/paracetamol-500-tablets')
  })
  it('supports removal from Catalog without interfering with filters', async () => {
    setup(
      '/catalog?q=para&branch=aster-commons',
      memoryStorage(envelope(['med-par-500'])),
    )
    await screen.findByText('3 presentations found')
    await userEvent.click(
      within(
        screen.getByRole('article', { name: 'Paracetamol · 500 mg' }),
      ).getByRole('button'),
    )
    expect(screen.getByLabelText('Pharmacy branch')).toHaveValue(
      'aster-commons',
    )
    expect(screen.getByRole('searchbox')).toHaveValue('para')
    expect(
      screen.getByRole('link', { name: 'Favorites, 0 saved presentations' }),
    ).toBeInTheDocument()
  })
  it('renders exact saved facts and keeps keyboard focus on the next removal control', async () => {
    const view = setup(
      '/favorites',
      memoryStorage(envelope(['med-par-500', 'med-cet-10'])),
    )
    await screen.findByRole('list', { name: 'Saved medication presentations' })
    const cet = screen.getByRole('article', { name: 'Cetirizine · 10 mg' })
    expect(
      within(cet).getByText('Cetirizine', { selector: 'dd' }),
    ).toBeInTheDocument()
    expect(within(cet).getByText('Demo pack · 14 tablets')).toBeInTheDocument()
    const remove = within(cet).getByRole('button')
    remove.focus()
    await userEvent.keyboard(' ')
    expect(
      screen.queryByRole('article', { name: 'Cetirizine · 10 mg' }),
    ).not.toBeInTheDocument()
    expect(
      within(
        screen.getByRole('article', { name: 'Paracetamol · 500 mg' }),
      ).getByRole('button'),
    ).toHaveFocus()
    expect(
      view.container.querySelector('[aria-live="polite"]'),
    ).toHaveTextContent('Bookmark removed: Cetirizine')
    await userEvent.keyboard('{Enter}')
    expect(screen.getByRole('link', { name: 'Browse catalog' })).toHaveFocus()
    expect(screen.getByText('No bookmarks yet')).toBeInTheDocument()
  })
  it('requires clear confirmation, supports cancellation, preserves other keys, and restores focus', async () => {
    const storage = memoryStorage(envelope(['med-par-500', 'med-cet-10']))
    storage.setItem('pharmaflow:theme', 'dark')
    setup('/favorites', storage)
    await screen.findByRole('list', { name: 'Saved medication presentations' })
    await userEvent.click(
      screen.getByRole('button', { name: 'Clear favorites' }),
    )
    expect(
      screen.getByRole('button', { name: 'Yes, clear favorites' }),
    ).toHaveFocus()
    expect(screen.getAllByRole('article')).toHaveLength(2)
    await userEvent.click(
      screen.getByRole('button', { name: 'Keep favorites' }),
    )
    expect(
      screen.getByRole('button', { name: 'Clear favorites' }),
    ).toHaveFocus()
    await userEvent.click(
      screen.getByRole('button', { name: 'Clear favorites' }),
    )
    await userEvent.keyboard('{Enter}')
    expect(screen.getByRole('link', { name: 'Browse catalog' })).toHaveFocus()
    expect(storage.getItem(FAVORITES_KEY)).toBeNull()
    expect(storage.getItem('pharmaflow:theme')).toBe('dark')
  })
  it('keeps stale IDs during loading and prunes only after the complete lookup succeeds', async () => {
    const storage = memoryStorage(envelope(['med-par-500', 'med-retired']))
    const lookup = pending<readonly Medication[]>()
    setup('/favorites', storage, {
      ...localCatalogReader,
      listMedications: () => lookup.promise,
    })
    expect(screen.getByRole('status')).toHaveTextContent(
      'Loading saved presentations',
    )
    expect(storage.getItem(FAVORITES_KEY)).toBe(
      envelope(['med-par-500', 'med-retired']),
    )
    expect(storage.setItem).not.toHaveBeenCalled()
    await act(async () => {
      lookup.resolve(await localCatalogReader.listMedications())
    })
    await screen.findByRole('article', { name: 'Paracetamol · 500 mg' })
    expect(storage.getItem(FAVORITES_KEY)).toBe(envelope(['med-par-500']))
    expect(
      screen.getByRole('link', { name: 'Favorites, 1 saved presentation' }),
    ).toBeInTheDocument()
  })
  it.each(['listMedications', 'discover'] as const)(
    'preserves IDs after %s failure and recovers with retry',
    async (method) => {
      const storage = memoryStorage(envelope(['med-par-500', 'med-retired']))
      const failing = vi
        .fn()
        .mockRejectedValueOnce(new Error('outage'))
        .mockImplementation(localCatalogReader[method])
      setup('/favorites', storage, { ...localCatalogReader, [method]: failing })
      await screen.findByRole('heading', {
        name: 'Saved presentations could not be loaded',
      })
      expect(storage.getItem(FAVORITES_KEY)).toBe(
        envelope(['med-par-500', 'med-retired']),
      )
      expect(storage.setItem).not.toHaveBeenCalled()
      await userEvent.click(screen.getByRole('button', { name: 'Try again' }))
      await screen.findByRole('article', { name: 'Paracetamol · 500 mg' })
      expect(storage.getItem(FAVORITES_KEY)).toBe(envelope(['med-par-500']))
    },
  )
  it('recovers an all-stale set to the empty state without broken cards', async () => {
    const storage = memoryStorage(envelope(['med-retired']))
    setup('/favorites', storage)
    await screen.findByText('No bookmarks yet')
    expect(screen.queryByRole('article')).not.toBeInTheDocument()
    expect(storage.getItem(FAVORITES_KEY)).toBeNull()
  })
  it.each([
    ['broken', /could not be read safely/],
    ['{"version":4,"medicationIds":[]}', /unsupported version/],
  ] as const)('explains storage recovery for %s', async (raw, message) => {
    const storage = memoryStorage(raw)
    setup('/favorites', storage)
    expect(screen.getByText(message)).toBeInTheDocument()
    expect(screen.getByText('No bookmarks yet')).toBeInTheDocument()
    await act(async () => {})
    expect(storage.setItem).not.toHaveBeenCalled()
    expect(storage.removeItem).not.toHaveBeenCalled()
  })
  it('explains memory-only behavior after a failed persistence write', async () => {
    const storage = memoryStorage()
    storage.setItem.mockImplementation(() => {
      throw new Error('quota')
    })
    setup('/catalog', storage)
    await screen.findByText('9 presentations found')
    await userEvent.click(
      within(
        screen.getByRole('article', { name: 'Paracetamol · 500 mg' }),
      ).getByRole('button'),
    )
    await userEvent.click(
      screen.getByRole('link', { name: 'Favorites, 1 saved presentation' }),
    )
    await screen.findByRole('article', { name: 'Paracetamol · 500 mg' })
    expect(
      screen.getByText(/changes may not survive a reload/),
    ).toBeInTheDocument()
    expect(storage.getItem(FAVORITES_KEY)).toBeNull()
  })
  it('updates cards/count from a valid cross-tab event and handles malformed incoming data', async () => {
    setup()
    act(() =>
      window.dispatchEvent(
        new StorageEvent('storage', {
          key: FAVORITES_KEY,
          newValue: envelope(['med-cet-10']),
        }),
      ),
    )
    await screen.findByRole('article', { name: 'Cetirizine · 10 mg' })
    expect(
      screen.getByRole('link', { name: 'Favorites, 1 saved presentation' }),
    ).toBeInTheDocument()
    act(() =>
      window.dispatchEvent(
        new StorageEvent('storage', { key: FAVORITES_KEY, newValue: 'broken' }),
      ),
    )
    expect(screen.getByText('No bookmarks yet')).toBeInTheDocument()
    expect(screen.getByText(/could not be read safely/)).toBeInTheDocument()
  })
})
