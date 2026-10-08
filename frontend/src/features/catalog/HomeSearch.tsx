import { useNavigate } from 'react-router'
import { useTranslation } from 'react-i18next'
import { SearchField } from '../../components/SearchField'
import styles from './Catalog.module.css'

export function HomeSearch() {
  const navigate = useNavigate()
  const { t } = useTranslation()
  return (
    <form
      role="search"
      aria-label={t('search.homeRegion')}
      className={styles.homeSearch}
      onSubmit={(event) => {
        event.preventDefault()
        const q = String(
          new FormData(event.currentTarget).get('q') ?? '',
        ).trim()
        const search = new URLSearchParams()
        if (q) search.set('q', q)
        navigate({ pathname: '/catalog', search: search.toString() })
      }}
    >
      <SearchField />
      <button className="button button-primary" type="submit">
        {t('search.submit')}
      </button>
    </form>
  )
}
