import { describe, expect, it, vi } from 'vitest'
import {
  browserFavoriteStorage,
  decodeFavorites,
  FAVORITES_KEY,
  readFavorites,
  writeFavorites,
} from './storage'

describe('favorite storage boundary', () => {
  it('starts empty when the key is absent', () => {
    expect(decodeFavorites(null)).toEqual({ ids: [], notice: null })
  })
  it('validates the envelope and normalizes duplicates', () => {
    expect(
      decodeFavorites(
        JSON.stringify({
          version: 1,
          medicationIds: ['med-par-500', 'med-par-500', 'med-cet-10'],
        }),
      ),
    ).toEqual({ ids: ['med-par-500', 'med-cet-10'], notice: null })
  })
  it.each([
    '{',
    'null',
    '[]',
    '42',
    '{}',
    '{"version":1}',
    '{"version":1,"medicationIds":"med-par-500"}',
    '{"version":1,"medicationIds":[null]}',
    '{"version":1,"medicationIds":[""]}',
    '{"version":1,"medicationIds":["med par"]}',
  ])('recovers unsafe payload %s without throwing', (raw) => {
    expect(decodeFavorites(raw)).toEqual({ ids: [], notice: 'invalid' })
  })
  it('keeps unsupported versions separate from corruption', () => {
    expect(
      decodeFavorites('{"version":2,"medicationIds":["med-par-500"]}'),
    ).toEqual({ ids: [], notice: 'unsupported' })
  })
  it('handles unavailable storage and read exceptions', () => {
    expect(readFavorites(null)).toEqual({ ids: [], notice: 'unavailable' })
    expect(
      readFavorites({
        getItem: () => {
          throw new Error('restricted')
        },
        setItem: vi.fn(),
        removeItem: vi.fn(),
      }),
    ).toEqual({ ids: [], notice: 'unavailable' })
  })
  it('handles access to localStorage itself throwing', () => {
    const getter = vi
      .spyOn(window, 'localStorage', 'get')
      .mockImplementation(() => {
        throw new Error('restricted')
      })
    expect(browserFavoriteStorage()).toBeNull()
    getter.mockRestore()
  })
  it('stores only unique IDs in the versioned envelope', () => {
    const storage = { getItem: vi.fn(), setItem: vi.fn(), removeItem: vi.fn() }
    expect(writeFavorites(storage, ['med-par-500', 'med-par-500'])).toBe(true)
    expect(storage.setItem).toHaveBeenCalledWith(
      FAVORITES_KEY,
      '{"version":1,"medicationIds":["med-par-500"]}',
    )
  })
  it('reports write or remove failure honestly', () => {
    const storage = {
      getItem: vi.fn(),
      setItem: () => {
        throw new Error('quota')
      },
      removeItem: () => {
        throw new Error('restricted')
      },
    }
    expect(writeFavorites(storage, ['med-par-500'])).toBe(false)
    expect(writeFavorites(storage, [])).toBe(false)
    expect(writeFavorites(null, ['med-par-500'])).toBe(false)
  })
  it('clear deletes only the favorites key and preserves unrelated data', () => {
    localStorage.setItem('pharmaflow:theme', 'dark')
    localStorage.setItem('unrelated', 'keep')
    localStorage.setItem(FAVORITES_KEY, '{}')
    expect(writeFavorites(localStorage, [])).toBe(true)
    expect(localStorage.getItem(FAVORITES_KEY)).toBeNull()
    expect(localStorage.getItem('pharmaflow:theme')).toBe('dark')
    expect(localStorage.getItem('unrelated')).toBe('keep')
    localStorage.removeItem('pharmaflow:theme')
    localStorage.removeItem('unrelated')
  })
})
