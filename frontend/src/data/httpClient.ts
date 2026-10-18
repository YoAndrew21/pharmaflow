export type ApiFailureKind = 'http' | 'network' | 'invalid-response'

export class ApiReadError extends Error {
  readonly kind: ApiFailureKind
  readonly status?: number
  constructor(kind: ApiFailureKind, status?: number) {
    super(`API read failed: ${kind}`)
    this.kind = kind
    this.status = status
  }
}

/** Errors carry categories only; backend bodies never become UI messages. */
export async function readJson(
  path: string,
  signal?: AbortSignal,
  detail = false,
): Promise<unknown> {
  let response: Response
  try {
    response = await fetch(`/api/${path}`, { signal })
  } catch (error) {
    if (
      signal?.aborted ||
      (error instanceof Error && error.name === 'AbortError')
    )
      throw error
    throw new ApiReadError('network')
  }
  if (detail && response.status === 404) return undefined
  if (!response.ok) throw new ApiReadError('http', response.status)
  try {
    return await response.json()
  } catch (error) {
    if (signal?.aborted) throw error
    throw new ApiReadError('invalid-response')
  }
}
