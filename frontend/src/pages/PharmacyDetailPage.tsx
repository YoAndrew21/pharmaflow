import { useLayoutEffect, useState } from 'react'
import { ArrowLeft } from 'lucide-react'
import { Link, useParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import type { CatalogReader } from '../data/catalogReader'
import { runtimeCatalogReader } from '../data/runtimeCatalogReader'
import { OpeningHours } from '../features/pharmacies/OpeningHours'
import { useBranchDetail } from '../features/pharmacies/useBranches'
import { serializeCatalogQuery } from '../features/catalog/queryParams'
import { defaultCatalogQuery } from '../domain/catalog'
import styles from '../features/pharmacies/Pharmacies.module.css'
import { PharmaArtwork } from '../components/PharmaArtwork'
import { StateArtwork } from '../components/StateArtwork'
import { Skeleton } from '../components/Skeleton'
import { ReadRetryButton } from '../components/ReadRetryButton'
import { LoadingMark } from '../components/LoadingMark'

export function PharmacyDetailPage({
  reader = runtimeCatalogReader,
}: {
  reader?: CatalogReader
}) {
  const { slug = '' } = useParams()
  const { t } = useTranslation()
  const [attempt, setAttempt] = useState(0)
  const state = useBranchDetail(reader, slug, attempt)
  const branch = state.status === 'success' ? state.data : null
  const title =
    (branch ? `${branch.name} — ${t('pharmacies.detailTitle')}` : null) ??
    t(
      state.status === 'success'
        ? 'pharmacies.notFoundTitle'
        : state.status === 'error'
          ? 'pharmacies.detailError'
          : 'pharmacies.detailTitle',
    )
  useLayoutEffect(() => {
    document.title = `${title} | ${t('brand')}`
  }, [title, t])
  return (
    <div className={`container ${styles.page}`}>
      <Link to="/pharmacies" className={styles.back}>
        <ArrowLeft size={18} aria-hidden="true" />
        {t('pharmacies.back')}
      </Link>
      {state.status === 'loading' && <h1>{t('pharmacies.detailTitle')}</h1>}
      {state.status === 'loading' && (
        <p role="status">
          <LoadingMark />
          {t('pharmacies.loadingDetail')}
        </p>
      )}
      <div
        aria-busy={state.status === 'loading'}
        data-read-region="pharmacy-detail"
      >
        {state.status === 'loading' && (
          <div aria-hidden="true" data-skeleton="pharmacy-detail">
            <Skeleton kind="title" />
            <div className={styles.detailGrid}>
              <Skeleton kind="media" className={styles.pendingDetailVisual} />
              <div className={styles.pendingInformation}>
                <Skeleton kind="title" />
                <Skeleton />
                <Skeleton />
                <Skeleton />
                <Skeleton />
                <Skeleton />
                <Skeleton />
                <Skeleton />
              </div>
            </div>
          </div>
        )}
        {state.status === 'error' && (
          <section className={styles.state} aria-labelledby="branch-error">
            <StateArtwork kind="error" />
            <h1 id="branch-error">{t('pharmacies.detailError')}</h1>
            <p role="status">{t('pharmacies.readError')}</p>
          </section>
        )}
        {state.status === 'success' && !branch && (
          <section className={styles.state}>
            <StateArtwork kind="missing" />
            <h1>{t('pharmacies.notFoundTitle')}</h1>
            <p>{t('pharmacies.notFoundDescription')}</p>
          </section>
        )}
        {branch && (
          <>
            <p className="eyebrow">{t('pharmacies.fictional')}</p>
            <h1>{branch.name}</h1>
            <div className={styles.detailGrid}>
              <figure className={styles.detailArtwork}>
                <PharmaArtwork kind="branch" variant={branch.id} />
                <figcaption>{t('visual.artNote')}</figcaption>
              </figure>
              <section
                className={styles.information}
                aria-labelledby="branch-location"
              >
                <h2 id="branch-location">{t('pharmacies.location')}</h2>
                <p className={styles.location}>
                  {t(`domain.locations.${branch.id}`, {
                    defaultValue: branch.fictionalLocation,
                  })}
                </p>
                <OpeningHours branch={branch} />
              </section>
            </div>
            <p className={styles.disclosure}>{t('pharmacies.disclosure')}</p>
            <Link
              className="button button-primary"
              to={`/catalog?${serializeCatalogQuery({ ...defaultCatalogQuery, branch: branch.slug })}`}
            >
              {t('pharmacies.browse')}
            </Link>
          </>
        )}
      </div>
      <ReadRetryButton
        isError={state.status === 'error'}
        isLoading={state.status === 'loading'}
        onRetry={() => setAttempt((value) => value + 1)}
      />
    </div>
  )
}
