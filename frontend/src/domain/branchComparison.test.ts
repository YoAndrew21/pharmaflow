import { describe, expect, it } from 'vitest'
import { demoDataset } from '../data/localCatalogReader'
import { buildBranchComparison } from './branchComparison'

describe('branch comparison', () => {
  it('preserves actual observations, including unavailable prices and timestamps', () => {
    const observations = demoDataset.availability.filter(
      (item) => item.medicationId === 'med-par-500',
    )
    const rows = buildBranchComparison(demoDataset.branches, observations)
    expect(rows).toHaveLength(3)
    rows.forEach((row, index) => {
      expect(row.branch).toBe(demoDataset.branches[index])
      expect(row.observation).toBe(
        observations.find((item) => item.branchId === row.branch.id),
      )
    })
    expect(rows.map((row) => row.observation?.status).sort()).toEqual([
      'available',
      'low-stock',
      'unavailable',
    ])
  })
  it('keeps absent observations null without fabricating records', () => {
    const rows = buildBranchComparison(demoDataset.branches, [])
    expect(rows.map((row) => row.observation)).toEqual([null, null, null])
    expect(demoDataset.availability).toHaveLength(21)
  })
})
