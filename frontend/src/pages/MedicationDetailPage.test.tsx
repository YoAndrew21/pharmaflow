import { FavoritesProvider } from '../features/favorites/FavoritesContext'
import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useNavigate } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import type { CatalogReader } from '../data/catalogReader'
import { localCatalogReader } from '../data/localCatalogReader'
import type { Medication } from '../domain/models'
import { CatalogPage } from './CatalogPage'
import { MedicationDetailPage } from './MedicationDetailPage'

function NavigateOther() {
  const navigate = useNavigate()
  return (
    <button onClick={() => navigate('/medications/cetirizine-10-tablets')}>
      Other presentation
    </button>
  )
}
function setup(
  slug = 'paracetamol-500-tablets',
  reader: CatalogReader = localCatalogReader,
  url?: string,
) {
  return render(
    <FavoritesProvider storage={null}>
      <MemoryRouter initialEntries={[url ?? `/medications/${slug}`]}>
        <Routes>
          <Route
            path="/medications/:slug"
            element={<MedicationDetailPage reader={reader} />}
          />
          <Route path="/catalog" element={<CatalogPage />} />
        </Routes>
        <NavigateOther />
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

describe('medication detail workflow', () => {
  it('renders one exact presentation and three labeled branch observations', async () => {
    setup()
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Paracetamol' }),
    ).toBeInTheDocument()
    expect(screen.getByText('500 mg')).toBeInTheDocument()
    expect(screen.getByText('General catalog')).toBeInTheDocument()
    expect(
      screen.getByText('Catalog flag: prescription not required'),
    ).toBeInTheDocument()
    const branches = within(
      screen.getByRole('list', { name: 'Availability by branch' }),
    ).getAllByRole('article')
    expect(branches).toHaveLength(3)
    expect(within(branches[0]!).getByText('Available')).toBeInTheDocument()
    expect(within(branches[1]!).getByText('Low stock')).toBeInTheDocument()
    expect(within(branches[2]!).getByText('Unavailable')).toBeInTheDocument()
    expect(within(branches[2]!).getByText(/USD\s2.00/)).toBeInTheDocument()
    branches.forEach((branch) =>
      expect(branch.querySelector('time')).toHaveAttribute('datetime'),
    )
    expect(document.title).toBe(
      'Paracetamol · 500 mg — Medication details | PharmaFlow',
    )
  })
  it('distinguishes another strength and demo prescription metadata', async () => {
    setup('omeprazole-20-capsules')
    await screen.findByRole('heading', { level: 1, name: 'Omeprazole' })
    expect(screen.getByText('20 mg')).toBeInTheDocument()
    expect(
      screen.getByText('Catalog flag: prescription required'),
    ).toBeInTheDocument()
    expect(screen.getByText(/not legal advice/)).toBeInTheDocument()
  })
  it('renders null prices without converting them to zero', async () => {
    setup('cetirizine-10-tablets')
    await screen.findByRole('heading', { level: 1, name: 'Cetirizine' })
    const willow = screen.getByRole('article', { name: 'Willow Arcade' })
    expect(
      within(willow).getByText('Reference price not provided'),
    ).toBeInTheDocument()
    expect(willow.querySelector('time')).not.toBeNull()
    expect(within(willow).queryByText(/USD\s0.00/)).not.toBeInTheDocument()
  })
  it('keeps missing observations separate from unavailable and timestamps', async () => {
    setup('sodium-chloride-09-solution')
    await screen.findByRole('heading', { level: 1, name: 'Sodium chloride' })
    const aster = screen.getByRole('article', { name: 'Aster Commons' })
    expect(within(aster).getByText('No demo information')).toBeInTheDocument()
    expect(
      within(aster).getByText('No demo update provided'),
    ).toBeInTheDocument()
    expect(aster.querySelector('time')).toBeNull()
    expect(within(aster).queryByText('Unavailable')).not.toBeInTheDocument()
  })
  it('renders all three branches even when the presentation has no observations', async () => {
    setup('paracetamol-caffeine-tablets')
    await screen.findByRole('heading', { level: 1 })
    expect(await screen.findAllByText('No demo information')).toHaveLength(3)
  })
  it('handles an unknown slug without requesting branch observations', async () => {
    const reader = {
      ...localCatalogReader,
      listBranches: vi.fn(),
      getAvailability: vi.fn(),
    }
    setup('unknown', reader)
    await screen.findByRole('heading', { name: 'Medication not found' })
    expect(reader.listBranches).not.toHaveBeenCalled()
    expect(reader.getAvailability).not.toHaveBeenCalled()
    expect(
      screen.getByRole('link', { name: 'Back to catalog' }),
    ).toHaveAttribute('href', '/catalog')
  })
  it('keeps loading visible until observations finish', async () => {
    const observations =
      pending<Awaited<ReturnType<CatalogReader['getAvailability']>>>()
    setup('paracetamol-500-tablets', {
      ...localCatalogReader,
      getAvailability: () => observations.promise,
    })
    expect(screen.getByRole('status')).toHaveTextContent(
      'Loading medication details',
    )
    await act(async () => {
      observations.resolve(
        await localCatalogReader.getAvailability('med-par-500'),
      )
    })
    await screen.findByRole('heading', { level: 1, name: 'Paracetamol' })
  })
  it.each(['getMedication', 'listBranches', 'getAvailability'] as const)(
    'distinguishes %s rejection from not found and retries',
    async (method) => {
      const failing = vi.fn().mockRejectedValueOnce(new Error('read failed'))
      failing.mockImplementation(localCatalogReader[method])
      setup('paracetamol-500-tablets', {
        ...localCatalogReader,
        [method]: failing,
      })
      await screen.findByRole('heading', {
        name: 'Medication details could not be loaded',
      })
      expect(screen.queryByText('Medication not found')).not.toBeInTheDocument()
      await userEvent.click(screen.getByRole('button', { name: 'Try again' }))
      await screen.findByRole('heading', { level: 1, name: 'Paracetamol' })
    },
  )
  it('ignores a stale medication read after changing slug', async () => {
    const stale = pending<Medication | null>()
    setup('paracetamol-500-tablets', {
      ...localCatalogReader,
      getMedication: (slug) =>
        slug === 'paracetamol-500-tablets'
          ? stale.promise
          : localCatalogReader.getMedication(slug),
    })
    await userEvent.click(
      screen.getByRole('button', { name: 'Other presentation' }),
    )
    await screen.findByRole('heading', { level: 1, name: 'Cetirizine' })
    await act(async () => {
      stale.resolve(
        await localCatalogReader.getMedication('paracetamol-500-tablets'),
      )
    })
    expect(
      screen.getByRole('heading', { level: 1, name: 'Cetirizine' }),
    ).toBeInTheDocument()
  })
  it('preserves filtered catalog context through the title link and return link', async () => {
    setup(
      '',
      localCatalogReader,
      '/catalog?q=para&branch=aster-commons&availability=low-stock&form=tablet&sort=price',
    )
    await screen.findByText('1 presentation found')
    await userEvent.click(
      screen.getByRole('link', { name: 'Paracetamol · 1000 mg' }),
    )
    await screen.findByRole('heading', { level: 1, name: 'Paracetamol' })
    expect(screen.getByText('1000 mg')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('link', { name: 'Back to catalog' }))
    await screen.findByText('1 presentation found')
    expect(screen.getByRole('searchbox')).toHaveValue('para')
    expect(screen.getByLabelText('Pharmacy branch')).toHaveValue(
      'aster-commons',
    )
  })
})
