import { describe, expect, it } from 'vitest'
import canonical from '../../../demo-data/catalog.json'
import { defaultCatalogQuery } from '../domain/catalog'
import { demoDataset, localCatalogReader } from './localCatalogReader'
import { assertDemoDataset } from './validateDataset'

describe('canonical fictional fixtures', () => {
  it('validates the complete schema, identities, relationships, prices, and fixed times', () => {
    expect(() => assertDemoDataset(canonical)).not.toThrow()
    expect(demoDataset.medications).toHaveLength(9)
    expect(demoDataset.branches.map((b) => b.name)).toEqual([
      'Aster Commons',
      'Willow Arcade',
      'Lantern Court',
    ])
    expect(demoDataset.snapshotAt).toBe('2026-09-01T12:00:00Z')
    expect(new Set(demoDataset.medications.map((m) => m.id)).size).toBe(9)
    expect(new Set(demoDataset.medications.map((m) => m.slug)).size).toBe(9)
    expect(new Set(demoDataset.branches.map((b) => b.id)).size).toBe(3)
    expect(new Set(demoDataset.branches.map((b) => b.slug)).size).toBe(3)
    expect(
      new Set(
        demoDataset.availability.map((r) => `${r.medicationId}/${r.branchId}`),
      ).size,
    ).toBe(demoDataset.availability.length)
  })
  it('covers distinct presentations, combination ingredients, forms, missing observations, and null prices', () => {
    expect(
      demoDataset.medications.filter((m) => m.displayName === 'Paracetamol'),
    ).toHaveLength(2)
    expect(
      demoDataset.medications.some((m) => m.activeIngredients.length > 1),
    ).toBe(true)
    expect(new Set(demoDataset.medications.map((m) => m.formCode)).size).toBe(3)
    expect(new Set(demoDataset.availability.map((r) => r.status))).toEqual(
      new Set(['available', 'low-stock', 'unavailable']),
    )
    expect(
      demoDataset.availability.some((r) => r.referencePrice === null),
    ).toBe(true)
    expect(demoDataset.availability.length).toBeLessThan(9 * 3)
    expect(
      new Set(demoDataset.availability.map((r) => r.updatedAt)).size,
    ).toBeGreaterThan(1)
    expect(
      demoDataset.availability.every(
        (r) =>
          demoDataset.medications.some((m) => m.id === r.medicationId) &&
          demoDataset.branches.some((b) => b.id === r.branchId),
      ),
    ).toBe(true)
  })
  it.each(['id', 'slug'] as const)(
    'rejects duplicate medication %s',
    (field) => {
      const data = structuredClone(canonical)
      data.medications[1]![field] = data.medications[0]![field]
      expect(() => assertDemoDataset(data)).toThrow(
        'duplicate medication identity',
      )
    },
  )
  it.each(['id', 'slug'] as const)('rejects duplicate branch %s', (field) => {
    const data = structuredClone(canonical)
    data.branches[1]![field] = data.branches[0]![field]
    expect(() => assertDemoDataset(data)).toThrow('duplicate branch identity')
  })
  it('rejects duplicate pairs and invalid foreign references', () => {
    const duplicate = structuredClone(canonical)
    duplicate.availability.push(duplicate.availability[0]!)
    expect(() => assertDemoDataset(duplicate)).toThrow(
      'duplicate availability pair',
    )
    const foreign = structuredClone(canonical)
    foreign.availability[0]!.medicationId = 'does-not-exist'
    expect(() => assertDemoDataset(foreign)).toThrow(
      'invalid foreign reference',
    )
    foreign.availability[0]!.medicationId = canonical.medications[0]!.id
    foreign.availability[0]!.branchId = 'does-not-exist'
    expect(() => assertDemoDataset(foreign)).toThrow(
      'invalid foreign reference',
    )
  })
  it('rejects invalid status, prices, currencies, and timestamps', () => {
    const data = structuredClone(canonical)
    data.availability[0]!.status = 'unknown'
    expect(() => assertDemoDataset(data)).toThrow('invalid availability status')
    data.availability[0]!.status = 'available'
    data.availability[0]!.referencePrice!.minorUnits = -1
    expect(() => assertDemoDataset(data)).toThrow('invalid price')
    data.availability[0]!.referencePrice!.minorUnits = 1.5
    expect(() => assertDemoDataset(data)).toThrow('invalid price')
    data.availability[0]!.referencePrice!.minorUnits = 0
    data.availability[0]!.referencePrice!.currencyCode = 'EUR'
    expect(() => assertDemoDataset(data)).toThrow('expected demo USD currency')
    data.availability[0]!.referencePrice!.currencyCode = 'USD'
    data.availability[0]!.updatedAt = 'invalid-date'
    expect(() => assertDemoDataset(data)).toThrow('expected UTC timestamp')
    data.availability[0]!.updatedAt = '2026-09-02T12:00:00Z'
    expect(() => assertDemoDataset(data)).toThrow('observation after snapshot')
  })
})

describe('local typed reader', () => {
  it('is asynchronous, deterministic, and does not mutate canonical data', async () => {
    const first = localCatalogReader.discover(defaultCatalogQuery)
    expect(first).toBeInstanceOf(Promise)
    expect(await first).toEqual(
      await localCatalogReader.discover(defaultCatalogQuery),
    )
    expect(Object.isFrozen(demoDataset)).toBe(true)
    expect(Object.isFrozen(demoDataset.medications[0])).toBe(true)
  })
  it('looks up resources and returns null or empty arrays for missing records', async () => {
    expect(await localCatalogReader.listMedications()).toHaveLength(9)
    expect(await localCatalogReader.listBranches()).toHaveLength(3)
    expect(
      (await localCatalogReader.getMedication('paracetamol-500-tablets'))?.id,
    ).toBe('med-par-500')
    expect((await localCatalogReader.getBranch('aster-commons'))?.name).toBe(
      'Aster Commons',
    )
    expect(await localCatalogReader.getMedication('missing')).toBeNull()
    expect(await localCatalogReader.getBranch('missing')).toBeNull()
    expect(await localCatalogReader.getAvailability('med-par-caf')).toEqual([])
  })
})
