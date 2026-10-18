import { discoverCatalog } from '../domain/catalog'
import type { CatalogReader } from './catalogReader'
import { readJson } from './httpClient'
import {
  availability,
  collection,
  medication,
  pharmacy,
  unique,
  validateReferences,
} from './validateApiResponse'

export function createHttpCatalogReader(): CatalogReader {
  // Share in-flight reads only inside one caller's cancellation scope. No global
  // response cache, no failure cache, and no cancellation shared across consumers.
  const pending = new WeakMap<AbortSignal, Map<string, Promise<unknown>>>()
  function read(
    path: string,
    signal?: AbortSignal,
    detail = false,
  ): Promise<unknown> {
    if (!signal) return readJson(path, signal, detail)
    let requests = pending.get(signal)
    if (!requests) {
      requests = new Map()
      pending.set(signal, requests)
    }
    const existing = requests.get(path)
    if (existing) return existing
    const request = readJson(path, signal, detail).finally(() =>
      requests.delete(path),
    )
    requests.set(path, request)
    return request
  }
  const reader: CatalogReader = {
    async listMedications(signal) {
      const rows = collection(await read('medications', signal), medication)
      return unique(
        unique(rows, (m) => m.id),
        (m) => m.slug,
      )
    },
    async getMedication(slug, signal) {
      const value = await read(
        `medications/${encodeURIComponent(slug)}`,
        signal,
        true,
      )
      return value === undefined ? null : medication(value)
    },
    async listBranches(signal) {
      const rows = collection(await read('pharmacies', signal), pharmacy)
      return unique(
        unique(rows, (b) => b.id),
        (b) => b.slug,
      )
    },
    async getBranch(slug, signal) {
      const value = await read(
        `pharmacies/${encodeURIComponent(slug)}`,
        signal,
        true,
      )
      return value === undefined ? null : pharmacy(value)
    },
    async getAvailability(medicationId, signal) {
      const rows = unique(
        collection(await read('availability', signal), availability),
        (o) => JSON.stringify([o.medicationId, o.branchId]),
      )
      return rows.filter((o) => o.medicationId === medicationId)
    },
    async discover(query, signal) {
      const [medications, branches, observations] = await Promise.all([
        reader.listMedications(signal),
        reader.listBranches(signal),
        read('availability', signal).then((value) =>
          unique(collection(value, availability), (o) =>
            JSON.stringify([o.medicationId, o.branchId]),
          ),
        ),
      ])
      validateReferences(medications, branches, observations)
      return discoverCatalog(
        {
          medications,
          branches,
          availability: observations,
          snapshotAt: __DEMO_SNAPSHOT_AT__,
        },
        query,
      )
    },
  }
  return reader
}
