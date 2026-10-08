import { useContext } from 'react'
import { FavoritesContext } from './favoritesContextValue'

export function useFavorites() {
  const value = useContext(FavoritesContext)
  if (!value) throw new Error('FavoritesProvider is required')
  return value
}
