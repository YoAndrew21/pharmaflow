import { describe, expect, it } from 'vitest'
import { demoDataset } from '../data/localCatalogReader'
import {
  defaultCatalogQuery,
  discoverCatalog,
  normalizeSearch,
  type CatalogQuery,
} from './catalog'

const discover = (query: Partial<CatalogQuery> = {}) =>
  discoverCatalog(demoDataset, { ...defaultCatalogQuery, ...query })
const ids = (query: Partial<CatalogQuery> = {}) =>
  discover(query).items.map((i) => i.medication.id)

describe('catalog discovery rules', () => {
  it('normalizes case, whitespace, and decomposed/precomposed accents', () => {
    expect(normalizeSearch('  PÁRACÉTAMOL  ')).toBe('paracetamol')
    expect(ids({ q: '  PÁRACÉTAMOL  ' })).toEqual([
      'med-par-1000',
      'med-par-500',
      'med-par-caf',
    ])
    expect(ids({ q: 'CE\u0301TI' })).toEqual(['med-cet-10'])
  })
  it('searches ingredient substrings including a combination ingredient', () => {
    expect(ids({ q: 'caff' })).toEqual(['med-par-caf'])
    expect(ids({ q: 'chlor' })).toEqual(['med-sal-09'])
    const ingredientOnly = {
      ...demoDataset,
      medications: demoDataset.medications.map((m) =>
        m.id === 'med-par-caf'
          ? { ...m, displayName: 'Illustrative combination' }
          : m,
      ),
    }
    expect(
      discoverCatalog(ingredientOnly, {
        ...defaultCatalogQuery,
        q: 'caffeine',
      }).items.map((i) => i.medication.id),
    ).toEqual(['med-par-caf'])
  })
  it('does not apply fuzzy matching or medical synonyms', () => {
    expect(ids({ q: 'paracetmol' })).toEqual([])
    expect(ids({ q: 'headache' })).toEqual([])
  })
  it('combines search, branch, availability, and form using AND', () => {
    expect(
      ids({
        q: 'para',
        branch: 'aster-commons',
        availability: 'low-stock',
        form: 'tablet',
      }),
    ).toEqual(['med-par-1000'])
    expect(
      ids({
        q: 'para',
        branch: 'aster-commons',
        availability: 'low-stock',
        form: 'solution',
      }),
    ).toEqual([])
  })
  it('matches a status at any branch when none is selected', () => {
    expect(ids({ availability: 'low-stock' })).toContain('med-par-500')
    expect(ids({ availability: 'available' })).toContain('med-cet-10')
  })
  it('restricts status to the selected branch', () => {
    expect(
      ids({ branch: 'aster-commons', availability: 'available' }),
    ).not.toContain('med-cet-10')
    expect(
      ids({ branch: 'aster-commons', availability: 'unavailable' }),
    ).toContain('med-cet-10')
  })
  it('keeps unknown availability separate from unavailable stock', () => {
    const saline = discover({ q: 'chlor', branch: 'aster-commons' }).items[0]!
    expect(saline.selectedAvailability).toBeNull()
    expect(saline.summary.unknown).toBe(1)
    expect(saline.referencePrice).toBeNull()
    expect(
      ids({ branch: 'aster-commons', availability: 'unavailable' }),
    ).not.toContain('med-sal-09')
    const combination = discover({ q: 'caffeine' }).items[0]!
    expect(combination.summary).toEqual({
      available: 0,
      lowStock: 0,
      unavailable: 0,
      unknown: 3,
      total: 3,
    })
  })
  it('summarizes mixed statuses without collapsing them', () => {
    expect(
      discover({ q: 'paracetamol', form: 'tablet' }).items.find(
        (i) => i.medication.id === 'med-par-500',
      )?.summary,
    ).toEqual({
      available: 1,
      lowStock: 1,
      unavailable: 1,
      unknown: 0,
      total: 3,
    })
  })
  it('sorts by name then stable ID for identical display names', () => {
    expect(ids()).toEqual([
      'med-asc-250',
      'med-cet-10',
      'med-ibu-200',
      'med-lor-1',
      'med-ome-20',
      'med-par-1000',
      'med-par-500',
      'med-par-caf',
      'med-sal-09',
    ])
  })
  it('uses the lowest stocked branch price and excludes unavailable branch prices', () => {
    const par = discover().items.find((i) => i.medication.id === 'med-par-500')!
    expect(par.referencePrice?.minorUnits).toBe(320)
    expect(
      discover().items.find((i) => i.medication.id === 'med-ome-20')
        ?.referencePrice,
    ).toBeNull()
    expect(
      discover().items.find((i) => i.medication.id === 'med-lor-1')
        ?.referencePrice,
    ).toBeNull()
  })
  it('uses only the selected branch price, even for an unavailable record', () => {
    expect(
      discover({ branch: 'aster-commons' }).items.find(
        (i) => i.medication.id === 'med-par-500',
      )?.referencePrice?.minorUnits,
    ).toBe(450)
    expect(
      discover({ branch: 'lantern-court' }).items.find(
        (i) => i.medication.id === 'med-par-500',
      )?.referencePrice?.minorUnits,
    ).toBe(200)
  })
  it('sorts aggregate prices ascending with missing prices last', () => {
    expect(ids({ sort: 'price' })).toEqual([
      'med-sal-09',
      'med-asc-250',
      'med-par-500',
      'med-ibu-200',
      'med-cet-10',
      'med-par-1000',
      'med-lor-1',
      'med-ome-20',
      'med-par-caf',
    ])
  })
  it('sorts selected branch prices and breaks equal prices by name then ID', () => {
    expect(ids({ branch: 'aster-commons', sort: 'price' })).toEqual([
      'med-asc-250',
      'med-ibu-200',
      'med-par-500',
      'med-cet-10',
      'med-par-1000',
      'med-ome-20',
      'med-lor-1',
      'med-par-caf',
      'med-sal-09',
    ])
    const data = {
      ...demoDataset,
      availability: demoDataset.availability.map((r) =>
        r.branchId === 'aster-commons'
          ? { ...r, referencePrice: { minorUnits: 100, currencyCode: 'USD' } }
          : r,
      ),
    }
    expect(
      discoverCatalog(data, {
        ...defaultCatalogQuery,
        branch: 'aster-commons',
        sort: 'price',
      })
        .items.slice(0, 6)
        .map((i) => i.medication.id),
    ).toEqual([
      'med-asc-250',
      'med-cet-10',
      'med-ibu-200',
      'med-lor-1',
      'med-ome-20',
      'med-par-1000',
    ])
  })
  it('handles a zero reference price and does not mutate the source', () => {
    const original = JSON.stringify(demoDataset)
    const zero = {
      ...demoDataset,
      availability: demoDataset.availability.map((r) =>
        r.medicationId === 'med-asc-250'
          ? { ...r, referencePrice: { minorUnits: 0, currencyCode: 'USD' } }
          : r,
      ),
    }
    expect(
      discoverCatalog(zero, { ...defaultCatalogQuery, sort: 'price' }).items[0]
        ?.referencePrice?.minorUnits,
    ).toBe(0)
    discover({ sort: 'price' })
    expect(JSON.stringify(demoDataset)).toBe(original)
  })
  it('ignores unknown branch/form values safely', () => {
    expect(ids({ branch: 'invalid', form: 'invalid' })).toEqual(ids())
    expect(discover({ branch: 'invalid', form: 'invalid' }).query).toEqual(
      defaultCatalogQuery,
    )
  })
})
