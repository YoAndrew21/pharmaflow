import { FavoritesProvider } from '../features/favorites/FavoritesContext'
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  MemoryRouter,
  Route,
  Routes,
  useNavigate,
  useLocation,
} from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import type { CatalogReader } from '../data/catalogReader'
import { localCatalogReader } from '../data/localCatalogReader'
import type { PharmacyBranch } from '../domain/models'
import { PharmacyDirectoryPage } from './PharmacyDirectoryPage'
import { PharmacyDetailPage } from './PharmacyDetailPage'
import { MedicationDetailPage } from './MedicationDetailPage'
import { CatalogPage } from './CatalogPage'
import { OpeningHours } from '../features/pharmacies/OpeningHours'

function Navigation() {
  const navigate = useNavigate()
  const location = useLocation()
  return (
    <>
      <span data-testid="url">
        {location.pathname}
        {location.search}
      </span>
      <button onClick={() => navigate('/pharmacies/willow-arcade')}>
        Other branch
      </button>
    </>
  )
}
function setup(
  url = '/pharmacies',
  reader: CatalogReader = localCatalogReader,
) {
  return render(
    <FavoritesProvider storage={null}>
      <MemoryRouter initialEntries={[url]}>
        <Routes>
          <Route
            path="/pharmacies"
            element={<PharmacyDirectoryPage reader={reader} />}
          />
          <Route
            path="/pharmacies/:slug"
            element={<PharmacyDetailPage reader={reader} />}
          />
          <Route
            path="/medications/:slug"
            element={<MedicationDetailPage reader={localCatalogReader} />}
          />
          <Route
            path="/catalog"
            element={<CatalogPage reader={localCatalogReader} />}
          />
        </Routes>
        <Navigation />
      </MemoryRouter>
    </FavoritesProvider>,
  )
}
function pending<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: Error) => void
  const promise = new Promise<T>((yes, no) => {
    resolve = yes
    reject = no
  })
  return { promise, resolve, reject }
}

describe('fictional pharmacy directory', () => {
  it('renders exactly three branches with correct links, location, schedules, and demo context', async () => {
    setup()
    const list = await screen.findByRole('list', {
      name: 'Fictional pharmacy branches',
    })
    expect(within(list).getAllByRole('article')).toHaveLength(3)
    for (const branch of await localCatalogReader.listBranches()) {
      const card = within(list).getByRole('article', { name: branch.name })
      expect(
        within(card).getByRole('link', { name: branch.name }),
      ).toHaveAttribute('href', `/pharmacies/${branch.slug}`)
      expect(
        within(card).getByText(branch.fictionalLocation),
      ).toBeInTheDocument()
      expect(within(card).getByText('Sunday')).toBeInTheDocument()
      expect(within(card).getByText('Closed')).toBeInTheDocument()
    }
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
    expect(screen.getByText(/not real pharmacy locations/)).toBeInTheDocument()
    expect(list.querySelector('iframe')).toBeNull()
    expect(
      within(list).queryByRole('link', { name: /map|directions|nearest/i }),
    ).not.toBeInTheDocument()
  })
  it('opens the named branch using keyboard navigation', async () => {
    setup()
    await screen.findByRole('link', { name: 'Aster Commons' })
    await userEvent.tab()
    expect(screen.getByRole('link', { name: 'Aster Commons' })).toHaveFocus()
    await userEvent.keyboard('{Enter}')
    await screen.findByRole('heading', { level: 1, name: 'Aster Commons' })
  })
  it('shows pending then directory success with a controlled promise', async () => {
    const read = pending<readonly PharmacyBranch[]>()
    setup('/pharmacies', {
      ...localCatalogReader,
      listBranches: () => read.promise,
    })
    expect(screen.getByRole('status')).toHaveTextContent(
      'Loading pharmacy branches',
    )
    await act(async () => {
      read.resolve(await localCatalogReader.listBranches())
    })
    await screen.findByRole('list', { name: 'Fictional pharmacy branches' })
  })
  it('distinguishes directory read failure from successful empty data and retries', async () => {
    const read = vi
      .fn()
      .mockRejectedValueOnce(new Error('read failed'))
      .mockImplementation(localCatalogReader.listBranches)
    setup('/pharmacies', { ...localCatalogReader, listBranches: read })
    await screen.findByRole('heading', {
      name: 'Pharmacy branches could not be loaded',
    })
    expect(
      screen.queryByText(
        'No fictional branches are listed in this data source.',
      ),
    ).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))
    await screen.findByRole('list', { name: 'Fictional pharmacy branches' })
  })
  it('renders successful empty data without an error', async () => {
    setup('/pharmacies', {
      ...localCatalogReader,
      listBranches: async () => [],
    })
    await screen.findByText(
      'No fictional branches are listed in this data source.',
    )
    expect(
      screen.queryByRole('button', { name: 'Try again' }),
    ).not.toBeInTheDocument()
  })
})

describe('pharmacy detail', () => {
  it.each(['aster-commons', 'willow-arcade', 'lantern-court'])(
    'loads %s directly with complete structured weekly hours',
    async (slug) => {
      const branch = (await localCatalogReader.getBranch(slug))!
      setup(`/pharmacies/${slug}`)
      await screen.findByRole('heading', { level: 1, name: branch.name })
      expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
      expect(screen.getByText(branch.fictionalLocation)).toBeInTheDocument()
      const hours = screen.getByRole('region', { name: 'Weekly opening hours' })
      expect(hours.querySelectorAll('dt')).toHaveLength(7)
      expect(hours.querySelectorAll('dd')).toHaveLength(7)
      expect(within(hours).getAllByText('9:00 AM – 5:00 PM')).toHaveLength(5)
      expect(within(hours).getByText('10:00 AM – 1:00 PM')).toBeInTheDocument()
      expect(within(hours).getByText('Closed')).toBeInTheDocument()
      expect(
        screen.getByText(/fictional demonstration information/),
      ).toBeInTheDocument()
      await waitFor(() =>
        expect(document.title).toBe(
          `${branch.name} — Pharmacy branch details | PharmaFlow`,
        ),
      )
    },
  )
  it('recovers an unknown slug to the independent directory', async () => {
    setup('/pharmacies/unknown')
    await screen.findByRole('heading', { name: 'Pharmacy branch not found' })
    expect(
      screen.queryByRole('button', { name: 'Try again' }),
    ).not.toBeInTheDocument()
    await userEvent.click(
      screen.getByRole('link', { name: 'Back to pharmacy branches' }),
    )
    await screen.findByRole('list', { name: 'Fictional pharmacy branches' })
  })
  it('shows loading before a controlled valid branch resolves', async () => {
    const read = pending<PharmacyBranch | null>()
    setup('/pharmacies/aster-commons', {
      ...localCatalogReader,
      getBranch: () => read.promise,
    })
    expect(screen.getByRole('status')).toHaveTextContent(
      'Loading pharmacy branch details',
    )
    await act(async () => {
      read.resolve(await localCatalogReader.getBranch('aster-commons'))
    })
    await screen.findByRole('heading', { level: 1, name: 'Aster Commons' })
  })
  it('keeps genuine read failure separate from not found and allows retry', async () => {
    const read = vi
      .fn()
      .mockRejectedValueOnce(new Error('failure'))
      .mockImplementation(localCatalogReader.getBranch)
    setup('/pharmacies/aster-commons', {
      ...localCatalogReader,
      getBranch: read,
    })
    await screen.findByRole('heading', {
      name: 'Pharmacy branch details could not be loaded',
    })
    expect(
      screen.queryByText('Pharmacy branch not found'),
    ).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))
    await screen.findByRole('heading', { level: 1, name: 'Aster Commons' })
  })
  it.each(['resolve', 'reject'] as const)(
    'ignores stale branch %s after a route change',
    async (outcome) => {
      const stale = pending<PharmacyBranch | null>()
      setup('/pharmacies/aster-commons', {
        ...localCatalogReader,
        getBranch: (slug) =>
          slug === 'aster-commons'
            ? stale.promise
            : localCatalogReader.getBranch(slug),
      })
      await userEvent.click(
        screen.getByRole('button', { name: 'Other branch' }),
      )
      await screen.findByRole('heading', { level: 1, name: 'Willow Arcade' })
      await act(async () => {
        if (outcome === 'resolve')
          stale.resolve(await localCatalogReader.getBranch('aster-commons'))
        else stale.reject(new Error('stale failure'))
      })
      expect(
        screen.getByRole('heading', { level: 1, name: 'Willow Arcade' }),
      ).toBeInTheDocument()
      expect(
        screen.queryByText('Pharmacy branch details could not be loaded'),
      ).not.toBeInTheDocument()
    },
  )
  it('renders split intervals in one understandable schedule', async () => {
    const branch = (await localCatalogReader.getBranch('aster-commons'))!
    render(
      <OpeningHours
        branch={{
          ...branch,
          openingHours: {
            ...branch.openingHours,
            monday: [
              { opensAt: '09:00', closesAt: '12:00' },
              { opensAt: '14:00', closesAt: '17:00' },
            ],
          },
        }}
      />,
    )
    const monday = screen.getByText('Monday').parentElement!
    expect(within(monday).getByText('9:00 AM – 12:00 PM')).toBeInTheDocument()
    expect(within(monday).getByText('2:00 PM – 5:00 PM')).toBeInTheDocument()
  })
})

describe('branch navigation integrations', () => {
  it('uses the canonical branch-filtered catalog and keeps normal filter controls', async () => {
    setup('/pharmacies/lantern-court')
    await screen.findByRole('heading', { level: 1, name: 'Lantern Court' })
    expect(
      screen.getByRole('link', { name: 'Browse medications at this branch' }),
    ).toHaveAttribute('href', '/catalog?branch=lantern-court')
    await userEvent.click(
      screen.getByRole('link', { name: 'Browse medications at this branch' }),
    )
    await screen.findByText('9 presentations found')
    expect(screen.getByLabelText('Pharmacy branch')).toHaveValue(
      'lantern-court',
    )
    const paracetamol = screen.getByRole('article', {
      name: 'Paracetamol · 500 mg',
    })
    expect(within(paracetamol).getByText('Unavailable')).toBeInTheDocument()
    expect(within(paracetamol).getByText(/USD\s2.00/)).toBeInTheDocument()
    expect(within(paracetamol).queryByText(/From USD/)).not.toBeInTheDocument()
    await userEvent.selectOptions(
      screen.getByLabelText('Pharmacy branch'),
      'aster-commons',
    )
    await screen.findByText(
      /Availability and reference prices refer only to Aster Commons/,
    )
    await userEvent.click(
      screen.getByRole('button', { name: 'Clear search & filters' }),
    )
    expect(screen.getByLabelText('Pharmacy branch')).toHaveValue('')
    expect(await screen.findByText(/From USD\s3.20/)).toBeInTheDocument()
  })
  it('links only branch identity from medication comparison, preserving its semantics', async () => {
    setup('/medications/paracetamol-500-tablets')
    await screen.findByRole('heading', { level: 1, name: 'Paracetamol' })
    const list = screen.getByRole('list', { name: 'Availability by branch' })
    for (const branch of await localCatalogReader.listBranches()) {
      const row = within(list).getByRole('article', { name: branch.name })
      expect(within(row).getAllByRole('link')).toHaveLength(1)
      expect(
        within(row).getByRole('link', { name: branch.name }),
      ).toHaveAttribute('href', `/pharmacies/${branch.slug}`)
      expect(within(row).queryByRole('button')).not.toBeInTheDocument()
    }
    await userEvent.click(
      within(list).getByRole('link', { name: 'Willow Arcade' }),
    )
    await screen.findByRole('heading', { level: 1, name: 'Willow Arcade' })
  })
})
