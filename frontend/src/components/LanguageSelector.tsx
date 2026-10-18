import { useTranslation } from 'react-i18next'
import { isLocale, persistLocale } from '../features/preferences/locale'
import { Select } from './Select'
import styles from './LanguageSelector.module.css'

export function LanguageSelector() {
  const { t, i18n } = useTranslation()
  return (
    <div className={styles.control}>
      <Select
        variant="compact"
        label={t('language')}
        value={i18n.resolvedLanguage ?? 'en'}
        options={[
          { value: 'en', label: 'English', lang: 'en' },
          { value: 'es', label: 'Español', lang: 'es' },
          { value: 'ja', label: '日本語', lang: 'ja' },
        ]}
        onChange={(locale) => {
          if (!isLocale(locale)) return
          void i18n.changeLanguage(locale)
          persistLocale(locale)
        }}
      />
    </div>
  )
}
