import { Link } from 'react-router'
import { useTranslation } from 'react-i18next'
import { StateArtwork } from '../components/StateArtwork'

export function NotFoundPage() {
  const { t } = useTranslation()
  return (
    <section className="container placeholder" aria-labelledby="page-title">
      <StateArtwork kind="missing" />
      <h1 id="page-title">{t('notFound.title')}</h1>
      <p>{t('notFound.description')}</p>
      <Link className="button button-primary" to="/">
        {t('notFound.back')}
      </Link>
    </section>
  )
}
