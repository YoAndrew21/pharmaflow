import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import canonical from '../../../demo-data/catalog.json'
import { App } from './App'
import i18n from '../i18n'
import { FAVORITES_KEY } from '../features/favorites/storage'

const saved = JSON.stringify({
  version: 1,
  medicationIds: ['med-par-500', 'unknown-id'],
})
beforeEach(() => {
  localStorage.clear()
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => ({ matches: false })),
  )
  vi.stubGlobal('scrollTo', vi.fn())
})
afterEach(() => vi.unstubAllGlobals())

function mockApi(empty = false) {
  const medications = empty
    ? []
    : canonical.medications.map((m) => ({
        ...m,
        displayName: `HTTP ${m.displayName}`,
      }))
  const branches = empty
    ? []
    : canonical.branches.map((b) => ({ ...b, name: `HTTP ${b.name}` }))
  const fetch = vi.fn(async (input: RequestInfo | URL) => {
    const path = String(input)
    if (path === '/api/medications') return Response.json(medications)
    if (path === '/api/pharmacies') return Response.json(branches)
    if (path === '/api/availability')
      return Response.json(empty ? [] : canonical.availability)
    const [kind, slug] = path.slice(5).split('/')
    const rows = kind === 'medications' ? medications : branches
    const row = rows.find((row) => row.slug === slug)
    return row ? Response.json(row) : new Response(null, { status: 404 })
  })
  vi.stubGlobal('fetch', fetch)
  return fetch
}
function mount(path: string) {
  window.history.replaceState(null, '', path)
  return render(<App />)
}

it.each([
  ['/', 'HTTP Paracetamol · 500 mg', '/api/medications'],
  ['/catalog', 'HTTP Paracetamol · 500 mg', '/api/medications'],
  [
    '/medications/paracetamol-500-tablets',
    'HTTP Paracetamol',
    '/api/medications/paracetamol-500-tablets',
  ],
  ['/pharmacies', 'HTTP Aster Commons', '/api/pharmacies'],
  [
    '/pharmacies/aster-commons',
    'HTTP Aster Commons',
    '/api/pharmacies/aster-commons',
  ],
  ['/favorites', 'HTTP Paracetamol · 500 mg', '/api/medications'],
])(
  '%s defaults to HTTP and displays API-derived values',
  async (path, text, endpoint) => {
    localStorage.setItem(FAVORITES_KEY, saved)
    const fetch = mockApi()
    mount(path)
    const role =
      path.startsWith('/medications/') || path.startsWith('/pharmacies/')
        ? 'heading'
        : 'link'
    expect(await screen.findByRole(role, { name: text })).toBeInTheDocument()
    expect(fetch).toHaveBeenCalledWith(endpoint, {
      signal: expect.any(AbortSignal),
    })
  },
)

it.each(['network', 'http', 'json', 'validation', 'abort'] as const)(
  'preserves Favorites after %s failure and never renders fixture records',
  async (failure) => {
    localStorage.setItem(FAVORITES_KEY, saved)
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        if (failure === 'network') throw new TypeError('private network detail')
        if (failure === 'abort') throw new DOMException('Aborted', 'AbortError')
        if (failure === 'http')
          return new Response('private backend detail', { status: 500 })
        if (failure === 'json') return new Response('{')
        return Response.json([{ malformed: true }])
      }),
    )
    mount('/favorites')
    await screen.findByRole('button', { name: i18n.t('catalog.retry') })
    expect(localStorage.getItem(FAVORITES_KEY)).toBe(saved)
    expect(
      screen.queryByRole('link', { name: /Paracetamol · 500/ }),
    ).not.toBeInTheDocument()
    expect(screen.queryByText(/private .* detail/)).not.toBeInTheDocument()
  },
)

it('reconciles unknown IDs only after complete successful HTTP reads', async () => {
  localStorage.setItem(FAVORITES_KEY, saved)
  mockApi()
  mount('/favorites')
  await screen.findByRole('link', { name: 'HTTP Paracetamol · 500 mg' })
  await waitFor(() =>
    expect(JSON.parse(localStorage.getItem(FAVORITES_KEY)!)).toEqual({
      version: 1,
      medicationIds: ['med-par-500'],
    }),
  )
})
it('treats valid authoritative HTTP 200 [] as empty and prunes stale Favorites', async () => {
  localStorage.setItem(FAVORITES_KEY, saved)
  mockApi(true)
  mount('/favorites')
  // Existing persistence removes its key when the authoritative result clears all IDs.
  await waitFor(() => expect(localStorage.getItem(FAVORITES_KEY)).toBeNull())
  expect(
    screen.getByRole('link', { name: 'Favorites, 0 saved presentations' }),
  ).toBeInTheDocument()
  expect(
    screen.queryByRole('link', { name: /Paracetamol · 500/ }),
  ).not.toBeInTheDocument()
})
it('does not reconcile if medication succeeds but another required collection fails', async () => {
  localStorage.setItem(FAVORITES_KEY, saved)
  vi.stubGlobal(
    'fetch',
    vi.fn(async (path: string) =>
      path === '/api/medications'
        ? Response.json([])
        : new Response(null, { status: 503 }),
    ),
  )
  mount('/favorites')
  await screen.findByRole('button', { name: i18n.t('catalog.retry') })
  expect(localStorage.getItem(FAVORITES_KEY)).toBe(saved)
})
it('retries a failed HTTP read successfully without remounting', async () => {
  const fetch = vi.fn(async () => new Response(null, { status: 500 }))
  vi.stubGlobal('fetch', fetch)
  mount('/catalog')
  const retry = await screen.findByRole('button', {
    name: i18n.t('catalog.retry'),
  })
  mockApi()
  await userEvent.click(retry)
  expect(
    await screen.findByRole('link', { name: 'HTTP Paracetamol · 500 mg' }),
  ).toBeInTheDocument()
})
it('aborts obsolete full-page HTTP requests without pruning Favorites', async () => {
  localStorage.setItem(FAVORITES_KEY, saved)
  const signals: AbortSignal[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(
      (_path: string, options: RequestInit) =>
        new Promise<Response>((_resolve, reject) => {
          signals.push(options.signal as AbortSignal)
          options.signal!.addEventListener('abort', () =>
            reject(new DOMException('Aborted', 'AbortError')),
          )
        }),
    ),
  )
  const view = mount('/favorites')
  expect(signals).toHaveLength(3)
  await act(async () => view.unmount())
  expect(signals.every((signal) => signal.aborted)).toBe(true)
  expect(localStorage.getItem(FAVORITES_KEY)).toBe(saved)
})
