import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest'
import { chooseOption, selectTrigger } from '../test/select'
import { App } from '../app/App'
import i18n from './index'
import { LOCALE_KEY } from '../features/preferences/locale'
import { resources } from './index'
import { localCatalogReader } from '../data/localCatalogReader'
import { installCanonicalFetch } from '../test/apiFetch'
import {
  formatDemoPrice,
  formatDemoTimestamp,
} from '../features/availability/formatters'

beforeEach(() => {
  installCanonicalFetch()
  localStorage.clear()
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => ({ matches: false })),
  )
  vi.stubGlobal('scrollTo', vi.fn())
})
afterEach(() => vi.unstubAllGlobals())

describe('language changes in the complete shell', () => {
  it.each(['en', 'es', 'ja'] as const)(
    '%s exposes localized Home/carousel controls and preserves normalized canonical search',
    async (locale) => {
      await act(() => i18n.changeLanguage(locale))
      window.history.replaceState(null, '', '/')
      const r = resources[locale].translation
      const view = render(<App />)
      const track = await screen.findByRole('list', {
        name: r.visual.discoveryTitle,
      })
      expect(document.documentElement.lang).toBe(locale)
      expect(document.title).toBe(`${r.nav.home} | PharmaFlow`)
      expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(
        r.visual.heroTitle,
      )
      expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(
        r.visual.heroAccent,
      )
      expect(track).toHaveAccessibleDescription(r.visual.carouselHelp)
      expect(
        screen.getByRole('button', { name: r.visual.previous }),
      ).toHaveAttribute('aria-controls', track.id)
      expect(
        screen.getByRole('button', { name: r.visual.next }),
      ).toHaveAttribute('aria-controls', track.id)
      expect(selectTrigger(r.language)).toHaveTextContent(
        { en: 'English', es: 'Español', ja: '日本語' }[locale],
      )
      for (const label of [r.nav.home, r.nav.catalog, r.nav.pharmacies]) {
        expect(
          within(
            screen.getByRole('navigation', { name: r.navigation }),
          ).getByRole('link', { name: label }),
        ).toBeInTheDocument()
      }
      const label = r.domain.favoriteLabels['med-par-500']
      const save = screen.getByRole('button', {
        name: i18n.t('favorites.saveLabel', { label }),
      })
      await userEvent.click(save)
      expect(save).toHaveAttribute('aria-pressed', 'true')
      expect(save).toHaveAccessibleName(
        i18n.t('favorites.removeLabel', { label }),
      )
      expect(
        view.container.querySelector('[aria-live="polite"]'),
      ).toHaveTextContent(i18n.t('favorites.announcements.saved', { label }))
      await userEvent.type(
        screen.getByRole('searchbox', { name: r.search.label }),
        'pÁrA{Enter}',
      )
      await screen.findByText(i18n.t('catalog.resultCount', { count: 3 }))
      expect(new URLSearchParams(window.location.search).get('q')).toBe('pÁrA')
      const results = screen.getByRole('list', { name: r.catalog.resultsLabel })
      expect(within(results).getAllByRole('article')).toHaveLength(3)
      for (const name of [
        'Paracetamol · 500 mg',
        'Paracetamol · 1000 mg',
        'Paracetamol + Caffeine · 500 mg / 50 mg',
      ]) {
        expect(within(results).getByRole('link', { name })).toBeInTheDocument()
      }
    },
  )

  it.each(['en', 'es', 'ja'] as const)(
    '%s renders localized stock, the same USD value and UTC instant, and distinct missing data',
    async (locale) => {
      await act(() => i18n.changeLanguage(locale))
      window.history.replaceState(
        null,
        '',
        '/catalog?q=para&branch=willow-arcade',
      )
      render(<App />)
      const card = await screen.findByRole('article', {
        name: 'Paracetamol · 500 mg',
      })
      const record = (
        await localCatalogReader.getAvailability('med-par-500')
      ).find((item) => item.branchId === 'willow-arcade')!
      expect(
        within(card).getByText(i18n.t('statuses.low-stock'), { exact: true }),
      ).toBeInTheDocument()
      expect(
        within(card).getByText(
          formatDemoPrice(record.referencePrice!, locale),
          { exact: true, normalizer: (text) => text },
        ),
      ).toBeInTheDocument()
      expect(card.querySelector('time')).toHaveAttribute(
        'datetime',
        record.updatedAt,
      )
      expect(card.querySelector('time')).toHaveTextContent(
        formatDemoTimestamp(record.updatedAt, locale),
      )
      const missing = screen.getByRole('article', {
        name: 'Paracetamol + Caffeine · 500 mg / 50 mg',
      })
      expect(
        within(missing).getByText(i18n.t('statuses.unknown'), { exact: true }),
      ).toBeInTheDocument()
      expect(
        within(missing).getByText(i18n.t('catalog.noPrice'), { exact: true }),
      ).toBeInTheDocument()
      expect(
        within(missing).queryByText(i18n.t('statuses.unavailable'), {
          exact: true,
        }),
      ).not.toBeInTheDocument()
      expect(missing.querySelector('time')).not.toBeInTheDocument()
    },
  )

  it.each(['en', 'es', 'ja'] as const)(
    '%s localizes branch title context, weekdays and closed days without changing the branch link',
    async (locale) => {
      await act(() => i18n.changeLanguage(locale))
      window.history.replaceState(null, '', '/pharmacies/aster-commons')
      render(<App />)
      await screen.findByRole('heading', { name: 'Aster Commons', level: 1 })
      expect(document.title).toBe(
        `Aster Commons — ${i18n.t('pharmacies.detailTitle')} | PharmaFlow`,
      )
      const hours = screen.getByRole('region', {
        name: i18n.t('pharmacies.hours'),
      })
      expect(
        within(hours).getByText(
          { en: 'Monday', es: 'lunes', ja: '月曜日' }[locale],
        ),
      ).toBeInTheDocument()
      const sunday = within(hours).getByText(
        { en: 'Sunday', es: 'domingo', ja: '日曜日' }[locale],
      ).parentElement!
      expect(
        within(sunday).getByText(i18n.t('pharmacies.closed')),
      ).toBeInTheDocument()
      expect(hours.querySelectorAll('dt')).toHaveLength(7)
      expect(
        screen.getByRole('link', { name: i18n.t('pharmacies.browse') }),
      ).toHaveAttribute('href', '/catalog?branch=aster-commons')
    },
  )
  it.each([
    ['es', 'Catálogo de medicamentos', 'Stock bajo', 'Guardar', 'Quitar'],
    ['ja', '医薬品カタログ', '在庫わずか', '保存', '削除'],
  ] as const)(
    'switches to %s without losing query, bookmarks, theme or keyboard focus',
    async (locale, title, status, save, remove) => {
      const query =
        '/catalog?q=para&branch=aster-commons&availability=low-stock&form=tablet&sort=price'
      window.history.replaceState(null, '', query)
      localStorage.setItem('pharmaflow:theme', 'dark')
      const bookmarks = '{"version":1,"medicationIds":["med-par-500"]}'
      localStorage.setItem('pharmaflow:favorites', bookmarks)
      const view = render(<App />)
      await screen.findByText('1 presentation found')
      const selector = selectTrigger('Language')
      selector.focus()
      await chooseOption(
        selector,
        { en: 'English', es: 'Español', ja: '日本語' }[locale],
      )
      await screen.findByRole('heading', { name: title, level: 1 })
      expect(document.documentElement.lang).toBe(locale)
      expect(document.title).toBe(`${title} | PharmaFlow`)
      expect(window.location.pathname + window.location.search).toBe(query)
      expect(localStorage.getItem(LOCALE_KEY)).toBe(locale)
      expect(localStorage.getItem('pharmaflow:favorites')).toBe(bookmarks)
      expect(document.documentElement.dataset.theme).toBe('dark')
      expect(selector).toHaveFocus()
      const card = screen.getByRole('article', {
        name: 'Paracetamol · 1000 mg',
      })
      expect(
        within(card).getByText(status, { exact: true }),
      ).toBeInTheDocument()
      const button = within(card).getByRole('button')
      expect(button).toHaveTextContent(save)
      expect(button).toHaveAttribute('aria-pressed', 'false')
      expect(button.getAttribute('aria-label')).toContain(button.textContent)
      await userEvent.click(button)
      expect(button).toHaveTextContent(remove)
      expect(button).toHaveAttribute('aria-pressed', 'true')
      expect(button.getAttribute('aria-label')).toContain(button.textContent)
      expect(
        view.container.querySelector('div[aria-live="polite"]'),
      ).toHaveTextContent(i18n.t('domain.packages.med-par-1000'))
      await chooseOption(selector, 'English')
      expect(
        view.container.querySelector('div[aria-live="polite"]'),
      ).toHaveTextContent('Demo pack · 10 tablets')
      expect(window.location.pathname + window.location.search).toBe(query)
      await userEvent.click(within(card).getByRole('link'))
      await screen.findByRole('heading', { level: 1, name: 'Paracetamol' })
      await chooseOption(
        selectTrigger('Language'),
        { en: 'English', es: 'Español', ja: '日本語' }[locale],
      )
      await waitFor(() =>
        expect(document.title).toBe(
          `Paracetamol · 1000 mg — ${i18n.t('detail.pageTitle')} | PharmaFlow`,
        ),
      )
    },
  )
  it.each([
    ['es', 'Medicamento no encontrado'],
    ['ja', '医薬品が見つかりません'],
  ] as const)(
    'localizes resolved not-found titles in %s',
    async (locale, title) => {
      window.history.replaceState(null, '', '/medications/unknown')
      render(<App />)
      await screen.findByRole('heading', {
        level: 1,
        name: 'Medication not found',
      })
      await chooseOption(
        selectTrigger('Language'),
        { en: 'English', es: 'Español', ja: '日本語' }[locale],
      )
      await waitFor(() => expect(document.title).toBe(`${title} | PharmaFlow`))
      expect(
        screen.getByRole('heading', { level: 1, name: title }),
      ).toBeInTheDocument()
    },
  )
  it('keeps language switching usable after a preference write failure', async () => {
    window.history.replaceState(null, '', '/')
    render(<App />)
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('quota')
    })
    try {
      await chooseOption(selectTrigger('Language'), '日本語')
      expect(document.documentElement.lang).toBe('ja')
      expect(selectTrigger('言語')).toHaveTextContent('日本語')
      await act(() => i18n.changeLanguage('en'))
    } finally {
      vi.restoreAllMocks()
    }
  })
})
