export const FAVORITES_KEY = 'pharmaflow:favorites'
export type FavoriteStorage = Pick<
  Storage,
  'getItem' | 'setItem' | 'removeItem'
>
export type StorageNotice = 'invalid' | 'unsupported' | 'unavailable' | null
export interface StoredFavorites {
  readonly ids: readonly string[]
  readonly notice: StorageNotice
}

export function isBookmarkId(value: unknown): value is string {
  return (
    typeof value === 'string' && /^[a-zA-Z0-9][a-zA-Z0-9_-]{0,127}$/.test(value)
  )
}

export function decodeFavorites(raw: string | null): StoredFavorites {
  if (raw === null) return { ids: [], notice: null }
  try {
    const value: unknown = JSON.parse(raw)
    if (!value || typeof value !== 'object' || Array.isArray(value))
      return { ids: [], notice: 'invalid' }
    const envelope = value as Record<string, unknown>
    if (typeof envelope.version !== 'number')
      return { ids: [], notice: 'invalid' }
    if (envelope.version !== 1) return { ids: [], notice: 'unsupported' }
    if (
      !Array.isArray(envelope.medicationIds) ||
      !envelope.medicationIds.every(isBookmarkId)
    )
      return { ids: [], notice: 'invalid' }
    return {
      ids: [...new Set(envelope.medicationIds as string[])],
      notice: null,
    }
  } catch {
    return { ids: [], notice: 'invalid' }
  }
}

export function browserFavoriteStorage(): FavoriteStorage | null {
  try {
    return window.localStorage
  } catch {
    return null
  }
}

export function readFavorites(
  storage: FavoriteStorage | null,
): StoredFavorites {
  try {
    return storage
      ? decodeFavorites(storage.getItem(FAVORITES_KEY))
      : { ids: [], notice: 'unavailable' }
  } catch {
    return { ids: [], notice: 'unavailable' }
  }
}

/** Empty sets remove this key only. Initialization and storage events never write. */
export function writeFavorites(
  storage: FavoriteStorage | null,
  ids: readonly string[],
): boolean {
  try {
    if (!storage) return false
    if (ids.length)
      storage.setItem(
        FAVORITES_KEY,
        JSON.stringify({ version: 1, medicationIds: [...new Set(ids)] }),
      )
    else storage.removeItem(FAVORITES_KEY)
    return true
  } catch {
    return false
  }
}
