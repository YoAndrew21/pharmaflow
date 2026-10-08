import { createContext } from 'react'
import type { FavoritesState } from './state'

export interface FavoritesValue extends FavoritesState {
  isFavorite(id: string): boolean
  addFavorite(id: string, label?: string): void
  removeFavorite(id: string, label?: string): void
  toggleFavorite(id: string, label?: string): void
  clearFavorites(): void
  reconcile(validIds: readonly string[]): void
}
export const FavoritesContext = createContext<FavoritesValue | null>(null)
