import { FavoritesProvider } from '../features/favorites/FavoritesContext'
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  MemoryRouter,
  Route,
  Routes,
  useLocation,
  useNavigate,
} from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import type { CatalogReader } from '../data/catalogReader'
import { localCatalogReader } from '../data/localCatalogReader'
import { defaultCatalogQuery, type CatalogResult } from '../domain/catalog'
import { CatalogPage } from './CatalogPage'
import { HomePage } from './HomePage'

function HistoryControls() {
  const location = useLocation()
  const navigate = useNavigate()
  return (
    <>
      <span data-testid="url">
        {location.pathname}
        {location.search}
      </span>
      <button onClick={() => navigate(-1)}>Go back</button>
      <button onClick={() => navigate(1)}>Go forward</button>
    </>
  )
}
function renderCatalog(
  url = '/catalog',
  reader: CatalogReader = localCatalogReader,
) {
  return render(
    <FavoritesProvider storage={null}>
      <MemoryRouter initialEntries={[url]}>
        <CatalogPage reader={reader} />
        <HistoryControls />
      </MemoryRouter>
    </FavoritesProvider>,
  )
}
function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((yes) => {
    resolve = yes
  })
  return { promise, resolve }
}

describe('catalog workflow', () => {
  it('renders accessible presentation cards and mixed status text with aggregate prices', async () => {
    renderCatalog()
    expect(await screen.findByText('9 presentations found')).toHaveAttribute(
      'role',
      'status',
    )
    expect(
      screen.getByRole('list', { name: 'Medication presentations' }),
    ).toBeInTheDocument()
    expect(screen.getAllByRole('article')).toHaveLength(9)
    const card = screen.getByRole('article', { name: 'Paracetamol · 500 mg' })
    expect(within(card).getByText('Demo pack · 20 tablets')).toBeInTheDocument()
    expect(
      within(card).getByText('Available at 1 of 3 branches'),
    ).toBeInTheDocument()
    expect(
      within(card).getByText('Low stock at 1 of 3 branches'),
    ).toBeInTheDocument()
    expect(
      within(card).getByText('Unavailable at 1 of 3 branches'),
    ).toBeInTheDocument()
    expect(within(card).getByText(/From USD\s3.20/)).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: /buy|cart|reserve/i }),
    ).not.toBeInTheDocument()
  })
  it('loads combined parameters directly, including branch-specific pricing', async () => {
    renderCatalog(
      '/catalog?q=para&branch=aster-commons&availability=low-stock&form=tablet&sort=price',
    )
    expect(await screen.findByText('1 presentation found')).toBeInTheDocument()
    expect(
      screen.getByRole('searchbox', {
        name: 'Medication name or active ingredient',
      }),
    ).toHaveValue('para')
    expect(screen.getByLabelText('Pharmacy branch')).toHaveValue(
      'aster-commons',
    )
    const card = screen.getByRole('article', { name: 'Paracetamol · 1000 mg' })
    expect(within(card).getByText('Low stock')).toBeInTheDocument()
    expect(within(card).getByText(/USD\s7.00/)).toBeInTheDocument()
    expect(within(card).queryByText(/From/)).not.toBeInTheDocument()
    expect(within(card).getByText(/Sep 1, 2026.*UTC/)).toBeInTheDocument()
  })
  it('keeps missing information distinct from unavailable and a successful empty result', async () => {
    renderCatalog('/catalog?q=chlor&branch=aster-commons')
    await screen.findByText('1 presentation found')
    const card = screen.getByRole('article')
    expect(within(card).getByText('No demo information')).toBeInTheDocument()
    expect(within(card).queryByText('Unavailable')).not.toBeInTheDocument()
    expect(
      within(card).getByText('Reference price not provided'),
    ).toBeInTheDocument()
    expect(
      screen.queryByText('No matching presentations'),
    ).not.toBeInTheDocument()
  })
  it('submits ingredient search, combines filters, preserves focus, and clears the URL/draft', async () => {
    const user = userEvent.setup()
    renderCatalog()
    await screen.findByText('9 presentations found')
    const searchbox = screen.getByRole('searchbox')
    await user.type(searchbox, 'paracetamol{Enter}')
    await screen.findByText('3 presentations found')
    expect(searchbox).toHaveFocus()
    const branch = screen.getByLabelText('Pharmacy branch')
    await user.selectOptions(branch, 'aster-commons')
    await waitFor(() =>
      expect(screen.getByTestId('url')).toHaveTextContent(
        'branch=aster-commons',
      ),
    )
    await user.selectOptions(screen.getByLabelText('Availability'), 'low-stock')
    await screen.findByText('1 presentation found')
    expect(screen.getByLabelText('Availability')).toHaveFocus()
    await user.selectOptions(
      screen.getByLabelText('Pharmaceutical form'),
      'tablet',
    )
    await waitFor(() =>
      expect(screen.getByTestId('url')).toHaveTextContent('form=tablet'),
    )
    await user.selectOptions(screen.getByLabelText('Sort by'), 'price')
    await waitFor(() =>
      expect(screen.getByTestId('url')).toHaveTextContent('sort=price'),
    )
    await user.click(
      screen.getByRole('button', { name: 'Clear search & filters' }),
    )
    await screen.findByText('9 presentations found')
    expect(screen.getByTestId('url')).toHaveTextContent(/^\/catalog$/)
    expect(screen.getByRole('searchbox')).toHaveValue('')
    expect(screen.getByLabelText('Pharmacy branch')).toHaveValue('')
    expect(screen.getByLabelText('Sort by')).toHaveValue('name')
    await user.type(screen.getByRole('searchbox'), 'unsubmitted')
    await user.click(
      screen.getByRole('button', { name: 'Clear search & filters' }),
    )
    expect(screen.getByRole('searchbox')).toHaveValue('')
  })
  it('restores URL state and search text when going backward/forward', async () => {
    const user = userEvent.setup()
    renderCatalog()
    await screen.findByText('9 presentations found')
    await user.type(screen.getByRole('searchbox'), 'caffeine{Enter}')
    await screen.findByText('1 presentation found')
    await user.click(screen.getByRole('button', { name: 'Go back' }))
    await screen.findByText('9 presentations found')
    expect(screen.getByRole('searchbox')).toHaveValue('')
    await user.click(screen.getByRole('button', { name: 'Go forward' }))
    await screen.findByText('1 presentation found')
    expect(screen.getByRole('searchbox')).toHaveValue('caffeine')
  })
  it('shows empty results with reset recovery, not a read error', async () => {
    const user = userEvent.setup()
    renderCatalog('/catalog?q=nothing-matches')
    await screen.findByText('0 presentations found')
    expect(
      screen.getByRole('heading', { name: 'No matching presentations' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByText('Catalog could not be read.'),
    ).not.toBeInTheDocument()
    await user.click(
      screen.getAllByRole('button', { name: 'Clear search & filters' })[0]!,
    )
    await screen.findByText('9 presentations found')
  })
  it('falls back safely for unknown branch/form/status/sort parameters', async () => {
    renderCatalog(
      '/catalog?branch=unknown&form=unknown&availability=unknown&sort=unknown',
    )
    await screen.findByText('9 presentations found')
    expect(screen.getByLabelText('Pharmacy branch')).toHaveValue('')
    expect(screen.getByLabelText('Pharmaceutical form')).toHaveValue('')
    expect(screen.getByLabelText('Availability')).toHaveValue('')
    expect(screen.getByLabelText('Sort by')).toHaveValue('name')
  })
  it('announces a pending read and then renders successful results', async () => {
    const pending = deferred<CatalogResult>()
    renderCatalog('/catalog', {
      ...localCatalogReader,
      discover: () => pending.promise,
    })
    expect(screen.getByRole('status')).toHaveTextContent('Loading catalog…')
    expect(
      screen.queryByText('No matching presentations'),
    ).not.toBeInTheDocument()
    await act(async () =>
      pending.resolve(await localCatalogReader.discover(defaultCatalogQuery)),
    )
    await screen.findByText('9 presentations found')
  })
  it('announces a rejected read separately from stock and retries successfully', async () => {
    const user = userEvent.setup()
    const discover = vi
      .fn(localCatalogReader.discover)
      .mockRejectedValueOnce(new Error('test rejection'))
    renderCatalog('/catalog', { ...localCatalogReader, discover })
    await screen.findByText('Catalog could not be read.')
    expect(
      screen.getByText(/does not indicate medication stock/),
    ).toBeInTheDocument()
    expect(
      screen.queryByText('No matching presentations'),
    ).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Try again' }))
    await screen.findByText('9 presentations found')
    expect(discover).toHaveBeenCalledTimes(2)
  })
  it('keeps controls during new reads and ignores stale promise resolutions', async () => {
    const user = userEvent.setup()
    const old = deferred<CatalogResult>()
    const current = deferred<CatalogResult>()
    const discover = vi
      .fn(localCatalogReader.discover)
      .mockImplementationOnce(localCatalogReader.discover)
      .mockImplementationOnce(() => old.promise)
      .mockImplementationOnce(() => current.promise)
    renderCatalog('/catalog', { ...localCatalogReader, discover })
    await screen.findByText('9 presentations found')
    await user.selectOptions(
      screen.getByLabelText('Pharmacy branch'),
      'aster-commons',
    )
    expect(screen.getByRole('status')).toHaveTextContent('Loading catalog…')
    await user.selectOptions(
      screen.getByLabelText('Availability'),
      'unavailable',
    )
    await act(async () =>
      current.resolve(
        await localCatalogReader.discover({
          ...defaultCatalogQuery,
          branch: 'aster-commons',
          availability: 'unavailable',
        }),
      ),
    )
    await screen.findByText('2 presentations found')
    await act(async () =>
      old.resolve(
        await localCatalogReader.discover({
          ...defaultCatalogQuery,
          branch: 'aster-commons',
        }),
      ),
    )
    expect(screen.getByRole('status')).toHaveTextContent(
      '2 presentations found',
    )
  })
})

it('Home search navigates to the functional catalog with an encoded ingredient query', async () => {
  const user = userEvent.setup()
  render(
    <FavoritesProvider storage={null}>
      <MemoryRouter initialEntries={['/']}>
        <Routes>
          <Route path="/" element={<HomePage reader={localCatalogReader} />} />
          <Route
            path="/catalog"
            element={<CatalogPage reader={localCatalogReader} />}
          />
        </Routes>
        <HistoryControls />
      </MemoryRouter>
    </FavoritesProvider>,
  )
  await user.type(
    screen.getByRole('searchbox', {
      name: 'Medication name or active ingredient',
    }),
    'caffeine',
  )
  await user.click(screen.getByRole('button', { name: 'Search catalog' }))
  await screen.findByText('1 presentation found')
  expect(screen.getByTestId('url')).toHaveTextContent('/catalog?q=caffeine')
  expect(
    screen.getByRole('article', { name: /Paracetamol \+ Caffeine/ }),
  ).toBeInTheDocument()
})
