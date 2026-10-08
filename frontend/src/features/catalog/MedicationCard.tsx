import { useTranslation } from 'react-i18next'
import { Link, useLocation } from 'react-router'
import type { CatalogItem } from '../../domain/catalog'
import type { PharmacyBranch } from '../../domain/models'
import { AvailabilityBadge } from './AvailabilityBadge'
import styles from './Catalog.module.css'
import { medicationPath } from './navigation'
import { FavoriteButton } from '../favorites/FavoriteButton'
import { PharmaArtwork } from '../../components/PharmaArtwork'

export function MedicationCard({
  item,
  branch,
  headingLevel = 2,
}: {
  item: CatalogItem
  branch?: PharmacyBranch
  headingLevel?: 2 | 3
}) {
  const Heading = headingLevel === 3 ? 'h3' : 'h2'
  const { t, i18n } = useTranslation()
  const { search } = useLocation()
  const {
    medication: med,
    summary,
    selectedAvailability: record,
    referencePrice,
  } = item
  const heading = `medication-${med.id}`
  const counts = [
    ['available', summary.available],
    ['low-stock', summary.lowStock],
    ['unavailable', summary.unavailable],
    ['unknown', summary.unknown],
  ] as const
  const price = referencePrice
    ? new Intl.NumberFormat(i18n.language, {
        style: 'currency',
        currency: referencePrice.currencyCode,
        currencyDisplay: 'code',
      }).format(referencePrice.minorUnits / 100)
    : null
  return (
    <article className={styles.card} aria-labelledby={heading}>
      <div className={styles.artwork}>
        <PharmaArtwork kind={med.formCode} variant={med.id} />
      </div>
      <Heading id={heading}>
        <Link
          to={medicationPath(med.slug, search)}
          className={styles.titleLink}
        >
          {med.displayName}{' '}
          <span className={styles.strength}>· {med.strengthLabel}</span>
        </Link>
      </Heading>
      <dl className={styles.facts}>
        <div>
          <dt>{t('catalog.ingredient')}</dt>
          <dd>{med.activeIngredients.join(' + ')}</dd>
        </div>
        <div>
          <dt>{t('catalog.formLabel')}</dt>
          <dd>{t(`forms.${med.formCode}`, { defaultValue: med.formCode })}</dd>
        </div>
        <div>
          <dt>{t('catalog.presentation')}</dt>
          <dd>
            {t(`domain.packages.${med.id}`, {
              defaultValue: med.presentationLabel,
            })}
          </dd>
        </div>
      </dl>
      <div className={styles.availability}>
        <p className={branch ? styles.branchLabel : styles.smallLabel}>
          {branch
            ? t('catalog.atBranch', { branch: branch.name })
            : t('catalog.acrossBranches')}
        </p>
        {branch ? (
          <AvailabilityBadge
            status={record?.status ?? 'unknown'}
            text={t(`statuses.${record?.status ?? 'unknown'}`)}
          />
        ) : (
          <ul className={styles.summary}>
            {counts
              .filter(([, count]) => count > 0)
              .map(([status, count]) => (
                <li key={status}>
                  <AvailabilityBadge
                    status={status}
                    text={t(`summaries.${status}`, {
                      count,
                      total: summary.total,
                    })}
                  />
                </li>
              ))}
          </ul>
        )}
        {branch && record && (
          <p className={styles.update}>
            {t('catalog.update')}{' '}
            <time dateTime={record.updatedAt}>
              {new Intl.DateTimeFormat(i18n.language, {
                dateStyle: 'medium',
                timeStyle: 'short',
                timeZone: 'UTC',
              }).format(new Date(record.updatedAt))}{' '}
              UTC
            </time>
          </p>
        )}
      </div>
      <div className={styles.price}>
        <span className={styles.smallLabel}>{t('catalog.priceLabel')}</span>
        <p className={!price ? styles.missingPrice : undefined}>
          {price
            ? branch
              ? price
              : t('catalog.fromPrice', { price })
            : t('catalog.noPrice')}
        </p>
      </div>
      <FavoriteButton medication={med} />
    </article>
  )
}
