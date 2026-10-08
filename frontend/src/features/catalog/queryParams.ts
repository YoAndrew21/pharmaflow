import { availabilityStatuses, type CatalogQuery } from '../../domain/catalog'

export function parseCatalogQuery(params: URLSearchParams): CatalogQuery {
  const status = params.get('availability')
  return {
    q: (params.get('q') ?? '').trim(),
    branch: params.get('branch') ?? '',
    availability: availabilityStatuses.find((value) => value === status) ?? '',
    form: params.get('form') ?? '',
    sort: params.get('sort') === 'price' ? 'price' : 'name',
  }
}

export function serializeCatalogQuery(query: CatalogQuery): URLSearchParams {
  const params = new URLSearchParams()
  if (query.q.trim()) params.set('q', query.q.trim())
  if (query.branch) params.set('branch', query.branch)
  if (query.availability) params.set('availability', query.availability)
  if (query.form) params.set('form', query.form)
  if (query.sort === 'price') params.set('sort', 'price')
  return params
}
