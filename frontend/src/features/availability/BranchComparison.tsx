import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { BranchComparisonRow } from '../../domain/branchComparison'
import { AvailabilityBadge } from '../catalog/AvailabilityBadge'
import { formatDemoPrice, formatDemoTimestamp } from './formatters'
import styles from './BranchComparison.module.css'

/** One labeled list/definition structure: desktop comparison rows, mobile cards. */
export function BranchComparison({
  rows,
}: {
  rows: readonly BranchComparisonRow[]
}) {
  const { t, i18n } = useTranslation()
  return (
    <section aria-labelledby="comparison-title" className={styles.section}>
      <h2 id="comparison-title">{t('detail.comparisonTitle')}</h2>
      <p className={styles.description}>{t('detail.comparisonDescription')}</p>
      <ul className={styles.list} aria-label={t('detail.comparisonTitle')}>
        {rows.map(({ branch, observation }) => (
          <li key={branch.id}>
            <article
              className={styles.row}
              aria-labelledby={`branch-${branch.id}`}
            >
              <div className={styles.branch}>
                <h3 id={`branch-${branch.id}`}>
                  <Link
                    className={styles.branchLink}
                    to={`/pharmacies/${encodeURIComponent(branch.slug)}`}
                  >
                    {branch.name}
                  </Link>
                </h3>
                <p>{t('detail.fictionalBranch')}</p>
              </div>
              <dl className={styles.facts}>
                <div>
                  <dt>{t('catalog.availability')}</dt>
                  <dd>
                    <AvailabilityBadge
                      status={observation?.status ?? 'unknown'}
                      text={t(`statuses.${observation?.status ?? 'unknown'}`)}
                    />
                  </dd>
                </div>
                <div>
                  <dt>{t('detail.referencePrice')}</dt>
                  <dd
                    className={
                      observation?.referencePrice
                        ? styles.price
                        : styles.missing
                    }
                  >
                    {observation?.referencePrice
                      ? formatDemoPrice(
                          observation.referencePrice,
                          i18n.language,
                        )
                      : t('catalog.noPrice')}
                  </dd>
                </div>
                <div>
                  <dt>{t('detail.demoUpdate')}</dt>
                  <dd className={styles.timestamp}>
                    {observation ? (
                      <time dateTime={observation.updatedAt}>
                        {formatDemoTimestamp(
                          observation.updatedAt,
                          i18n.language,
                        )}
                      </time>
                    ) : (
                      t('detail.noUpdate')
                    )}
                  </dd>
                </div>
              </dl>
            </article>
          </li>
        ))}
      </ul>
      <p className={styles.note}>{t('detail.comparisonNote')}</p>
    </section>
  )
}
