import type { Availability, Medication, PharmacyBranch } from '../domain/models'
import type { CatalogQuery, CatalogResult } from '../domain/catalog'

/** A small read boundary. Future local/API implementations preserve these domain shapes.
 * Discovery projections keep query rules behind the boundary for the future API adapter.
 */
export interface CatalogReader {
  discover(query: CatalogQuery): Promise<CatalogResult>
  listMedications(): Promise<readonly Medication[]>
  getMedication(slug: string): Promise<Medication | null>
  listBranches(): Promise<readonly PharmacyBranch[]>
  getBranch(slug: string): Promise<PharmacyBranch | null>
  getAvailability(medicationId: string): Promise<readonly Availability[]>
}
