import { useMemo, useRef, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import { Select } from '../components/Select'
import { SearchField } from '../components/SearchField'
import type { CatalogReader } from '../data/catalogReader'
import { runtimeCatalogReader } from '../data/runtimeCatalogReader'
import { availabilityStatuses, normalizeCatalogQuery } from '../domain/catalog'
import {
  parseCatalogQuery,
  serializeCatalogQuery,
} from '../features/catalog/queryParams'
import { useCatalog } from '../features/catalog/useCatalog'
import { MedicationCard } from '../features/catalog/MedicationCard'
import styles from '../features/catalog/Catalog.module.css'
import { StateArtwork } from '../components/StateArtwork'
import { LoadingMark } from '../components/LoadingMark'

export function CatalogPage({
  reader = runtimeCatalogReader,
}: {
  reader?: CatalogReader
}) {
  const { t, i18n } = useTranslation()
  const [params, setParams] = useSearchParams()
  const search = params.toString()
  const query = useMemo(
    () => parseCatalogQuery(new URLSearchParams(search)),
    [search],
  )
  const [attempt, setAttempt] = useState(0)
  const { state, metadata } = useCatalog(reader, query, `${search}:${attempt}`)
  const formRef = useRef<HTMLFormElement>(null)
  type SelectField = 'branch' | 'availability' | 'form' | 'sort'
  const pendingSelection = useRef<{ name: SelectField; value: string } | null>(
    null,
  )
  const resetting = useRef(false)
  const reset = () => {
    pendingSelection.current = null
    resetting.current = true
    try {
      // React Aria resets named controls too; those callbacks must not submit.
      formRef.current?.reset()
      setParams(new URLSearchParams())
    } finally {
      resetting.current = false
    }
  }
  function select(name: SelectField, value: string) {
    if (resetting.current) return
    pendingSelection.current = { name, value }
    try {
      formRef.current?.requestSubmit()
    } finally {
      pendingSelection.current = null
    }
  }
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const fields = new FormData(event.currentTarget)
    // The hidden native select may still reflect the previous React render.
    const committed = pendingSelection.current
    pendingSelection.current = null
    if (committed) fields.set(committed.name, committed.value)
    const next = new URLSearchParams()
    for (const key of ['q', 'branch', 'availability', 'form', 'sort'])
      next.set(key, String(fields.get(key) ?? ''))
    const canonical = serializeCatalogQuery(parseCatalogQuery(next))
    if (canonical.toString() !== search) setParams(canonical)
  }
  const result = state.status === 'success' ? state.data : null
  const effectiveQuery = metadata
    ? normalizeCatalogQuery(query, metadata.branches, metadata.forms)
    : query
  const branch = metadata?.branches.find((b) => b.id === effectiveQuery.branch)
  return (
    <div className={`container ${styles.page}`}>
      <header className={styles.intro}>
        <p className="eyebrow">{t('catalog.eyebrow')}</p>
        <h1>{t('catalog.title')}</h1>
        <p>{t('catalog.description')}</p>
      </header>
      {metadata && (
        <>
          <p className={styles.snapshot}>
            {t('catalog.snapshot')}{' '}
            <time dateTime={metadata.snapshotAt}>
              {new Intl.DateTimeFormat(i18n.language, {
                dateStyle: 'medium',
                timeStyle: 'short',
                timeZone: 'UTC',
              }).format(new Date(metadata.snapshotAt))}{' '}
              UTC
            </time>{' '}
            · {t('catalog.notLive')}
          </p>
          <form
            ref={formRef}
            role="search"
            aria-label={t('search.catalogRegion')}
            className={styles.controls}
            onSubmit={submit}
          >
            <div className={styles.searchRow}>
              <SearchField defaultValue={effectiveQuery.q} />
              <button type="submit" className="button button-primary">
                {t('search.submit')}
              </button>
            </div>
            <div className={styles.filters}>
              <Select
                label={t('catalog.branch')}
                name="branch"
                value={effectiveQuery.branch}
                onChange={(value) => select('branch', value)}
                options={[
                  { value: '', label: t('catalog.allBranches') },
                  ...metadata.branches.map((b) => ({
                    value: b.id,
                    label: b.name,
                  })),
                ]}
              />
              <Select
                label={t('catalog.availability')}
                name="availability"
                value={effectiveQuery.availability}
                onChange={(value) => select('availability', value)}
                options={[
                  { value: '', label: t('catalog.allStatuses') },
                  ...availabilityStatuses.map((status) => ({
                    value: status,
                    label: t(`statuses.${status}`),
                  })),
                ]}
              />
              <Select
                label={t('catalog.form')}
                name="form"
                value={effectiveQuery.form}
                onChange={(value) => select('form', value)}
                options={[
                  { value: '', label: t('catalog.allForms') },
                  ...metadata.forms.map((form) => ({
                    value: form,
                    label: t(`forms.${form}`, { defaultValue: form }),
                  })),
                ]}
              />
              <Select
                label={t('catalog.sort')}
                name="sort"
                value={effectiveQuery.sort}
                onChange={(value) => select('sort', value)}
                options={[
                  { value: 'name', label: t('catalog.sortName') },
                  { value: 'price', label: t('catalog.sortPrice') },
                ]}
              />
            </div>
            <div className={styles.controlFooter}>
              <p>
                {branch
                  ? t('catalog.branchHint', { branch: branch.name })
                  : t('catalog.anyHint')}
              </p>
              <button
                className="button button-secondary"
                type="button"
                onClick={reset}
              >
                {t('catalog.clear')}
              </button>
            </div>
          </form>
        </>
      )}
      <p
        role="status"
        aria-live="polite"
        aria-atomic="true"
        className={styles.count}
      >
        {state.status === 'loading' && <LoadingMark />}
        {state.status === 'loading'
          ? t('catalog.loading')
          : result
            ? t('catalog.resultCount', { count: result.items.length })
            : t('catalog.readFailed')}
      </p>
      {state.status === 'error' && (
        <section className={styles.state} aria-labelledby="error-title">
          <StateArtwork kind="error" />
          <h2 id="error-title">{t('catalog.errorTitle')}</h2>
          <p>{t('catalog.errorDescription')}</p>
          <div className={styles.stateActions}>
            <button
              className="button button-primary"
              type="button"
              onClick={() => setAttempt((value) => value + 1)}
            >
              {t('catalog.retry')}
            </button>
            <button
              className="button button-secondary"
              type="button"
              onClick={reset}
            >
              {t('catalog.clear')}
            </button>
          </div>
        </section>
      )}
      {result &&
        (result.items.length ? (
          <ul className={styles.results} aria-label={t('catalog.resultsLabel')}>
            {result.items.map((item) => (
              <li key={item.medication.id}>
                <MedicationCard item={item} branch={branch} />
              </li>
            ))}
          </ul>
        ) : (
          <section className={styles.state} aria-labelledby="empty-title">
            <StateArtwork />
            <h2 id="empty-title">{t('catalog.emptyTitle')}</h2>
            <p>{t('catalog.emptyDescription')}</p>
            <button
              className="button button-secondary"
              type="button"
              onClick={reset}
            >
              {t('catalog.clear')}
            </button>
          </section>
        ))}
      {result && result.items.length > 0 && (
        <p className={styles.artNote}>{t('visual.artNote')}</p>
      )}
    </div>
  )
}
