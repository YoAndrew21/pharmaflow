import type {
  Availability,
  AvailabilityStatus,
  Medication,
  PharmacyBranch,
  ReferencePrice,
} from './models'

export interface DemoDataset {
  readonly snapshotAt: string
  readonly medications: readonly Medication[]
  readonly branches: readonly PharmacyBranch[]
  readonly availability: readonly Availability[]
}

export const availabilityStatuses = [
  'available',
  'low-stock',
  'unavailable',
] as const
export type CatalogSort = 'name' | 'price'
export interface CatalogQuery {
  readonly q: string
  readonly branch: string
  readonly availability: AvailabilityStatus | ''
  readonly form: string
  readonly sort: CatalogSort
}

export const defaultCatalogQuery: CatalogQuery = {
  q: '',
  branch: '',
  availability: '',
  form: '',
  sort: 'name',
}
export interface AvailabilitySummary {
  readonly available: number
  readonly lowStock: number
  readonly unavailable: number
  readonly unknown: number
  readonly total: number
}

export interface CatalogItem {
  readonly medication: Medication
  readonly summary: AvailabilitySummary
  readonly selectedAvailability: Availability | null
  readonly referencePrice: ReferencePrice | null
}

export interface CatalogResult {
  readonly items: readonly CatalogItem[]
  readonly branches: readonly PharmacyBranch[]
  readonly forms: readonly string[]
  readonly query: CatalogQuery
  readonly snapshotAt: string
}

export function normalizeSearch(value: string): string {
  return value
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .toLocaleLowerCase('en')
    .trim()
}

function compareText(a: string, b: string): number {
  // Stable ordinal comparison after normalization, independent of OS collation.
  return a < b ? -1 : a > b ? 1 : 0
}

export function summarizeAvailability(
  records: readonly Availability[],
  branchCount: number,
): AvailabilitySummary {
  return {
    available: records.filter((r) => r.status === 'available').length,
    lowStock: records.filter((r) => r.status === 'low-stock').length,
    unavailable: records.filter((r) => r.status === 'unavailable').length,
    unknown: branchCount - records.length,
    total: branchCount,
  }
}

export function normalizeCatalogQuery(
  requested: CatalogQuery,
  branches: readonly PharmacyBranch[],
  forms: readonly string[],
): CatalogQuery {
  return {
    ...requested,
    branch: branches.some((b) => b.id === requested.branch)
      ? requested.branch
      : '',
    form: forms.includes(requested.form) ? requested.form : '',
  }
}

export function discoverCatalog(
  dataset: DemoDataset,
  requested: CatalogQuery,
): CatalogResult {
  const forms = [...new Set(dataset.medications.map((m) => m.formCode))].sort(
    compareText,
  )
  const query = normalizeCatalogQuery(requested, dataset.branches, forms)
  const needle = normalizeSearch(query.q)
  const items: CatalogItem[] = []
  for (const medication of dataset.medications) {
    if (
      needle &&
      ![medication.displayName, ...medication.activeIngredients].some((text) =>
        normalizeSearch(text).includes(needle),
      )
    )
      continue
    if (query.form && medication.formCode !== query.form) continue
    const records = dataset.availability.filter(
      (r) => r.medicationId === medication.id,
    )
    const selectedAvailability =
      records.find((r) => r.branchId === query.branch) ?? null
    const relevant = query.branch
      ? selectedAvailability
        ? [selectedAvailability]
        : []
      : records
    if (
      query.availability &&
      !relevant.some((r) => r.status === query.availability)
    )
      continue
    const priced = query.branch
      ? relevant
      : records.filter(
          (r) => r.status === 'available' || r.status === 'low-stock',
        )
    const prices = priced.flatMap((r) =>
      r.referencePrice ? [r.referencePrice] : [],
    )
    const referencePrice = prices.reduce<ReferencePrice | null>(
      (lowest, price) =>
        !lowest || price.minorUnits < lowest.minorUnits ? price : lowest,
      null,
    )
    items.push({
      medication,
      selectedAvailability,
      referencePrice,
      summary: summarizeAvailability(records, dataset.branches.length),
    })
  }
  items.sort((a, b) => {
    if (query.sort === 'price') {
      if (a.referencePrice === null && b.referencePrice !== null) return 1
      if (a.referencePrice !== null && b.referencePrice === null) return -1
      if (a.referencePrice && b.referencePrice) {
        const difference =
          a.referencePrice.minorUnits - b.referencePrice.minorUnits
        if (difference) return difference
      }
    }
    return (
      compareText(
        normalizeSearch(a.medication.displayName),
        normalizeSearch(b.medication.displayName),
      ) || compareText(a.medication.id, b.medication.id)
    )
  })
  return {
    items,
    branches: dataset.branches,
    forms,
    query,
    snapshotAt: dataset.snapshotAt,
  }
}
