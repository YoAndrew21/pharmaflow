import { useState } from 'react'
import {
  ArrowRight,
  Bookmark,
  GitCompareArrows,
  ScanSearch,
  ShieldCheck,
  MapPin,
} from 'lucide-react'
import { Link } from 'react-router'
import { useTranslation } from 'react-i18next'
import styles from './HomePage.module.css'
import { HomeSearch } from '../features/catalog/HomeSearch'
import { PharmaArtwork } from '../components/PharmaArtwork'
import { HeroArtwork } from '../components/HeroArtwork'
import { StateArtwork } from '../components/StateArtwork'
import { MedicationCardSkeleton } from '../components/MedicationCardSkeleton'
import { ReadRetryButton } from '../components/ReadRetryButton'
import { LoadingMark } from '../components/LoadingMark'
import { DiscoveryCarousel } from '../components/DiscoveryCarousel'
import { MedicationCard } from '../features/catalog/MedicationCard'
import { useCatalog } from '../features/catalog/useCatalog'
import { runtimeCatalogReader } from '../data/runtimeCatalogReader'
import type { CatalogReader } from '../data/catalogReader'
import { defaultCatalogQuery } from '../domain/catalog'
import { serializeCatalogQuery } from '../features/catalog/queryParams'

export function HomePage({
  reader = runtimeCatalogReader,
}: {
  reader?: CatalogReader
}) {
  const { t } = useTranslation()
  const [attempt, setAttempt] = useState(0)
  const { state } = useCatalog(reader, defaultCatalogQuery, `home-${attempt}`)
  const data = state.status === 'success' ? state.data : null
  return (
    <div className="container">
      <section className={styles.hero} aria-labelledby="home-title">
        <div className={styles.heroContent}>
          <p className="eyebrow">
            <span className={styles.signal} />
            {t('visual.heroEyebrow')}
          </p>
          <h1 id="home-title" className={styles.title}>
            {t('visual.heroTitle')}
            <span>{t('visual.heroAccent')}</span>
          </h1>
          <p className={styles.description}>{t('home.description')}</p>
          <div className={styles.actions}>
            <Link className="button button-primary" to="/catalog">
              {t('home.primary')}
              <ArrowRight size={18} aria-hidden="true" />
            </Link>
            <Link className="button button-ghost" to="/pharmacies">
              {t('home.secondary')}
            </Link>
          </div>
          <HomeSearch />
        </div>
        <div className={styles.heroVisual}>
          <HeroArtwork />
          <div className={`${styles.callout} ${styles.calloutSearch}`}>
            <ScanSearch size={19} aria-hidden="true" />
            <span>{t('visual.searchCallout')}</span>
          </div>
          <div className={`${styles.callout} ${styles.calloutCompare}`}>
            <GitCompareArrows size={19} aria-hidden="true" />
            <span>{t('home.branchesTitle')}</span>
          </div>
          <div className={`${styles.callout} ${styles.calloutSave}`}>
            <Bookmark size={18} aria-hidden="true" />
            <span>{t('visual.saveCallout')}</span>
          </div>
          <p className={styles.visualCaption}>{t('visual.flowCaption')}</p>
        </div>
      </section>
      <div className={styles.demoStrip}>
        <ShieldCheck size={22} aria-hidden="true" />
        <strong>{t('home.phase')}</strong>
        <p>{t('visual.demoDescription')}</p>
      </div>
      {state.status === 'loading' && (
        <p className={styles.pendingStatus} role="status">
          <LoadingMark />
          {t('catalog.loading')}
        </p>
      )}
      <div
        aria-busy={state.status === 'loading'}
        data-read-region="home-discovery"
      >
        {state.status === 'loading' && (
          <div
            aria-hidden="true"
            className={styles.pendingDiscovery}
            data-skeleton="home-discovery"
          >
            <div className={styles.pendingTrack}>
              <MedicationCardSkeleton />
              <MedicationCardSkeleton />
              <MedicationCardSkeleton />
            </div>
          </div>
        )}
        {state.status === 'error' && (
          <section
            className={styles.readState}
            aria-labelledby="home-read-error"
          >
            <StateArtwork kind="error" />
            <h2 id="home-read-error">{t('catalog.readFailed')}</h2>
            <p role="status">{t('catalog.errorDescription')}</p>
          </section>
        )}
        {data && (
          <>
            {data.items.length ? (
              <DiscoveryCarousel
                title={t('visual.discoveryTitle')}
                description={t('visual.discoveryDescription')}
                items={data.items.map((item) => ({
                  id: item.medication.id,
                  content: <MedicationCard item={item} headingLevel={3} />,
                }))}
              />
            ) : (
              <p className={styles.readState} role="status">
                {t('catalog.emptyTitle')}
              </p>
            )}
            {data.forms.length > 0 && (
              <section className={styles.section} aria-labelledby="forms-title">
                <div className={styles.sectionHeading}>
                  <div>
                    <p className="eyebrow">{t('visual.formsEyebrow')}</p>
                    <h2 id="forms-title">{t('visual.formsTitle')}</h2>
                  </div>
                  <Link className="button button-ghost" to="/catalog">
                    {t('visual.viewCatalog')}
                    <ArrowRight size={18} aria-hidden="true" />
                  </Link>
                </div>
                <div className={styles.forms}>
                  {data.forms.map((form) => (
                    <Link
                      key={form}
                      className={styles.formCard}
                      to={`/catalog?${serializeCatalogQuery({ ...defaultCatalogQuery, form })}`}
                    >
                      <PharmaArtwork kind={form} />
                      <span>
                        {t(`forms.${form}`, { defaultValue: form })}
                        <ArrowRight size={20} aria-hidden="true" />
                      </span>
                    </Link>
                  ))}
                </div>
                <p className={styles.artNote}>{t('visual.artNote')}</p>
              </section>
            )}
            {data.branches.length > 0 && (
              <section
                className={styles.section}
                aria-labelledby="home-branches"
              >
                <div className={styles.sectionHeading}>
                  <div>
                    <p className="eyebrow">{t('pharmacies.eyebrow')}</p>
                    <h2 id="home-branches">{t('visual.branchesTitle')}</h2>
                    <p>{t('home.branchesDescription')}</p>
                  </div>
                  <Link className="button button-ghost" to="/pharmacies">
                    {t('home.secondary')}
                    <ArrowRight size={18} aria-hidden="true" />
                  </Link>
                </div>
                <div className={styles.branches}>
                  {data.branches.map((branch) => (
                    <article key={branch.id} className={styles.branchCard}>
                      <PharmaArtwork kind="branch" variant={branch.id} />
                      <div className={styles.branchContent}>
                        <p className={styles.branchLabel}>
                          <MapPin size={14} aria-hidden="true" />
                          {t('pharmacies.fictional')}
                        </p>
                        <h3>
                          <Link
                            to={`/pharmacies/${encodeURIComponent(branch.slug)}`}
                          >
                            {branch.name}
                            <ArrowRight size={18} aria-hidden="true" />
                          </Link>
                        </h3>
                        <p>
                          {t(`domain.locations.${branch.id}`, {
                            defaultValue: branch.fictionalLocation,
                          })}
                        </p>
                      </div>
                    </article>
                  ))}
                </div>
              </section>
            )}
          </>
        )}
      </div>
      <ReadRetryButton
        isError={state.status === 'error'}
        isLoading={state.status === 'loading'}
        onRetry={() => setAttempt((value) => value + 1)}
      />
      <section className={styles.favorites} aria-labelledby="home-favorites">
        <div className={styles.bookmarkArt} aria-hidden="true">
          <Bookmark size={78} strokeWidth={1} />
          <span />
          <span />
        </div>
        <div>
          <p className="eyebrow">{t('visual.favoritesEyebrow')}</p>
          <h2 id="home-favorites">{t('visual.favoritesTitle')}</h2>
          <p>{t('home.favoritesDescription')}</p>
          <Link className="button button-secondary" to="/favorites">
            {t('visual.viewFavorites')}
            <ArrowRight size={18} aria-hidden="true" />
          </Link>
        </div>
      </section>
    </div>
  )
}
