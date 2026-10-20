import styles from './Skeleton.module.css'

/** Geometry only: no content, focus targets, animation, or request state. */
export function Skeleton({
  kind = 'line',
  className = '',
}: {
  kind?: 'line' | 'title' | 'media' | 'control'
  className?: string
}) {
  return (
    <span
      aria-hidden="true"
      data-skeleton={kind}
      className={`${styles.skeleton} ${styles[kind]} ${className}`}
    />
  )
}
