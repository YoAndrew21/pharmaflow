/// <reference types="node" />
import { readFileSync, existsSync } from 'node:fs'
import { dirname, resolve, extname } from 'node:path'
import { expect, it } from 'vitest'

it('the browser entry import graph contains HTTP runtime wiring and no canonical reader/data', () => {
  const visited = new Set<string>()
  function visit(path: string) {
    if (visited.has(path)) return
    visited.add(path)
    const source = readFileSync(path, 'utf8')
    const imports = source.matchAll(
      /(?:from\s*|import\s*|import\s*\()(['"])([^'"]+)\1/g,
    )
    for (const match of imports) {
      const name = match[2]!
      if (!name.startsWith('.')) continue
      const base = resolve(dirname(path), name)
      const target = [
        base,
        `${base}.ts`,
        `${base}.tsx`,
        resolve(base, 'index.ts'),
        resolve(base, 'index.tsx'),
      ].find(
        (p) => existsSync(p) && ['.ts', '.tsx', '.json'].includes(extname(p)),
      )
      if (target) visit(target)
    }
  }
  visit(resolve('src/main.tsx'))
  expect([...visited].some((p) => p.endsWith('runtimeCatalogReader.ts'))).toBe(
    true,
  )
  expect(
    [...visited].some((p) => /localCatalogReader|catalog\.json/.test(p)),
  ).toBe(false)
  const pages = [
    'HomePage',
    'CatalogPage',
    'MedicationDetailPage',
    'PharmacyDirectoryPage',
    'PharmacyDetailPage',
    'FavoritesPage',
  ]
  for (const page of pages)
    expect(readFileSync(resolve(`src/pages/${page}.tsx`), 'utf8')).toContain(
      'reader = runtimeCatalogReader',
    )
})
