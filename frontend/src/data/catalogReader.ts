import type { Availability, Medication, PharmacyBranch } from '../domain/models'
import type { CatalogQuery, CatalogResult } from '../domain/catalog'

/** A small asynchronous read boundary shared by HTTP runtime and local test readers.
 * Discovery currently applies pure frontend query rules to HTTP-sourced records.
 */
export interface CatalogReader {
  discover(query: CatalogQuery, signal?: AbortSignal): Promise<CatalogResult>
  listMedications(signal?: AbortSignal): Promise<readonly Medication[]>
  getMedication(slug: string, signal?: AbortSignal): Promise<Medication | null>
  listBranches(signal?: AbortSignal): Promise<readonly PharmacyBranch[]>
  getBranch(slug: string, signal?: AbortSignal): Promise<PharmacyBranch | null>
  getAvailability(
    medicationId: string,
    signal?: AbortSignal,
  ): Promise<readonly Availability[]>
}
