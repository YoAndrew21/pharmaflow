import { useLayoutEffect, useState } from 'react'
import { ArrowLeft, Info } from 'lucide-react'
import { Link, useParams, useSearchParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import type { CatalogReader } from '../data/catalogReader'
import { runtimeCatalogReader } from '../data/runtimeCatalogReader'
import { buildBranchComparison } from '../domain/branchComparison'
import { BranchComparison } from '../features/availability/BranchComparison'
import { catalogReturnPath } from '../features/catalog/navigation'
import { useMedicationDetail } from '../features/medication/useMedicationDetail'
import styles from './MedicationDetailPage.module.css'
import { FavoriteButton } from '../features/favorites/FavoriteButton'
import { PharmaArtwork } from '../components/PharmaArtwork'
import { StateArtwork } from '../components/StateArtwork'
import { LoadingMark } from '../components/LoadingMark'

export function MedicationDetailPage({
  reader = runtimeCatalogReader,
}: {
  reader?: CatalogReader
}) {
  const { slug = '' } = useParams()
  const [params] = useSearchParams()
  const [attempt, setAttempt] = useState(0)
  const { t } = useTranslation()
  const state = useMedicationDetail(reader, slug, attempt)
  const detail = state.status === 'success' ? state.data : null
  const medication = detail?.medication
  const title = medication
    ? `${medication.displayName} · ${medication.strengthLabel} — ${t('detail.pageTitle')}`
    : t(
        state.status === 'success'
          ? 'detail.notFoundTitle'
          : state.status === 'error'
            ? 'detail.errorTitle'
            : 'detail.pageTitle',
      )
  useLayoutEffect(() => {
    document.title = `${title} | ${t('brand')}`
  }, [title, t])
  return (
    <div className={`container ${styles.page}`}>
      <Link className={styles.back} to={catalogReturnPath(params)}>
        <ArrowLeft size={18} aria-hidden="true" />
        {t('detail.back')}
      </Link>
      {state.status === 'loading' && (
        <>
          <h1>{t('detail.pageTitle')}</h1>
          <p role="status">
            <LoadingMark />
            {t('detail.loading')}
          </p>
        </>
      )}
      {state.status === 'error' && (
        <section className={styles.state} aria-labelledby="detail-error">
          <StateArtwork kind="error" />
          <h1 id="detail-error">{t('detail.errorTitle')}</h1>
          <p role="status">{t('detail.errorDescription')}</p>
          <button
            className="button button-primary"
            type="button"
            onClick={() => setAttempt((value) => value + 1)}
          >
            {t('catalog.retry')}
          </button>
        </section>
      )}
      {state.status === 'success' && !detail && (
        <section className={styles.state} aria-labelledby="detail-not-found">
          <StateArtwork kind="missing" />
          <h1 id="detail-not-found">{t('detail.notFoundTitle')}</h1>
          <p>{t('detail.notFoundDescription')}</p>
        </section>
      )}
      {detail && medication && (
        <>
          <div className={styles.detailHero}>
            <figure className={styles.artwork}>
              <PharmaArtwork
                kind={medication.formCode}
                variant={medication.id}
              />
              <figcaption>{t('visual.artNote')}</figcaption>
            </figure>
            <div>
              <header className={styles.identity}>
                <p className="eyebrow">{t('detail.eyebrow')}</p>
                <h1>{medication.displayName}</h1>
                <p className={styles.presentation}>
                  {medication.strengthLabel} ·{' '}
                  {t(`forms.${medication.formCode}`, {
                    defaultValue: medication.formCode,
                  })}{' '}
                  ·{' '}
                  {t(`domain.packages.${medication.id}`, {
                    defaultValue: medication.presentationLabel,
                  })}
                </p>
                <FavoriteButton medication={medication} />
              </header>
              <section
                className={styles.catalogFacts}
                aria-labelledby="catalog-facts"
              >
                <h2 id="catalog-facts">{t('detail.factsTitle')}</h2>
                <dl>
                  <div>
                    <dt>{t('detail.ingredients')}</dt>
                    <dd>{medication.activeIngredients.join(' + ')}</dd>
                  </div>
                  <div>
                    <dt>{t('detail.strength')}</dt>
                    <dd>{medication.strengthLabel}</dd>
                  </div>
                  <div>
                    <dt>{t('catalog.form')}</dt>
                    <dd>
                      {t(`forms.${medication.formCode}`, {
                        defaultValue: medication.formCode,
                      })}
                    </dd>
                  </div>
                  <div>
                    <dt>{t('catalog.presentation')}</dt>
                    <dd>
                      {t(`domain.packages.${medication.id}`, {
                        defaultValue: medication.presentationLabel,
                      })}
                    </dd>
                  </div>
                  <div>
                    <dt>{t('detail.category')}</dt>
                    <dd>
                      {t(`categories.${medication.categoryCode}`, {
                        defaultValue: medication.categoryCode,
                      })}
                    </dd>
                  </div>
                  <div>
                    <dt>{t('detail.prescription')}</dt>
                    <dd>
                      {t(
                        medication.prescriptionRequired
                          ? 'detail.prescriptionRequired'
                          : 'detail.prescriptionNotRequired',
                      )}
                    </dd>
                  </div>
                </dl>
                <p className={styles.prescriptionNote}>
                  {t('detail.prescriptionNote')}
                </p>
              </section>
            </div>
          </div>
          <p className={styles.disclosure}>
            <Info size={18} aria-hidden="true" />
            <span>{t('detail.disclosure')}</span>
          </p>
          <BranchComparison
            rows={buildBranchComparison(detail.branches, detail.observations)}
          />
        </>
      )}
    </div>
  )
}
