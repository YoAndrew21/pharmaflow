import { useEffect, useState } from 'react'
import type { CatalogReader } from '../../data/catalogReader'
import type { CatalogQuery, CatalogResult } from '../../domain/catalog'
import type { ReadState } from '../../domain/models'

export function useCatalog(
  reader: CatalogReader,
  query: CatalogQuery,
  requestKey: string,
) {
  const [response, setResponse] = useState<{
    key: string
    reader: CatalogReader
    state: ReadState<CatalogResult>
    metadata: CatalogResult | null
  } | null>(null)
  useEffect(() => {
    let active = true
    const controller = new AbortController()
    async function read() {
      try {
        const data = await reader.discover(query, controller.signal)
        if (active)
          setResponse({
            key: requestKey,
            reader,
            state: { status: 'success', data },
            metadata: data,
          })
      } catch {
        if (active)
          setResponse((previous) => ({
            key: requestKey,
            reader,
            state: { status: 'error', message: 'catalog-read-failed' },
            metadata: previous?.reader === reader ? previous.metadata : null,
          }))
      }
    }
    void read()
    return () => {
      active = false
      controller.abort()
    }
  }, [reader, query, requestKey])
  const state: ReadState<CatalogResult> =
    response?.key === requestKey && response.reader === reader
      ? response.state
      : { status: 'loading' }
  return {
    state,
    metadata: response?.reader === reader ? response.metadata : null,
  }
}
