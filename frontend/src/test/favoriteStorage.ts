import { vi } from 'vitest'
import { FAVORITES_KEY } from '../features/favorites/storage'

export function memoryStorage(raw: string | null = null) {
  const values = new Map<string, string>(
    raw === null ? [] : [[FAVORITES_KEY, raw]],
  )
  return {
    getItem: vi.fn((key: string) => values.get(key) ?? null),
    setItem: vi.fn((key: string, value: string) => {
      values.set(key, value)
    }),
    removeItem: vi.fn((key: string) => {
      values.delete(key)
    }),
  }
}
