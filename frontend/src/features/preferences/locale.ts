export const locales = ['en', 'es', 'ja'] as const
export type Locale = (typeof locales)[number]
export const LOCALE_KEY = 'pharmaflow:locale'

export function isLocale(value: unknown): value is Locale {
  return locales.some((locale) => locale === value)
}

export function readLocale(): Locale {
  try {
    const stored = localStorage.getItem(LOCALE_KEY)
    return isLocale(stored) ? stored : 'en'
  } catch {
    return 'en'
  }
}

export function persistLocale(locale: Locale) {
  try {
    localStorage.setItem(LOCALE_KEY, locale)
  } catch {
    // The selected language still works in memory when storage is restricted.
  }
}
