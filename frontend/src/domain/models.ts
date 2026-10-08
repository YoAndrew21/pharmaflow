/** One distinct strength/form/package presentation; all future data is fictional. */
export interface Medication {
  readonly id: string
  readonly slug: string
  readonly displayName: string
  readonly activeIngredients: readonly string[]
  readonly strengthLabel: string
  readonly formCode: string
  readonly presentationLabel: string
  readonly categoryCode: string
  readonly prescriptionRequired: boolean
}

export type Weekday =
  | 'monday'
  | 'tuesday'
  | 'wednesday'
  | 'thursday'
  | 'friday'
  | 'saturday'
  | 'sunday'

/** Local HH:mm times; an empty day means closed. Split intervals are supported. */
export interface OpeningInterval {
  readonly opensAt: string
  readonly closesAt: string
}

export interface PharmacyBranch {
  readonly id: string
  readonly slug: string
  readonly name: string
  readonly fictionalLocation: string
  readonly openingHours: Readonly<Record<Weekday, readonly OpeningInterval[]>>
  readonly timeZone: string
}

export type AvailabilityStatus = 'available' | 'low-stock' | 'unavailable'

export interface ReferencePrice {
  readonly minorUnits: number
  readonly currencyCode: string
}

/** Unique medication/branch pair. Missing records mean unknown, never unavailable. */
export interface Availability {
  readonly medicationId: Medication['id']
  readonly branchId: PharmacyBranch['id']
  readonly status: AvailabilityStatus
  readonly referencePrice: ReferencePrice | null
  /** ISO 8601 UTC demonstration observation, not a live update. */
  readonly updatedAt: string
}

export type FavoriteMedicationId = Medication['id']

/** UI request state stays separate from medication stock status. */
export type ReadState<T> =
  | { readonly status: 'idle' }
  | { readonly status: 'loading' }
  | { readonly status: 'success'; readonly data: T }
  | { readonly status: 'error'; readonly message: string }
