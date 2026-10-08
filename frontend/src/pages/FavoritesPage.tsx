import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router'
import { useTranslation } from 'react-i18next'
import type { CatalogReader } from '../data/catalogReader'
import { localCatalogReader } from '../data/localCatalogReader'
import { MedicationCard } from '../features/catalog/MedicationCard'
import { useFavorites } from '../features/favorites/useFavorites'
import { useFavoriteCatalog } from '../features/favorites/useFavoriteCatalog'
import styles from '../features/favorites/Favorites.module.css'
import catalogStyles from '../features/catalog/Catalog.module.css'
import { StateArtwork } from '../components/StateArtwork'
import { LoadingMark } from '../components/LoadingMark'

export function FavoritesPage({
  reader = localCatalogReader,
}: {
  reader?: CatalogReader
}) {
  const { t } = useTranslation()
  const favorites = useFavorites()
  const { ids, reconcile } = favorites
  const [attempt, setAttempt] = useState(0)
  const [confirming, setConfirming] = useState(false)
  const state = useFavoriteCatalog(reader, attempt)
  const list = useRef<HTMLUListElement>(null)
  const emptyLink = useRef<HTMLAnchorElement>(null)
  const clearButton = useRef<HTMLButtonElement>(null)
  const confirmButton = useRef<HTMLButtonElement>(null)
  const pendingFocus = useRef<string | null>(null)
  const items =
    state.status === 'success'
      ? state.data.catalog.items.filter((item) =>
          favorites.ids.includes(item.medication.id),
        )
      : []
  useEffect(() => {
    if (state.status === 'success') reconcile(state.data.validIds)
  }, [state, ids, reconcile])
  useEffect(() => {
    if (pendingFocus.current !== null) {
      const next = [
        ...(list.current?.querySelectorAll<HTMLButtonElement>(
          'button[data-favorite-id]',
        ) ?? []),
      ].find((button) => button.dataset.favoriteId === pendingFocus.current)
      ;(next ?? emptyLink.current ?? clearButton.current)?.focus()
      pendingFocus.current = null
    }
  }, [favorites.ids])
  useEffect(() => {
    if (confirming) confirmButton.current?.focus()
  }, [confirming])
  const empty = favorites.ids.length === 0
  return (
    <div className={`container ${styles.page}`}>
      <p className="eyebrow">{t('visual.favoritesEyebrow')}</p>
      <h1>{t('favorites.title')}</h1>
      <p className={styles.description}>{t('favorites.description')}</p>
      <p className={styles.description}>{t('favorites.privacy')}</p>
      {favorites.notice && (
        <p className={styles.notice}>
          {t(`favorites.notices.${favorites.notice}`)}
        </p>
      )}
      {!empty && (
        <div className={styles.toolbar}>
          <p>{t('favorites.count', { count: favorites.ids.length })}</p>
          <button
            ref={clearButton}
            type="button"
            className="button button-secondary"
            onClick={() => setConfirming(true)}
          >
            {t('favorites.clear')}
          </button>
        </div>
      )}
      {confirming && !empty && (
        <section
          className={styles.confirmation}
          aria-labelledby="clear-confirmation"
        >
          <p id="clear-confirmation">{t('favorites.confirmDescription')}</p>
          <button
            ref={confirmButton}
            className="button button-danger"
            type="button"
            onClick={() => {
              pendingFocus.current = ''
              favorites.clearFavorites()
              setConfirming(false)
            }}
          >
            {t('favorites.confirmClear')}
          </button>
          <button
            className="button button-secondary"
            type="button"
            onClick={() => {
              setConfirming(false)
              clearButton.current?.focus()
            }}
          >
            {t('favorites.cancel')}
          </button>
        </section>
      )}
      {empty ? (
        <section className={styles.state} aria-labelledby="empty-favorites">
          <StateArtwork kind="bookmark" />
          <h2 id="empty-favorites">{t('favorites.emptyTitle')}</h2>
          <p>{t('favorites.emptyDescription')}</p>
          <Link ref={emptyLink} className="button button-primary" to="/catalog">
            {t('favorites.browse')}
          </Link>
        </section>
      ) : (
        <>
          {state.status === 'loading' && (
            <p role="status">
              <LoadingMark />
              {t('favorites.loading')}
            </p>
          )}
          {state.status === 'error' && (
            <section className={styles.state} aria-labelledby="favorites-error">
              <StateArtwork kind="error" />
              <h2 id="favorites-error">{t('favorites.errorTitle')}</h2>
              <p role="status">{t('favorites.errorDescription')}</p>
              <button
                type="button"
                className="button button-primary"
                onClick={() => setAttempt((value) => value + 1)}
              >
                {t('catalog.retry')}
              </button>
            </section>
          )}
          {state.status === 'success' && (
            <ul
              ref={list}
              className={catalogStyles.results}
              aria-label={t('favorites.listLabel')}
              onClickCapture={(event) => {
                const button = (
                  event.target as HTMLElement
                ).closest<HTMLButtonElement>('button[data-favorite-id]')
                if (!button) return
                const index = items.findIndex(
                  (item) => item.medication.id === button.dataset.favoriteId,
                )
                pendingFocus.current =
                  items[index + 1]?.medication.id ??
                  items[index - 1]?.medication.id ??
                  ''
              }}
            >
              {items.map((item) => (
                <li key={item.medication.id}>
                  <MedicationCard item={item} />
                </li>
              ))}
            </ul>
          )}
        </>
      )}
      {!empty && state.status === 'success' && (
        <p className={catalogStyles.artNote}>{t('visual.artNote')}</p>
      )}
    </div>
  )
}
