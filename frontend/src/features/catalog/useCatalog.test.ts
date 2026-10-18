import { act, renderHook, waitFor } from '@testing-library/react'
import { expect, it, vi } from 'vitest'
import type { CatalogReader } from '../../data/catalogReader'
import { localCatalogReader } from '../../data/localCatalogReader'
import { defaultCatalogQuery, type CatalogResult } from '../../domain/catalog'
import { useCatalog } from './useCatalog'

function pending() {
  let resolve!: (result: CatalogResult) => void
  const promise = new Promise<CatalogResult>((yes) => {
    resolve = yes
  })
  return { promise, resolve }
}

it('reader identity invalidates an already successful response even with the same request key', async () => {
  const first: CatalogReader = {
    ...localCatalogReader,
    discover: vi.fn(localCatalogReader.discover),
  }
  const deferred = pending()
  const second: CatalogReader = {
    ...localCatalogReader,
    discover: vi.fn(() => deferred.promise),
  }
  const { result, rerender } = renderHook(
    ({ reader }) => useCatalog(reader, defaultCatalogQuery, 'same-key'),
    { initialProps: { reader: first } },
  )
  await waitFor(() => expect(result.current.state.status).toBe('success'))
  const signal = vi.mocked(first.discover).mock.calls[0]![1]!
  rerender({ reader: second })
  expect(signal.aborted).toBe(true)
  expect(result.current.state.status).toBe('loading')
  expect(result.current.metadata).toBeNull()
  await act(async () =>
    deferred.resolve(await localCatalogReader.discover(defaultCatalogQuery)),
  )
  expect(result.current.state.status).toBe('success')
})
it('ignores a late obsolete response even when its reader ignores cancellation', async () => {
  const older = pending(),
    newer = pending()
  const reader: CatalogReader = {
    ...localCatalogReader,
    discover: vi
      .fn()
      .mockReturnValueOnce(older.promise)
      .mockReturnValueOnce(newer.promise),
  }
  const { result, rerender } = renderHook(
    ({ key }) => useCatalog(reader, defaultCatalogQuery, key),
    { initialProps: { key: 'old' } },
  )
  rerender({ key: 'new' })
  const data = await localCatalogReader.discover(defaultCatalogQuery)
  await act(async () => newer.resolve({ ...data, items: [] }))
  await act(async () => older.resolve(data))
  expect(result.current.state).toEqual({
    status: 'success',
    data: { ...data, items: [] },
  })
})
