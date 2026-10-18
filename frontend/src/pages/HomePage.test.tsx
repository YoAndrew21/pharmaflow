import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import type { CatalogReader } from '../data/catalogReader'
import { localCatalogReader } from '../data/localCatalogReader'
import { defaultCatalogQuery, type CatalogResult } from '../domain/catalog'
import { FavoritesProvider } from '../features/favorites/FavoritesContext'
import { HomePage } from './HomePage'
import { CatalogPage } from './CatalogPage'

function setup(reader: CatalogReader = localCatalogReader) {
  return render(
    <FavoritesProvider storage={null}>
      <MemoryRouter>
        <Routes>
          <Route path="/" element={<HomePage reader={reader} />} />
          <Route
            path="/catalog"
            element={<CatalogPage reader={localCatalogReader} />}
          />
        </Routes>
      </MemoryRouter>
    </FavoritesProvider>,
  )
}

describe('Home discovery reads', () => {
  it('uses canonical discovery presentations, forms and branches with meaningful heading levels', async () => {
    const discover = vi.fn(localCatalogReader.discover)
    setup({ ...localCatalogReader, discover })
    const list = await screen.findByRole('list', {
      name: 'A little exploration. More clarity.',
    })
    expect(discover).toHaveBeenCalledWith(
      defaultCatalogQuery,
      expect.any(AbortSignal),
    )
    const data = await localCatalogReader.discover(defaultCatalogQuery)
    expect(within(list).getAllByRole('article')).toHaveLength(data.items.length)
    for (const { medication } of data.items) {
      expect(
        within(list).getByRole('heading', {
          name: `${medication.displayName} · ${medication.strengthLabel}`,
          level: 3,
        }),
      ).toBeInTheDocument()
    }
    for (const form of data.forms)
      expect(
        screen.getByRole('link', { name: new RegExp(`^${form}$`, 'i') }),
      ).toHaveAttribute('href', `/catalog?form=${form}`)
    for (const branch of data.branches)
      expect(screen.getByRole('link', { name: branch.name })).toHaveAttribute(
        'href',
        `/pharmacies/${branch.slug}`,
      )
  })
  it('opens a form in the existing URL-filtered catalog', async () => {
    setup()
    await userEvent.click(await screen.findByRole('link', { name: 'Capsule' }))
    await screen.findByText('2 presentations found')
    expect(screen.getByLabelText('Pharmaceutical form')).toHaveValue('capsule')
    expect(screen.getAllByRole('article')).toHaveLength(2)
  })
  it('keeps search usable while discovery is pending, without displaying empty data', async () => {
    let resolve!: (data: CatalogResult) => void
    const promise = new Promise<CatalogResult>((yes) => {
      resolve = yes
    })
    setup({ ...localCatalogReader, discover: () => promise })
    expect(screen.getByRole('status')).toHaveTextContent('Loading catalog')
    expect(screen.getByRole('searchbox')).toBeEnabled()
    expect(
      screen.queryByText('No matching presentations'),
    ).not.toBeInTheDocument()
    await act(async () =>
      resolve(await localCatalogReader.discover(defaultCatalogQuery)),
    )
    await screen.findByRole('list', {
      name: 'A little exploration. More clarity.',
    })
  })
  it('distinguishes read failure from stock and retries through the same reader', async () => {
    const discover = vi
      .fn(localCatalogReader.discover)
      .mockRejectedValueOnce(new Error('read failed'))
    setup({ ...localCatalogReader, discover })
    await screen.findByRole('heading', { name: 'Catalog could not be read.' })
    expect(screen.getByRole('status')).toHaveTextContent(
      'does not indicate medication stock',
    )
    expect(
      screen.queryByRole('link', { name: 'Aster Commons' }),
    ).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))
    await screen.findByRole('list', {
      name: 'A little exploration. More clarity.',
    })
    expect(discover).toHaveBeenCalledTimes(2)
  })
  it('does not fabricate cards, forms or branches after a successful empty read', async () => {
    const data = await localCatalogReader.discover(defaultCatalogQuery)
    setup({
      ...localCatalogReader,
      discover: async () => ({ ...data, items: [], forms: [], branches: [] }),
    })
    await screen.findByText('No matching presentations')
    expect(screen.queryByRole('article')).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Next presentations' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Try again' }),
    ).not.toBeInTheDocument()
    expect(screen.getByRole('searchbox')).toBeEnabled()
  })
})
