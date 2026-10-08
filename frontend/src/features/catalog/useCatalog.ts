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
    state: ReadState<CatalogResult>
    metadata: CatalogResult | null
  } | null>(null)
  useEffect(() => {
    let active = true
    async function read() {
      try {
        const data = await reader.discover(query)
        if (active)
          setResponse({
            key: requestKey,
            state: { status: 'success', data },
            metadata: data,
          })
      } catch {
        if (active)
          setResponse((previous) => ({
            key: requestKey,
            state: { status: 'error', message: 'catalog-read-failed' },
            metadata: previous?.metadata ?? null,
          }))
      }
    }
    void read()
    return () => {
      active = false
    }
  }, [reader, query, requestKey])
  const state: ReadState<CatalogResult> =
    response?.key === requestKey ? response.state : { status: 'loading' }
  return { state, metadata: response?.metadata ?? null }
}
