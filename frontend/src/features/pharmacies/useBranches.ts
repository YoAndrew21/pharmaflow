import { useCallback, useEffect, useState } from 'react'
import type { CatalogReader } from '../../data/catalogReader'
import type { ReadState } from '../../domain/models'

/** Narrow lifecycle for branch reads; stale requests cannot replace the current route. */
function useBranchRead<T>(
  read: () => Promise<T>,
  attempt: number,
): ReadState<T> {
  const [response, setResponse] = useState<{
    read: typeof read
    attempt: number
    state: ReadState<T>
  } | null>(null)
  useEffect(() => {
    let active = true
    void (async () => {
      try {
        const data = await read()
        if (active)
          setResponse({ read, attempt, state: { status: 'success', data } })
      } catch {
        if (active)
          setResponse({
            read,
            attempt,
            state: { status: 'error', message: 'branch-read-failed' },
          })
      }
    })()
    return () => {
      active = false
    }
  }, [read, attempt])
  return response?.read === read && response.attempt === attempt
    ? response.state
    : { status: 'loading' }
}

export function useBranchDirectory(reader: CatalogReader, attempt: number) {
  const read = useCallback(() => reader.listBranches(), [reader])
  return useBranchRead(read, attempt)
}

export function useBranchDetail(
  reader: CatalogReader,
  slug: string,
  attempt: number,
) {
  const read = useCallback(() => reader.getBranch(slug), [reader, slug])
  return useBranchRead(read, attempt)
}
