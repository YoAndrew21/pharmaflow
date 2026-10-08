import { createInstance } from 'i18next'
import ts from 'typescript'
import { describe, expect, it } from 'vitest'
import { resources } from './index'
import { localCatalogReader } from '../data/localCatalogReader'
import {
  formatDemoPrice,
  formatDemoTimestamp,
} from '../features/availability/formatters'
import { formatWeeklyHours } from '../features/pharmacies/scheduleFormat'

function leaves(value: object, prefix = ''): Record<string, string> {
  return Object.fromEntries(
    Object.entries(value).flatMap(([key, entry]) => {
      const path = prefix ? `${prefix}.${key}` : key
      return typeof entry === 'string'
        ? [[path, entry]]
        : Object.entries(leaves(entry, path))
    }),
  )
}
const placeholders = (value: string) =>
  [...value.matchAll(/{{\s*([^}]+)\s*}}/g)].map((match) => match[1]).sort()

describe('complete authored locale resources', () => {
  it('covers static UI/accessibility message references in every supported locale', () => {
    const keys = new Set<string>()
    function readArgument(node: ts.Node) {
      if (ts.isStringLiteral(node)) keys.add(node.text)
      else if (ts.isConditionalExpression(node)) {
        readArgument(node.whenTrue)
        readArgument(node.whenFalse)
      } else if (ts.isBinaryExpression(node)) {
        readArgument(node.left)
        readArgument(node.right)
      }
    }
    const sources = import.meta.glob<string>('../**/*.tsx', {
      query: '?raw',
      import: 'default',
      eager: true,
    })
    for (const [path, text] of Object.entries(sources)) {
      if (!path.endsWith('.test.tsx')) {
        const source = ts.createSourceFile(
          path,
          text,
          ts.ScriptTarget.Latest,
          true,
          ts.ScriptKind.TSX,
        )
        function visit(node: ts.Node) {
          if (
            ts.isCallExpression(node) &&
            ts.isIdentifier(node.expression) &&
            node.expression.text === 't' &&
            node.arguments[0]
          )
            readArgument(node.arguments[0])
          ts.forEachChild(node, visit)
        }
        visit(source)
      }
    }
    expect(keys.size).toBeGreaterThan(0)
    for (const [locale, resource] of Object.entries(resources)) {
      const messages = leaves(resource.translation)
      for (const key of keys) {
        if (messages[key] !== undefined) continue
        expect(messages[`${key}_one`], `${locale}: ${key}_one`).toBeDefined()
        expect(
          messages[`${key}_other`],
          `${locale}: ${key}_other`,
        ).toBeDefined()
      }
    }
  })
  it.each(['en', 'es', 'ja'] as const)(
    '%s has every message and interpolation variable',
    (locale) => {
      const english = leaves(resources.en.translation)
      const actual = leaves(resources[locale].translation)
      expect(Object.keys(actual).sort()).toEqual(Object.keys(english).sort())
      for (const [key, value] of Object.entries(actual)) {
        expect(value.trim(), key).not.toBe('')
        expect(placeholders(value), key).toEqual(placeholders(english[key]!))
      }
    },
  )
  it.each(['es', 'ja'] as const)(
    '%s does not leave English interface messages untranslated',
    (locale) => {
      const english = leaves(resources.en.translation)
      for (const [key, value] of Object.entries(
        leaves(resources[locale].translation),
      )) {
        if (key === 'brand') continue
        expect(value, key).not.toBe(english[key])
      }
    },
  )
  it('falls back to English for unsupported locales and missing messages', async () => {
    const instance = createInstance()
    await instance.init({
      resources: {
        ...resources,
        en: {
          translation: {
            ...resources.en.translation,
            fallbackProbe: 'English fallback',
          },
        },
      },
      lng: 'fr',
      supportedLngs: ['en', 'es', 'ja'],
      fallbackLng: 'en',
    })
    expect(instance.t('catalog.title')).toBe('Medication catalog')
    await instance.changeLanguage('ja')
    expect(instance.t('fallbackProbe')).toBe('English fallback')
  })
  it('covers every canonical package, location and presentation-specific favorite label', async () => {
    const medications = await localCatalogReader.listMedications()
    const branches = await localCatalogReader.listBranches()
    for (const resource of Object.values(resources)) {
      expect(Object.keys(resource.translation.domain.packages).sort()).toEqual(
        medications.map((med) => med.id).sort(),
      )
      expect(Object.keys(resource.translation.domain.locations).sort()).toEqual(
        branches.map((branch) => branch.id).sort(),
      )
      for (const med of medications) {
        const label = leaves(resource.translation)[
          `domain.favoriteLabels.${med.id}`
        ]!
        expect(label).toContain(med.displayName)
        expect(label).toContain(med.strengthLabel)
        expect(label).toContain(
          leaves(resource.translation)[`domain.packages.${med.id}`],
        )
      }
    }
  })
  it.each(['en', 'es', 'ja'] as const)(
    '%s formats the same USD price, UTC instant and fixed weekly schedule',
    async (locale) => {
      const price = { minorUnits: 320, currencyCode: 'USD' }
      const formatted = formatDemoPrice(price, locale)
      expect(formatted).toContain('USD')
      expect(formatted).toMatch(locale === 'es' ? /3,20/ : /3\.20/)
      expect(price).toEqual({ minorUnits: 320, currencyCode: 'USD' })
      const stamp = formatDemoTimestamp('2026-09-01T10:00:00Z', locale)
      expect(stamp).toContain('2026')
      expect(stamp).toContain('UTC')
      expect(stamp).toContain('10:00')
      const branch = (await localCatalogReader.listBranches())[0]!
      const hours = formatWeeklyHours(branch.openingHours, locale)
      expect(hours).toHaveLength(7)
      expect(hours[0]?.label).toBe(
        { en: 'Monday', es: 'lunes', ja: '月曜日' }[locale],
      )
      expect(hours[6]?.intervals).toEqual([])
      expect(hours[0]?.intervals[0]?.opensAt).toBe('09:00')
      expect(hours[0]?.intervals[0]?.closesAt).toBe('17:00')
    },
  )
})
