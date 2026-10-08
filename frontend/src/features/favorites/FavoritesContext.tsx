import {
  useCallback,
  useEffect,
  useMemo,
  useReducer,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import { useTranslation } from 'react-i18next'
import {
  browserFavoriteStorage,
  decodeFavorites,
  FAVORITES_KEY,
  readFavorites,
  writeFavorites,
  type FavoriteStorage,
} from './storage'
import {
  favoritesReducer,
  type FavoritesAction,
  type FavoritesState,
} from './state'
import { FavoritesContext } from './favoritesContextValue'
import styles from './Favorites.module.css'

export function FavoritesProvider({
  children,
  storage,
}: {
  children: ReactNode
  storage?: FavoriteStorage | null
}) {
  const { t, i18n } = useTranslation()
  const [binding] = useState(() => {
    const selected = storage === undefined ? browserFavoriteStorage() : storage
    return {
      storage: selected,
      initial: {
        ...readFavorites(selected),
        announcement: null,
      } as FavoritesState,
    }
  })
  const [state, dispatch] = useReducer(favoritesReducer, binding.initial)
  const current = useRef(binding.initial)
  const commit = useCallback(
    (action: FavoritesAction) => {
      const next = favoritesReducer(current.current, action)
      if (next === current.current) return
      // Persist only explicit local mutations: no initial overwrite, no cross-tab echo.
      const persisted = writeFavorites(binding.storage, next.ids)
      const finalState = {
        ...next,
        notice: persisted ? null : 'unavailable',
      } as FavoritesState
      current.current = finalState
      dispatch({ type: 'replace', state: finalState })
    },
    [binding],
  )
  useEffect(() => {
    function sync(event: StorageEvent) {
      if (event.key !== FAVORITES_KEY && event.key !== null) return
      if (
        !binding.storage ||
        (event.storageArea && event.storageArea !== binding.storage)
      )
        return
      const incoming = {
        ...decodeFavorites(event.newValue),
        announcement: { key: 'synced' },
      } as FavoritesState
      current.current = incoming
      dispatch({ type: 'replace', state: incoming })
    }
    window.addEventListener('storage', sync)
    return () => window.removeEventListener('storage', sync)
  }, [binding])
  const addFavorite = useCallback(
    (id: string, label?: string) => commit({ type: 'add', id, label }),
    [commit],
  )
  const removeFavorite = useCallback(
    (id: string, label?: string) => commit({ type: 'remove', id, label }),
    [commit],
  )
  const toggleFavorite = useCallback(
    (id: string, label?: string) => commit({ type: 'toggle', id, label }),
    [commit],
  )
  const clearFavorites = useCallback(() => commit({ type: 'clear' }), [commit])
  const reconcile = useCallback(
    (validIds: readonly string[]) => commit({ type: 'reconcile', validIds }),
    [commit],
  )
  const value = useMemo(
    () => ({
      ...state,
      isFavorite: (id: string) => state.ids.includes(id),
      addFavorite,
      removeFavorite,
      toggleFavorite,
      clearFavorites,
      reconcile,
    }),
    [
      state,
      addFavorite,
      removeFavorite,
      toggleFavorite,
      clearFavorites,
      reconcile,
    ],
  )
  return (
    <FavoritesContext.Provider value={value}>
      {children}
      <div
        className={styles.announcement}
        aria-live="polite"
        aria-atomic="true"
      >
        {state.announcement
          ? t(`favorites.announcements.${state.announcement.key}`, {
              label:
                state.announcement.medicationId &&
                state.announcement.label ===
                  i18n.getResource(
                    'en',
                    'translation',
                    `domain.favoriteLabels.${state.announcement.medicationId}`,
                  )
                  ? t(
                      `domain.favoriteLabels.${state.announcement.medicationId}`,
                      {
                        defaultValue: state.announcement.label,
                      },
                    )
                  : state.announcement.label,
            })
          : ''}
      </div>
    </FavoritesContext.Provider>
  )
}
