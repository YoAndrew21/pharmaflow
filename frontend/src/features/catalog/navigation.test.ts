import { describe, expect, it } from 'vitest'
import { catalogReturnPath, medicationPath } from './navigation'

describe('catalog detail navigation', () => {
  it('round trips useful filters in a shareable detail URL', () => {
    const search =
      '?q=para&branch=aster-commons&availability=low-stock&form=tablet&sort=price'
    const detail = new URL(
      medicationPath('paracetamol-1000-tablets', search),
      'https://example.test',
    )
    expect(catalogReturnPath(detail.searchParams)).toBe(`/catalog${search}`)
  })
  it('recovers direct detail routes to the catalog', () => {
    expect(catalogReturnPath(new URLSearchParams())).toBe('/catalog')
  })
  it('does not use arbitrary return URLs or unrelated parameters', () => {
    expect(
      catalogReturnPath(new URLSearchParams('catalog=https://outside.test')),
    ).toBe('/catalog')
    expect(medicationPath('a/b', '?unrelated=yes')).toBe('/medications/a%2Fb')
  })
})
