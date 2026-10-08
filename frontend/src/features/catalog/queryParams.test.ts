import { describe, expect, it } from 'vitest'
import { defaultCatalogQuery } from '../../domain/catalog'
import { parseCatalogQuery, serializeCatalogQuery } from './queryParams'

describe('catalog URL state', () => {
  it('uses safe defaults for missing and invalid enumerated values', () => {
    expect(parseCatalogQuery(new URLSearchParams())).toEqual(
      defaultCatalogQuery,
    )
    expect(
      parseCatalogQuery(
        new URLSearchParams('availability=unknown&sort=expensive'),
      ),
    ).toEqual(defaultCatalogQuery)
  })
  it('parses all supported parameters and ignores unrelated ones', () => {
    expect(
      parseCatalogQuery(
        new URLSearchParams(
          'q=%20Caffeine%20&branch=aster-commons&availability=low-stock&form=tablet&sort=price&unused=x',
        ),
      ),
    ).toEqual({
      q: 'Caffeine',
      branch: 'aster-commons',
      availability: 'low-stock',
      form: 'tablet',
      sort: 'price',
    })
  })
  it('round-trips reserved characters and omits empty/default values', () => {
    const query = {
      ...defaultCatalogQuery,
      q: 'Paracetamol + Caffeine & demo',
      sort: 'price' as const,
    }
    expect(parseCatalogQuery(serializeCatalogQuery(query))).toEqual(query)
    expect(serializeCatalogQuery(defaultCatalogQuery).toString()).toBe('')
  })
})
