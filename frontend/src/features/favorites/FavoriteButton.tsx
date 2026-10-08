import { Bookmark, BookmarkCheck } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import type { Medication } from '../../domain/models'
import { useFavorites } from './useFavorites'
import styles from './Favorites.module.css'

export function FavoriteButton({ medication }: { medication: Medication }) {
  const { t } = useTranslation()
  const favorites = useFavorites()
  const saved = favorites.isFavorite(medication.id)
  const canonicalLabel = `${medication.displayName} · ${medication.strengthLabel} · ${medication.presentationLabel}`
  const label = t(`domain.favoriteLabels.${medication.id}`, {
    defaultValue: canonicalLabel,
  })
  const Icon = saved ? BookmarkCheck : Bookmark
  return (
    <button
      type="button"
      className={styles.toggle}
      aria-pressed={saved}
      aria-label={t(saved ? 'favorites.removeLabel' : 'favorites.saveLabel', {
        label,
      })}
      data-favorite-id={medication.id}
      onClick={() => favorites.toggleFavorite(medication.id, canonicalLabel)}
    >
      <Icon size={18} aria-hidden="true" />
      <span>{t(saved ? 'favorites.saved' : 'favorites.save')}</span>
    </button>
  )
}
