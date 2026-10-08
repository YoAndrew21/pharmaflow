import type { Availability, PharmacyBranch } from './models'

export interface BranchComparisonRow {
  readonly branch: PharmacyBranch
  readonly observation: Availability | null
}

/** Missing data stays null; never manufacture an Availability record or stock status. */
export function buildBranchComparison(
  branches: readonly PharmacyBranch[],
  observations: readonly Availability[],
): readonly BranchComparisonRow[] {
  return branches.map((branch) => ({
    branch,
    observation:
      observations.find((record) => record.branchId === branch.id) ?? null,
  }))
}
