import { useEffect, useId, useRef } from 'react'
import { Search } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import styles from './SearchField.module.css'

export function SearchField({ defaultValue = '' }: { defaultValue?: string }) {
  const id = useId()
  const input = useRef<HTMLInputElement>(null)
  useEffect(() => {
    if (input.current) input.current.value = defaultValue
  }, [defaultValue])
  const { t } = useTranslation()
  return (
    <div className={styles.field}>
      <label htmlFor={id}>{t('search.label')}</label>
      <div className={styles.input}>
        <Search size={20} aria-hidden="true" />
        <input
          ref={input}
          id={id}
          name="q"
          type="search"
          defaultValue={defaultValue}
          placeholder={t('search.placeholder')}
        />
      </div>
    </div>
  )
}
