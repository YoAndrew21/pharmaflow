import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import styles from './ReadRetryButton.module.css'

/** Keep the same focused action during retry; no request state or focus moves. */
export function ReadRetryButton({
  isError,
  isLoading,
  onRetry,
}: {
  isError: boolean
  isLoading: boolean
  onRetry: () => void
}) {
  const { t } = useTranslation()
  const [visible, setVisible] = useState(isError)
  if (isError && !visible) setVisible(true)
  if (!isError && !isLoading && visible) setVisible(false)
  if (!visible) return null
  return (
    <div className={styles.action}>
      <button
        type="button"
        className="button button-primary"
        aria-disabled={isLoading || undefined}
        onClick={() => {
          if (!isLoading) onRetry()
        }}
      >
        {t('catalog.retry')}
      </button>
    </div>
  )
}
