import { useEffect, useState } from 'react'
import type { CatalogReader } from '../../data/catalogReader'
import type {
  Availability,
  Medication,
  PharmacyBranch,
  ReadState,
} from '../../domain/models'

export interface MedicationDetail {
  readonly medication: Medication
  readonly branches: readonly PharmacyBranch[]
  readonly observations: readonly Availability[]
}

export function useMedicationDetail(
  reader: CatalogReader,
  slug: string,
  attempt: number,
): ReadState<MedicationDetail | null> {
  const [response, setResponse] = useState<{
    slug: string
    attempt: number
    reader: CatalogReader
    state: ReadState<MedicationDetail | null>
  } | null>(null)
  useEffect(() => {
    let active = true
    const controller = new AbortController()
    async function read() {
      try {
        const medication = await reader.getMedication(slug, controller.signal)
        if (!active) return
        if (!medication) {
          setResponse({
            slug,
            attempt,
            reader,
            state: { status: 'success', data: null },
          })
          return
        }
        const [branches, observations] = await Promise.all([
          reader.listBranches(controller.signal),
          reader.getAvailability(medication.id, controller.signal),
        ])
        if (active)
          setResponse({
            slug,
            attempt,
            reader,
            state: {
              status: 'success',
              data: { medication, branches, observations },
            },
          })
      } catch {
        if (active)
          setResponse({
            slug,
            attempt,
            reader,
            state: { status: 'error', message: 'medication-read-failed' },
          })
      }
    }
    void read()
    return () => {
      active = false
      controller.abort()
    }
  }, [reader, slug, attempt])
  return response?.slug === slug &&
    response.attempt === attempt &&
    response.reader === reader
    ? response.state
    : { status: 'loading' }
}
