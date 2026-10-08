import type { ReferencePrice } from '../../domain/models'

export function formatDemoPrice(price: ReferencePrice, locale: string): string {
  return new Intl.NumberFormat(locale, {
    style: 'currency',
    currency: price.currencyCode,
    currencyDisplay: 'code',
  }).format(price.minorUnits / 100)
}

export function formatDemoTimestamp(instant: string, locale: string): string {
  return `${new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC' }).format(new Date(instant))} UTC`
}
