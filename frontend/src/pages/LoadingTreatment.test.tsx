import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { expect, it, vi } from 'vitest'
import type { CatalogReader } from '../data/catalogReader'
import { localCatalogReader } from '../data/localCatalogReader'
import { FavoritesProvider } from '../features/favorites/FavoritesContext'
import { FAVORITES_KEY } from '../features/favorites/storage'
import { memoryStorage } from '../test/favoriteStorage'
import { HomePage } from './HomePage'
import { CatalogPage } from './CatalogPage'
import { MedicationDetailPage } from './MedicationDetailPage'
import { PharmacyDirectoryPage } from './PharmacyDirectoryPage'
import { PharmacyDetailPage } from './PharmacyDetailPage'
import { FavoritesPage } from './FavoritesPage'
import i18n from '../i18n'
import { chooseOption, selectTrigger } from '../test/select'

const screens = [
  { name: 'Home', path: '/', loading: 'catalog.loading' },
  { name: 'Catalog', path: '/catalog', loading: 'catalog.loading' },
  {
    name: 'Medication',
    path: '/medications/paracetamol-500-tablets',
    loading: 'detail.loading',
  },
  {
    name: 'Directory',
    path: '/pharmacies',
    loading: 'pharmacies.loadingDirectory',
  },
  {
    name: 'Branch',
    path: '/pharmacies/aster-commons',
    loading: 'pharmacies.loadingDetail',
  },
  { name: 'Favorites', path: '/favorites', loading: 'favorites.loading' },
] as const
type ScreenCase = (typeof screens)[number]

function gate() {
  let resolve!: () => void
  let reject!: (reason: Error) => void
  const promise = new Promise<void>((yes, no) => {
    resolve = yes
    reject = no
  })
  return { promise, resolve, reject }
}

function delayedReader(
  wait: () => Promise<void>,
  empty = false,
): CatalogReader {
  return {
    async discover(query, signal) {
      await wait()
      const data = await localCatalogReader.discover(query, signal)
      return empty ? { ...data, items: [], branches: [], forms: [] } : data
    },
    async listMedications(signal) {
      await wait()
      return empty ? [] : localCatalogReader.listMedications(signal)
    },
    async getMedication(slug, signal) {
      await wait()
      return empty ? null : localCatalogReader.getMedication(slug, signal)
    },
    async listBranches(signal) {
      await wait()
      return empty ? [] : localCatalogReader.listBranches(signal)
    },
    async getBranch(slug, signal) {
      await wait()
      return empty ? null : localCatalogReader.getBranch(slug, signal)
    },
    async getAvailability(id, signal) {
      await wait()
      return localCatalogReader.getAvailability(id, signal)
    },
  }
}

function setup(item: ScreenCase, reader: CatalogReader, ids = ['med-par-500']) {
  const storage = memoryStorage(
    JSON.stringify({ version: 1, medicationIds: ids }),
  )
  const view = render(
    <FavoritesProvider storage={storage}>
      <MemoryRouter initialEntries={[item.path]}>
        <Routes>
          <Route path="/" element={<HomePage reader={reader} />} />
          <Route path="/catalog" element={<CatalogPage reader={reader} />} />
          <Route
            path="/medications/:slug"
            element={<MedicationDetailPage reader={reader} />}
          />
          <Route
            path="/pharmacies"
            element={<PharmacyDirectoryPage reader={reader} />}
          />
          <Route
            path="/pharmacies/:slug"
            element={<PharmacyDetailPage reader={reader} />}
          />
          <Route
            path="/favorites"
            element={<FavoritesPage reader={reader} />}
          />
        </Routes>
      </MemoryRouter>
    </FavoritesProvider>,
  )
  return { ...view, storage }
}

function pendingUI(container: HTMLElement, item: ScreenCase) {
  const region = container.querySelector('[data-read-region]')!
  expect(region).toHaveAttribute('aria-busy', 'true')
  const status = screen.getByRole('status')
  expect(status).toHaveTextContent(i18n.t(item.loading))
  expect(region).not.toContainElement(status)
  const skeletons = [...container.querySelectorAll('[data-skeleton]')]
  if (item.name === 'Favorites') expect(skeletons).toHaveLength(0)
  else expect(skeletons.length).toBeGreaterThan(0)
  for (const skeleton of skeletons) {
    expect(skeleton).toHaveAttribute('aria-hidden', 'true')
    expect(skeleton.textContent).toBe('')
    expect(
      skeleton.querySelector(
        'a,button,input,select,textarea,[tabindex],[role],time',
      ),
    ).toBeNull()
  }
  expect(screen.queryByRole('article')).not.toBeInTheDocument()
  return skeletons
}

async function loaded(item: ScreenCase) {
  if (item.name === 'Home')
    await screen.findByRole('list', { name: i18n.t('visual.discoveryTitle') })
  if (item.name === 'Catalog') await screen.findByText('9 presentations found')
  if (item.name === 'Medication')
    await screen.findByRole('heading', { name: 'Paracetamol', level: 1 })
  if (item.name === 'Directory')
    await screen.findByRole('list', { name: i18n.t('pharmacies.listLabel') })
  if (item.name === 'Branch')
    await screen.findByRole('heading', { name: 'Aster Commons', level: 1 })
  if (item.name === 'Favorites')
    await screen.findByRole('article', { name: 'Paracetamol · 500 mg' })
}

it.each(screens)(
  '$name replaces only pending decoration with real successful content',
  async (item) => {
    const read = gate()
    const { container } = setup(
      item,
      delayedReader(() => read.promise),
    )
    const skeletons = pendingUI(container, item)
    await act(async () => read.resolve())
    await loaded(item)
    expect(container.querySelector('[data-read-region]')).toHaveAttribute(
      'aria-busy',
      'false',
    )
    expect(container.querySelector('[data-skeleton]')).toBeNull()
    for (const node of skeletons) expect(node).not.toBeInTheDocument()
  },
)

it.each(screens)(
  '$name removes pending decoration on error without presenting empty/404 or stock data',
  async (item) => {
    const read = gate()
    const { container, storage } = setup(
      item,
      delayedReader(() => read.promise),
    )
    const originalIds = storage.getItem(FAVORITES_KEY)
    const skeletons = pendingUI(container, item)
    expect(storage.setItem).not.toHaveBeenCalled()
    await act(async () => read.reject(new Error('controlled read failure')))
    await screen.findByRole('button', { name: 'Try again' })
    expect(container.querySelector('[data-read-region]')).toHaveAttribute(
      'aria-busy',
      'false',
    )
    expect(container.querySelector('[data-skeleton]')).toBeNull()
    for (const node of skeletons) expect(node).not.toBeInTheDocument()
    expect(screen.queryByRole('article')).not.toBeInTheDocument()
    expect(storage.getItem(FAVORITES_KEY)).toBe(originalIds)
    expect(storage.setItem).not.toHaveBeenCalled()
  },
)

it.each(
  screens.filter((item) =>
    ['Home', 'Catalog', 'Directory', 'Favorites'].includes(item.name),
  ),
)('$name transitions from pending to its genuine empty state', async (item) => {
  const read = gate()
  const { container } = setup(
    item,
    delayedReader(() => read.promise, true),
  )
  const skeletons = pendingUI(container, item)
  await act(async () => read.resolve())
  await screen.findByText(
    i18n.t(
      item.name === 'Directory'
        ? 'pharmacies.empty'
        : item.name === 'Favorites'
          ? 'favorites.emptyTitle'
          : 'catalog.emptyTitle',
    ),
  )
  expect(container.querySelector('[aria-busy="true"]')).toBeNull()
  expect(container.querySelector('[data-skeleton]')).toBeNull()
  for (const node of skeletons) expect(node).not.toBeInTheDocument()
  expect(screen.queryByRole('article')).not.toBeInTheDocument()
})

it.each(screens.filter((item) => ['Medication', 'Branch'].includes(item.name)))(
  '$name transitions from pending to genuine not-found',
  async (item) => {
    const read = gate()
    const { container } = setup(
      item,
      delayedReader(() => read.promise, true),
    )
    const skeletons = pendingUI(container, item)
    await act(async () => read.resolve())
    await screen.findByRole('heading', {
      name: i18n.t(
        item.name === 'Medication'
          ? 'detail.notFoundTitle'
          : 'pharmacies.notFoundTitle',
      ),
      level: 1,
    })
    expect(container.querySelector('[data-read-region]')).toHaveAttribute(
      'aria-busy',
      'false',
    )
    for (const node of skeletons) expect(node).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Try again' }),
    ).not.toBeInTheDocument()
  },
)

it.each(screens)(
  '$name preserves the actual Retry node and focus when retry starts, without another request on repeated activation',
  async (item) => {
    let fail = true
    const read = gate()
    const wait = vi.fn(async () => {
      if (fail) throw new Error('first failure')
      await read.promise
    })
    const { container } = setup(item, delayedReader(wait))
    const button = await screen.findByRole('button', { name: 'Try again' })
    fail = false
    await userEvent.click(button)
    pendingUI(container, item)
    expect(screen.getByRole('button', { name: 'Try again' })).toBe(button)
    expect(button).toHaveFocus()
    expect(button).toHaveAttribute('aria-disabled', 'true')
    expect(button.closest('[aria-busy]')).toBeNull()
    const calls = wait.mock.calls.length
    await userEvent.keyboard('{Enter}')
    expect(wait).toHaveBeenCalledTimes(calls)
    await act(async () => read.resolve())
    await loaded(item)
    expect(button).not.toBeInTheDocument()
  },
)

it('keeps the immediate local Favorites empty state without a loader, busy region, or skeleton', () => {
  const read = gate()
  const { container } = setup(
    screens[5],
    delayedReader(() => read.promise),
    [],
  )
  expect(screen.getByText(i18n.t('favorites.emptyTitle'))).toBeInTheDocument()
  expect(screen.queryByRole('status')).not.toBeInTheDocument()
  expect(
    container.querySelector('[aria-busy="true"], [data-skeleton]'),
  ).toBeNull()
})

it('retains real Catalog controls, focus, and an unsubmitted draft through refetch without showing stale cards or Retry', async () => {
  const user = userEvent.setup()
  const read = gate()
  const discover = vi
    .fn(localCatalogReader.discover)
    .mockImplementationOnce(localCatalogReader.discover)
    .mockImplementationOnce(async (query, signal) => {
      await read.promise
      return localCatalogReader.discover(query, signal)
    })
  const { container } = setup(screens[1], { ...localCatalogReader, discover })
  await loaded(screens[1])
  const form = screen.getByRole('search')
  const input = screen.getByRole('searchbox')
  const branch = selectTrigger('Pharmacy branch')
  await chooseOption(branch, 'Aster Commons')
  pendingUI(container, screens[1])
  await waitFor(() => expect(branch).toHaveFocus())
  expect(screen.getByRole('search')).toBe(form)
  expect(screen.getByRole('searchbox')).toBe(input)
  expect(selectTrigger('Pharmacy branch')).toBe(branch)
  expect(form.closest('[aria-busy="true"]')).toBeNull()
  expect(
    container.querySelector('[data-skeleton="catalog-controls"]'),
  ).toBeNull()
  expect(
    screen.queryByRole('button', { name: 'Try again' }),
  ).not.toBeInTheDocument()
  await user.type(input, 'paracetamol')
  expect(discover).toHaveBeenCalledTimes(2)
  expect(input).toHaveFocus()
  await act(async () => read.resolve())
  await loaded(screens[1])
  expect(input).toHaveValue('paracetamol')
  expect(input).toHaveFocus()
  expect(selectTrigger('Pharmacy branch')).toHaveTextContent('Aster Commons')
  await user.keyboard('{Enter}')
  await screen.findByText('3 presentations found')
  expect(discover).toHaveBeenLastCalledWith(
    expect.objectContaining({ q: 'paracetamol', branch: 'aster-commons' }),
    expect.any(AbortSignal),
  )
})
