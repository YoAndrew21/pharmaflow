import { useEffect, useState } from 'react'
import type { CatalogReader } from '../../data/catalogReader'
import { defaultCatalogQuery, type CatalogResult } from '../../domain/catalog'
import type { ReadState } from '../../domain/models'

interface FavoriteCatalog {
  readonly catalog: CatalogResult
  readonly validIds: readonly string[]
}
export function useFavoriteCatalog(
  reader: CatalogReader,
  attempt: number,
): ReadState<FavoriteCatalog> {
  const [response, setResponse] = useState<{
    reader: CatalogReader
    attempt: number
    state: ReadState<FavoriteCatalog>
  } | null>(null)
  useEffect(() => {
    let active = true
    const controller = new AbortController()
    void (async () => {
      try {
        // Only listMedications is the complete authoritative lookup for stale IDs.
        const [medications, catalog] = await Promise.all([
          reader.listMedications(controller.signal),
          reader.discover(defaultCatalogQuery, controller.signal),
        ])
        if (active)
          setResponse({
            reader,
            attempt,
            state: {
              status: 'success',
              data: {
                catalog,
                validIds: medications.map((medication) => medication.id),
              },
            },
          })
      } catch {
        if (active)
          setResponse({
            reader,
            attempt,
            state: { status: 'error', message: 'favorites-read-failed' },
          })
      }
    })()
    return () => {
      active = false
      controller.abort()
    }
  }, [reader, attempt])
  return response?.reader === reader && response.attempt === attempt
    ? response.state
    : { status: 'loading' }
}
