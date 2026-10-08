import { isBookmarkId, type StoredFavorites } from './storage'

export interface FavoritesState extends StoredFavorites {
  readonly announcement: {
    readonly key: 'saved' | 'removed' | 'cleared' | 'pruned' | 'synced'
    readonly label?: string
    readonly medicationId?: string
  } | null
}
export type FavoritesAction =
  | { type: 'add' | 'remove' | 'toggle'; id: string; label?: string }
  | { type: 'clear' }
  | { type: 'reconcile'; validIds: readonly string[] }
  | { type: 'replace'; state: FavoritesState }

export function favoritesReducer(
  state: FavoritesState,
  action: FavoritesAction,
): FavoritesState {
  if (action.type === 'replace') return action.state
  if (action.type === 'clear')
    return { ...state, ids: [], announcement: { key: 'cleared' } }
  if (action.type === 'reconcile') {
    const valid = new Set(action.validIds)
    const ids = state.ids.filter((id) => valid.has(id))
    return ids.length === state.ids.length
      ? state
      : { ...state, ids, announcement: { key: 'pruned' } }
  }
  if (!isBookmarkId(action.id)) return state
  const exists = state.ids.includes(action.id)
  const remove =
    action.type === 'remove' || (action.type === 'toggle' && exists)
  if ((remove && !exists) || (!remove && exists)) return state
  return {
    ...state,
    ids: remove
      ? state.ids.filter((id) => id !== action.id)
      : [...state.ids, action.id],
    announcement: {
      key: remove ? 'removed' : 'saved',
      label: action.label ?? action.id,
      medicationId: action.id,
    },
  }
}
