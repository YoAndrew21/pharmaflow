import { useTranslation } from 'react-i18next'
import type { PharmacyBranch } from '../../domain/models'
import { formatWeeklyHours } from './scheduleFormat'
import styles from './Pharmacies.module.css'

export function OpeningHours({
  branch,
  headingLevel = 2,
}: {
  branch: PharmacyBranch
  headingLevel?: 2 | 3
}) {
  const { t, i18n } = useTranslation()
  const Heading = headingLevel === 3 ? 'h3' : 'h2'
  return (
    <section aria-labelledby={`hours-${branch.id}`} className={styles.hours}>
      <Heading id={`hours-${branch.id}`}>{t('pharmacies.hours')}</Heading>
      <p className={styles.timeZone}>
        {t('pharmacies.timeZone', { timeZone: branch.timeZone })}
      </p>
      <dl className={styles.schedule}>
        {formatWeeklyHours(branch.openingHours, i18n.language).map(
          ({ day, label, intervals }) => (
            <div key={day}>
              <dt>{label}</dt>
              <dd>
                {intervals.length
                  ? intervals.map((interval) => (
                      <span
                        className={styles.interval}
                        key={`${interval.opensAt}-${interval.closesAt}`}
                      >
                        {interval.label}
                      </span>
                    ))
                  : t('pharmacies.closed')}
              </dd>
            </div>
          ),
        )}
      </dl>
    </section>
  )
}
