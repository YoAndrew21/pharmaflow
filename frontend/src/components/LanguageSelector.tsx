import { useTranslation } from 'react-i18next'
import { isLocale, persistLocale } from '../features/preferences/locale'
import styles from './LanguageSelector.module.css'

export function LanguageSelector() {
  const { t, i18n } = useTranslation()
  return (
    <label className={styles.control}>
      <span className={styles.label}>{t('language')}</span>
      <select
        value={i18n.resolvedLanguage ?? 'en'}
        onChange={(event) => {
          const locale = event.currentTarget.value
          if (!isLocale(locale)) return
          void i18n.changeLanguage(locale)
          persistLocale(locale)
        }}
      >
        <option value="en" lang="en">
          English
        </option>
        <option value="es" lang="es">
          Español
        </option>
        <option value="ja" lang="ja">
          日本語
        </option>
      </select>
    </label>
  )
}
