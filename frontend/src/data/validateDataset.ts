import { availabilityStatuses, type DemoDataset } from '../domain/catalog'

function requireCondition(
  condition: unknown,
  message: string,
): asserts condition {
  if (!condition) throw new Error(`Invalid demo dataset: ${message}`)
}
function object(value: unknown): asserts value is Record<string, unknown> {
  requireCondition(
    typeof value === 'object' && value !== null && !Array.isArray(value),
    'expected object',
  )
}
function text(value: unknown): asserts value is string {
  requireCondition(
    typeof value === 'string' && value.trim().length > 0,
    'expected nonempty string',
  )
}
function instant(value: unknown): asserts value is string {
  text(value)
  requireCondition(
    /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/.test(value) &&
      Number.isFinite(Date.parse(value)),
    'expected UTC timestamp',
  )
}
function identity(rows: unknown[], label: string): Set<string> {
  const ids = new Set<string>()
  const slugs = new Set<string>()
  for (const row of rows) {
    object(row)
    text(row.id)
    text(row.slug)
    requireCondition(
      !ids.has(row.id) && !slugs.has(row.slug),
      `duplicate ${label} identity`,
    )
    ids.add(row.id)
    slugs.add(row.slug)
  }
  return ids
}

/** Fail loudly on malformed canonical data, before it is used as a future seed. */
export function assertDemoDataset(
  value: unknown,
): asserts value is DemoDataset {
  object(value)
  instant(value.snapshotAt)
  requireCondition(
    Array.isArray(value.medications) &&
      Array.isArray(value.branches) &&
      Array.isArray(value.availability),
    'expected dataset arrays',
  )
  const medicationIds = identity(value.medications, 'medication')
  const branchIds = identity(value.branches, 'branch')
  for (const medication of value.medications) {
    object(medication)
    for (const key of [
      'displayName',
      'strengthLabel',
      'formCode',
      'presentationLabel',
      'categoryCode',
    ])
      text(medication[key])
    requireCondition(
      Array.isArray(medication.activeIngredients) &&
        medication.activeIngredients.length > 0,
      'expected ingredients',
    )
    medication.activeIngredients.forEach(text)
    requireCondition(
      typeof medication.prescriptionRequired === 'boolean',
      'expected prescription flag',
    )
  }
  const days = [
    'monday',
    'tuesday',
    'wednesday',
    'thursday',
    'friday',
    'saturday',
    'sunday',
  ]
  for (const branch of value.branches) {
    object(branch)
    text(branch.name)
    text(branch.fictionalLocation)
    text(branch.timeZone)
    object(branch.openingHours)
    new Intl.DateTimeFormat('en', { timeZone: branch.timeZone })
    for (const day of days) {
      const intervals = branch.openingHours[day]
      requireCondition(Array.isArray(intervals), 'expected weekly intervals')
      for (const interval of intervals) {
        object(interval)
        text(interval.opensAt)
        text(interval.closesAt)
        const time = /^(?:[01]\d|2[0-3]):[0-5]\d$/
        requireCondition(
          time.test(interval.opensAt) &&
            time.test(interval.closesAt) &&
            interval.opensAt < interval.closesAt,
          'expected opening interval',
        )
      }
    }
  }
  const pairs = new Set<string>()
  const currencies = new Set<string>()
  for (const record of value.availability) {
    object(record)
    text(record.medicationId)
    text(record.branchId)
    text(record.status)
    instant(record.updatedAt)
    requireCondition(
      medicationIds.has(record.medicationId) && branchIds.has(record.branchId),
      'invalid foreign reference',
    )
    const pair = `${record.medicationId}/${record.branchId}`
    requireCondition(!pairs.has(pair), 'duplicate availability pair')
    pairs.add(pair)
    requireCondition(
      availabilityStatuses.some((status) => status === record.status),
      'invalid availability status',
    )
    requireCondition(
      Date.parse(record.updatedAt) <= Date.parse(value.snapshotAt),
      'observation after snapshot',
    )
    if (record.referencePrice !== null) {
      object(record.referencePrice)
      const price = record.referencePrice
      requireCondition(
        typeof price.minorUnits === 'number' &&
          Number.isSafeInteger(price.minorUnits) &&
          price.minorUnits >= 0,
        'invalid price',
      )
      text(price.currencyCode)
      requireCondition(
        price.currencyCode === 'USD',
        'expected demo USD currency',
      )
      currencies.add(price.currencyCode)
    }
  }
  requireCondition(currencies.size <= 1, 'mixed demo currencies')
}
