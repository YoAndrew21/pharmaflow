import { Skeleton } from './Skeleton'
import styles from './MedicationCardSkeleton.module.css'

/** Shared by the discovery carousel and catalog; never represents a record. */
export function MedicationCardSkeleton() {
  return (
    <div
      className={styles.card}
      aria-hidden="true"
      data-skeleton="medication-card"
    >
      <Skeleton kind="media" />
      <Skeleton kind="title" />
      <div className={styles.lines}>
        <Skeleton />
        <Skeleton className={styles.short} />
        <Skeleton />
        <Skeleton className={styles.short} />
      </div>
      <div className={styles.lines}>
        <Skeleton />
        <Skeleton className={styles.short} />
      </div>
    </div>
  )
}
