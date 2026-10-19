import { useEffect, useId, useRef, useState } from 'react'
import { Search, X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import styles from './SearchField.module.css'

export function SearchField({ defaultValue = '' }: { defaultValue?: string }) {
  const id = useId()
  const input = useRef<HTMLInputElement>(null)
  const [draft, setDraft] = useState({ defaultValue, text: defaultValue })
  // URL changes replace the committed value; ordinary renders keep the draft.
  if (draft.defaultValue !== defaultValue) {
    setDraft({ defaultValue, text: defaultValue })
  }
  useEffect(() => {
    const form = input.current?.form
    const reset = (event: Event) => {
      if (!event.defaultPrevented)
        setDraft({ defaultValue, text: defaultValue })
    }
    form?.addEventListener('reset', reset)
    return () => form?.removeEventListener('reset', reset)
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
          value={draft.text}
          onChange={(event) =>
            setDraft({ defaultValue, text: event.currentTarget.value })
          }
          placeholder={t('search.placeholder')}
        />
        <span className={styles.clearSlot}>
          {draft.text.length > 0 && (
            <button
              type="button"
              className={styles.clear}
              aria-label={t('search.clear')}
              onClick={() => {
                setDraft({ defaultValue, text: '' })
                input.current?.focus()
              }}
            >
              <X size={18} aria-hidden="true" />
            </button>
          )}
        </span>
      </div>
    </div>
  )
}
