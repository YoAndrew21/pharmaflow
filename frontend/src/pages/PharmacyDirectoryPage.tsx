import { useState } from 'react'
import { Link } from 'react-router'
import { useTranslation } from 'react-i18next'
import type { CatalogReader } from '../data/catalogReader'
import { localCatalogReader } from '../data/localCatalogReader'
import { OpeningHours } from '../features/pharmacies/OpeningHours'
import { useBranchDirectory } from '../features/pharmacies/useBranches'
import styles from '../features/pharmacies/Pharmacies.module.css'
import { PharmaArtwork } from '../components/PharmaArtwork'
import { StateArtwork } from '../components/StateArtwork'
import { LoadingMark } from '../components/LoadingMark'

export function PharmacyDirectoryPage({
  reader = localCatalogReader,
}: {
  reader?: CatalogReader
}) {
  const { t } = useTranslation()
  const [attempt, setAttempt] = useState(0)
  const state = useBranchDirectory(reader, attempt)
  return (
    <div className={`container ${styles.page}`}>
      <p className="eyebrow">{t('pharmacies.eyebrow')}</p>
      <h1>{t('pharmacies.title')}</h1>
      <p className={styles.description}>{t('pharmacies.description')}</p>
      {state.status === 'loading' && (
        <p role="status">
          <LoadingMark />
          {t('pharmacies.loadingDirectory')}
        </p>
      )}
      {state.status === 'error' && (
        <section className={styles.state} aria-labelledby="directory-error">
          <StateArtwork kind="error" />
          <h2 id="directory-error">{t('pharmacies.directoryError')}</h2>
          <p role="status">{t('pharmacies.readError')}</p>
          <button
            type="button"
            className="button button-primary"
            onClick={() => setAttempt((value) => value + 1)}
          >
            {t('catalog.retry')}
          </button>
        </section>
      )}
      {state.status === 'success' &&
        (state.data.length ? (
          <ul
            className={styles.directory}
            aria-label={t('pharmacies.listLabel')}
          >
            {state.data.map((branch) => (
              <li key={branch.id}>
                <article
                  className={styles.card}
                  aria-labelledby={`directory-${branch.id}`}
                >
                  <div className={styles.cardArtwork}>
                    <PharmaArtwork kind="branch" variant={branch.id} />
                  </div>
                  <p className={styles.indicator}>
                    {t('pharmacies.fictional')}
                  </p>
                  <h2 id={`directory-${branch.id}`}>
                    <Link
                      className={styles.branchLink}
                      to={`/pharmacies/${encodeURIComponent(branch.slug)}`}
                    >
                      {branch.name}
                    </Link>
                  </h2>
                  <p className={styles.location}>
                    {t(`domain.locations.${branch.id}`, {
                      defaultValue: branch.fictionalLocation,
                    })}
                  </p>
                  <OpeningHours branch={branch} headingLevel={3} />
                </article>
              </li>
            ))}
          </ul>
        ) : (
          <p role="status">{t('pharmacies.empty')}</p>
        ))}
    </div>
  )
}
