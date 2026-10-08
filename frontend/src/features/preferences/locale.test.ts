import { afterEach, describe, expect, it, vi } from 'vitest'
import { LOCALE_KEY, persistLocale, readLocale } from './locale'

afterEach(() => vi.restoreAllMocks())

describe('browser-local language preference', () => {
  it('falls back and stays usable when access to localStorage itself is blocked', () => {
    vi.spyOn(window, 'localStorage', 'get').mockImplementation(() => {
      throw new DOMException('Storage access denied', 'SecurityError')
    })
    expect(readLocale()).toBe('en')
    expect(() => persistLocale('ja')).not.toThrow()
  })
  it.each([null, 'fr', 'es-ES', '{broken', ''])(
    'falls back safely for %s',
    (value) => {
      if (value === null) localStorage.removeItem(LOCALE_KEY)
      else localStorage.setItem(LOCALE_KEY, value)
      expect(readLocale()).toBe('en')
    },
  )
  it.each(['en', 'es', 'ja'] as const)(
    'persists %s independently of bookmarks and theme',
    (locale) => {
      localStorage.setItem('pharmaflow:theme', 'dark')
      localStorage.setItem(
        'pharmaflow:favorites',
        '{"version":1,"medicationIds":["med-par-500"]}',
      )
      persistLocale(locale)
      expect(readLocale()).toBe(locale)
      expect(localStorage.getItem('pharmaflow:theme')).toBe('dark')
      expect(localStorage.getItem('pharmaflow:favorites')).toBe(
        '{"version":1,"medicationIds":["med-par-500"]}',
      )
    },
  )
  it('handles failed reads and writes without crashing', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('restricted')
    })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('quota')
    })
    expect(readLocale()).toBe('en')
    expect(() => persistLocale('ja')).not.toThrow()
  })
})
