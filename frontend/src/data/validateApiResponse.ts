import type {
  Availability,
  Medication,
  PharmacyBranch,
  ReferencePrice,
  Weekday,
} from '../domain/models'
import { ApiReadError } from './httpClient'

function invalid(): never {
  throw new ApiReadError('invalid-response')
}
function record(value: unknown): Record<string, unknown> {
  if (typeof value !== 'object' || value === null || Array.isArray(value))
    invalid()
  return value as Record<string, unknown>
}
function text(value: unknown): string {
  if (typeof value !== 'string' || value.trim().length === 0) invalid()
  return value
}
function boolean(value: unknown): boolean {
  if (typeof value !== 'boolean') invalid()
  return value
}
export function collection<T>(
  value: unknown,
  parse: (value: unknown) => T,
): readonly T[] {
  if (!Array.isArray(value)) invalid()
  return value.map(parse)
}
export function unique<T>(
  rows: readonly T[],
  key: (row: T) => string,
): readonly T[] {
  if (new Set(rows.map(key)).size !== rows.length) invalid()
  return rows
}
export function medication(value: unknown): Medication {
  const r = record(value)
  const activeIngredients = collection(r.activeIngredients, text)
  if (!activeIngredients.length) invalid()
  return {
    id: text(r.id),
    slug: text(r.slug),
    displayName: text(r.displayName),
    activeIngredients,
    strengthLabel: text(r.strengthLabel),
    formCode: text(r.formCode),
    presentationLabel: text(r.presentationLabel),
    categoryCode: text(r.categoryCode),
    prescriptionRequired: boolean(r.prescriptionRequired),
  }
}
const weekdays: readonly Weekday[] = [
  'monday',
  'tuesday',
  'wednesday',
  'thursday',
  'friday',
  'saturday',
  'sunday',
]
function time(value: unknown): string {
  const result = text(value)
  if (!/^([01]\d|2[0-3]):[0-5]\d$/.test(result)) invalid()
  return result
}
export function pharmacy(value: unknown): PharmacyBranch {
  const r = record(value)
  const hours = record(r.openingHours)
  const openingHours = Object.fromEntries(
    weekdays.map((day) => [
      day,
      collection(hours[day], (value) => {
        const interval = record(value)
        const opensAt = time(interval.opensAt),
          closesAt = time(interval.closesAt)
        if (opensAt >= closesAt) invalid()
        return { opensAt, closesAt }
      }),
    ]),
  ) as unknown as PharmacyBranch['openingHours']
  return {
    id: text(r.id),
    slug: text(r.slug),
    name: text(r.name),
    fictionalLocation: text(r.fictionalLocation),
    timeZone: text(r.timeZone),
    openingHours,
  }
}
function price(value: unknown): ReferencePrice | null {
  if (value === null) return null
  const r = record(value)
  if (
    typeof r.minorUnits !== 'number' ||
    !Number.isSafeInteger(r.minorUnits) ||
    r.minorUnits < 0
  )
    invalid()
  const currencyCode = text(r.currencyCode)
  if (!/^[A-Z]{3}$/.test(currencyCode)) invalid()
  return { minorUnits: r.minorUnits, currencyCode }
}
export function availability(value: unknown): Availability {
  const r = record(value)
  if (
    r.status !== 'available' &&
    r.status !== 'low-stock' &&
    r.status !== 'unavailable'
  )
    invalid()
  const updatedAt = text(r.updatedAt)
  if (
    !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/.test(updatedAt) ||
    !Number.isFinite(Date.parse(updatedAt)) ||
    new Date(updatedAt).toISOString() !== updatedAt.replace('Z', '.000Z')
  )
    invalid()
  return {
    medicationId: text(r.medicationId),
    branchId: text(r.branchId),
    status: r.status,
    referencePrice: price(r.referencePrice),
    updatedAt,
  }
}

export function validateReferences(
  medications: readonly Medication[],
  branches: readonly PharmacyBranch[],
  observations: readonly Availability[],
) {
  const medicationIds = new Set(medications.map((m) => m.id)),
    branchIds = new Set(branches.map((b) => b.id))
  if (
    observations.some(
      (o) => !medicationIds.has(o.medicationId) || !branchIds.has(o.branchId),
    )
  )
    invalid()
}
