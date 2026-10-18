import { afterEach, describe, expect, it, vi } from 'vitest'
import canonical from '../../../demo-data/catalog.json'
import { defaultCatalogQuery } from '../domain/catalog'
import { createHttpCatalogReader } from './httpCatalogReader'
import { ApiReadError, readJson } from './httpClient'
import { installCanonicalFetch } from '../test/apiFetch'

afterEach(() => vi.unstubAllGlobals())

describe('HTTP catalog boundary', () => {
  it('reads all collections, keeps exact values/order and uses relative GET URLs', async () => {
    const fetch = installCanonicalFetch()
    const reader = createHttpCatalogReader()
    expect(await reader.listMedications()).toEqual(canonical.medications)
    expect(await reader.listBranches()).toEqual(canonical.branches)
    expect(await reader.getAvailability('med-par-500')).toEqual(
      canonical.availability.filter((o) => o.medicationId === 'med-par-500'),
    )
    expect(fetch.mock.calls.map((call) => call[0])).toEqual([
      '/api/medications',
      '/api/pharmacies',
      '/api/availability',
    ])
  })

  it('reads details and maps only detail 404 to successful null', async () => {
    const fetch = installCanonicalFetch()
    const reader = createHttpCatalogReader()
    expect(await reader.getMedication('paracetamol-500-tablets')).toEqual(
      canonical.medications[0],
    )
    expect(await reader.getBranch('aster-commons')).toEqual(
      canonical.branches[0],
    )
    expect(await reader.getMedication('missing')).toBeNull()
    expect(await reader.getBranch('missing')).toBeNull()
    await reader.getMedication('a/b ?')
    expect(fetch).toHaveBeenLastCalledWith('/api/medications/a%2Fb%20%3F', {
      signal: undefined,
    })
  })

  it.each([404, 401, 500])(
    'rejects collection HTTP %s without reading backend errors',
    async (status) => {
      vi.stubGlobal(
        'fetch',
        vi.fn(async () => new Response('private backend error', { status })),
      )
      await expect(
        createHttpCatalogReader().listMedications(),
      ).rejects.toMatchObject({ kind: 'http', status })
    },
  )
  it('keeps non-404 detail failures as failures', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(null, { status: 503 })),
    )
    await expect(
      createHttpCatalogReader().getBranch('aster-commons'),
    ).rejects.toMatchObject({ kind: 'http', status: 503 })
  })
  it('categorizes network errors without exposing their message', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockRejectedValue(new Error('private network details')),
    )
    await expect(readJson('medications')).rejects.toEqual(
      new ApiReadError('network'),
    )
  })
  it('rejects malformed JSON', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response('{')),
    )
    await expect(
      createHttpCatalogReader().listMedications(),
    ).rejects.toMatchObject({ kind: 'invalid-response' })
  })
  it.each([
    null,
    {},
    [{ id: 'incomplete' }],
    [{ ...canonical.medications[0], prescriptionRequired: 'false' }],
    [{ ...canonical.medications[0], activeIngredients: [] }],
  ])('rejects malformed medication response %#', async (value) => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => Response.json(value)),
    )
    await expect(
      createHttpCatalogReader().listMedications(),
    ).rejects.toMatchObject({ kind: 'invalid-response' })
  })
  it('rejects null detail JSON on 200 rather than treating it as 404', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => Response.json(null)),
    )
    await expect(
      createHttpCatalogReader().getMedication('known'),
    ).rejects.toMatchObject({ kind: 'invalid-response' })
  })
  it('requires every weekday and validates opening intervals', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        Response.json([
          { ...canonical.branches[0], openingHours: { monday: [] } },
        ]),
      ),
    )
    await expect(
      createHttpCatalogReader().listBranches(),
    ).rejects.toMatchObject({ kind: 'invalid-response' })
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        Response.json([
          {
            ...canonical.branches[0],
            openingHours: {
              ...canonical.branches[0]!.openingHours,
              monday: [{ opensAt: '18:00', closesAt: '08:00' }],
            },
          },
        ]),
      ),
    )
    await expect(
      createHttpCatalogReader().listBranches(),
    ).rejects.toMatchObject({ kind: 'invalid-response' })
  })
  it.each([Number.MAX_SAFE_INTEGER + 1, -1, 1.5])(
    'rejects unsafe/negative/fractional price %s',
    async (minorUnits) => {
      vi.stubGlobal(
        'fetch',
        vi.fn(async () =>
          Response.json([
            {
              ...canonical.availability[0],
              referencePrice: { minorUnits, currencyCode: 'USD' },
            },
          ]),
        ),
      )
      await expect(
        createHttpCatalogReader().getAvailability('med-par-500'),
      ).rejects.toMatchObject({ kind: 'invalid-response' })
    },
  )
  it.each([undefined, {}, { minorUnits: 10, currencyCode: 'invalid' }])(
    'requires explicit null or a valid price %#',
    async (referencePrice) => {
      vi.stubGlobal(
        'fetch',
        vi.fn(async () =>
          Response.json([{ ...canonical.availability[0], referencePrice }]),
        ),
      )
      await expect(
        createHttpCatalogReader().getAvailability('med-par-500'),
      ).rejects.toMatchObject({ kind: 'invalid-response' })
    },
  )
  it.each(['2026-02-30T00:00:00Z', '2026-10-01T00:00:00+00:00', 'bad'])(
    'rejects invalid/noncanonical UTC instant %s',
    async (updatedAt) => {
      vi.stubGlobal(
        'fetch',
        vi.fn(async () =>
          Response.json([{ ...canonical.availability[0], updatedAt }]),
        ),
      )
      await expect(
        createHttpCatalogReader().getAvailability('med-par-500'),
      ).rejects.toMatchObject({ kind: 'invalid-response' })
    },
  )
  it('accepts observations after the fixed snapshot, preserving UTC values', async () => {
    const updatedAt = '2030-01-01T00:00:00Z'
    const row = { ...canonical.availability[0]!, updatedAt }
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => Response.json([row])),
    )
    expect(
      await createHttpCatalogReader().getAvailability(row.medicationId),
    ).toEqual([row])
  })
  it('preserves null prices and unavailable-but-priced observations from HTTP', async () => {
    installCanonicalFetch()
    const reader = createHttpCatalogReader()
    const all = (
      await Promise.all(
        canonical.medications.map((m) => reader.getAvailability(m.id)),
      )
    ).flat()
    expect(all.filter((o) => o.referencePrice === null)).toEqual(
      canonical.availability.filter((o) => o.referencePrice === null),
    )
    const pricedUnavailable = canonical.availability.filter(
      (o) => o.status === 'unavailable' && o.referencePrice !== null,
    )
    expect(pricedUnavailable.length).toBeGreaterThan(0)
    expect(
      all.filter(
        (o) => o.status === 'unavailable' && o.referencePrice !== null,
      ),
    ).toEqual(pricedUnavailable)
  })
  it('rejects duplicate identities and observation pairs', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        Response.json([canonical.medications[0], canonical.medications[0]]),
      ),
    )
    await expect(
      createHttpCatalogReader().listMedications(),
    ).rejects.toMatchObject({ kind: 'invalid-response' })
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        Response.json([canonical.availability[0], canonical.availability[0]]),
      ),
    )
    await expect(
      createHttpCatalogReader().getAvailability('med-par-500'),
    ).rejects.toMatchObject({ kind: 'invalid-response' })
  })
  it('uses three collection requests for discovery and preserves missing/null observations', async () => {
    const fetch = installCanonicalFetch()
    const result = await createHttpCatalogReader().discover(defaultCatalogQuery)
    expect(fetch).toHaveBeenCalledTimes(3)
    expect(result.snapshotAt).toBe(canonical.snapshotAt)
    expect(result.items.reduce((n, item) => n + item.summary.unknown, 0)).toBe(
      6,
    )
    const reader = createHttpCatalogReader()
    const observations = await reader.getAvailability('med-par-500')
    expect(observations).toEqual(
      canonical.availability.filter((o) => o.medicationId === 'med-par-500'),
    )
    expect(await reader.getAvailability('missing')).toEqual([])
  })
  it('rejects orphan references instead of manufacturing a discovery result', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (path: string) =>
        Response.json(
          path.endsWith('availability')
            ? [{ ...canonical.availability[0], medicationId: 'orphan' }]
            : path.endsWith('medications')
              ? canonical.medications
              : canonical.branches,
        ),
      ),
    )
    await expect(
      createHttpCatalogReader().discover(defaultCatalogQuery),
    ).rejects.toMatchObject({ kind: 'invalid-response' })
  })
  it('accepts authoritative empty collections without local fallback', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => Response.json([])),
    )
    const reader = createHttpCatalogReader()
    expect(await reader.listMedications()).toEqual([])
    expect(await reader.listBranches()).toEqual([])
    expect((await reader.discover(defaultCatalogQuery)).items).toEqual([])
  })
  it('propagates cancellation to fetch and never converts it into an empty result', async () => {
    const controller = new AbortController()
    const fetch = vi.fn(
      (_path: string, options: RequestInit) =>
        new Promise((_resolve, reject) =>
          options.signal!.addEventListener('abort', () =>
            reject(new DOMException('Aborted', 'AbortError')),
          ),
        ),
    )
    vi.stubGlobal('fetch', fetch)
    const request = createHttpCatalogReader().listMedications(controller.signal)
    controller.abort()
    await expect(request).rejects.toMatchObject({ name: 'AbortError' })
    expect(fetch).toHaveBeenCalledWith('/api/medications', {
      signal: controller.signal,
    })
  })
  it('deduplicates simultaneous reads in one scope but isolates cancellation between consumers', async () => {
    const fetch = installCanonicalFetch(),
      reader = createHttpCatalogReader()
    const first = new AbortController(),
      second = new AbortController()
    await Promise.all([
      reader.listMedications(first.signal),
      reader.discover(defaultCatalogQuery, first.signal),
    ])
    expect(fetch).toHaveBeenCalledTimes(3)
    first.abort()
    expect(await reader.listMedications(second.signal)).toHaveLength(9)
    expect(second.signal.aborted).toBe(false)
    expect(fetch).toHaveBeenCalledTimes(4)
  })
})
