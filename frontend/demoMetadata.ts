import { readFileSync } from 'node:fs'

// Build/dev metadata only. No canonical response records enter the browser bundle.
const metadata: unknown = JSON.parse(
  readFileSync(new URL('../demo-data/catalog.json', import.meta.url), 'utf8'),
)
if (
  typeof metadata !== 'object' ||
  metadata === null ||
  !('snapshotAt' in metadata) ||
  typeof metadata.snapshotAt !== 'string'
)
  throw new Error('Missing fixed demo snapshot metadata')

export const demoMetadataDefine = {
  __DEMO_SNAPSHOT_AT__: JSON.stringify(metadata.snapshotAt),
}
