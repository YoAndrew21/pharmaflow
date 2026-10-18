import { vi } from 'vitest'
import canonical from '../../../demo-data/catalog.json'

/** Explicit HTTP fixture for complete-shell tests; never installed globally. */
export function installCanonicalFetch() {
  const fetch = vi.fn(async (input: RequestInfo | URL) => {
    const path = String(input)
    if (path === '/api/medications') return Response.json(canonical.medications)
    if (path === '/api/pharmacies') return Response.json(canonical.branches)
    if (path === '/api/availability')
      return Response.json(canonical.availability)
    const [kind, slug] = path.slice('/api/'.length).split('/')
    const rows =
      kind === 'medications' ? canonical.medications : canonical.branches
    const value = rows.find(
      (row) => row.slug === decodeURIComponent(slug ?? ''),
    )
    return value ? Response.json(value) : new Response(null, { status: 404 })
  })
  vi.stubGlobal('fetch', fetch)
  return fetch
}
