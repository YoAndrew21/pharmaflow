import styles from './LoadingMark.module.css'

/** Decorative read feedback; the adjacent status text carries the meaning. */
export function LoadingMark() {
  return (
    <span className={styles.mark} aria-hidden="true">
      <span />
      <span />
      <span />
    </span>
  )
}
