import canonical from '../../../demo-data/catalog.json'
import { discoverCatalog, type DemoDataset } from '../domain/catalog'
import type { CatalogReader } from './catalogReader'
import { assertDemoDataset } from './validateDataset'

assertDemoDataset(canonical)
function freezeDeep<T>(value: T): T {
  if (value && typeof value === 'object') {
    Object.values(value).forEach(freezeDeep)
    Object.freeze(value)
  }
  return value
}

// One canonical JSON source, guarded and immutable. No network or artificial latency.
export const demoDataset: DemoDataset = freezeDeep(canonical)
export const localCatalogReader: CatalogReader = {
  async discover(query) {
    return discoverCatalog(demoDataset, query)
  },
  async listMedications() {
    return demoDataset.medications
  },
  async getMedication(slug) {
    return demoDataset.medications.find((m) => m.slug === slug) ?? null
  },
  async listBranches() {
    return demoDataset.branches
  },
  async getBranch(slug) {
    return demoDataset.branches.find((b) => b.slug === slug) ?? null
  },
  async getAvailability(medicationId) {
    return demoDataset.availability.filter(
      (r) => r.medicationId === medicationId,
    )
  },
}
