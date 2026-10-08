import { parseCatalogQuery, serializeCatalogQuery } from './queryParams'

/** Carry only catalog query state, never an arbitrary return URL. Survives refresh/share. */
export function medicationPath(slug: string, catalogSearch: string): string {
  const catalog = serializeCatalogQuery(
    parseCatalogQuery(new URLSearchParams(catalogSearch)),
  ).toString()
  const params = new URLSearchParams()
  if (catalog) params.set('catalog', catalog)
  return `/medications/${encodeURIComponent(slug)}${params.size ? `?${params}` : ''}`
}

export function catalogReturnPath(detailParams: URLSearchParams): string {
  const search = serializeCatalogQuery(
    parseCatalogQuery(new URLSearchParams(detailParams.get('catalog') ?? '')),
  ).toString()
  return `/catalog${search ? `?${search}` : ''}`
}
