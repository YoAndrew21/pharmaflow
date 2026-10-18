# PharmaFlow frontend

See [the project README](../README.md) for status, boundaries, setup, and architecture.

From this directory run `npm ci`, then `npm run dev`. Quality checks: `npm run typecheck`, `npm run lint`, `npm run format:check`, and `npm run build`.

The production runtime uses `runtimeCatalogReader`, backed by native fetch and
relative `/api` URLs for the five existing read-only ASP.NET Core routes. There is
no local response fallback. Start the existing API separately using its `https`
launch profile; Vite proxies `/api` to `https://localhost:7043`. The proxy accepts
the local development certificate (`secure: false`) only in Vite's development
server configuration. Production hosting must route same-origin `/api` requests
to the API with its normal certificate validation. No database secrets belong in
frontend configuration.

This increment keeps search, filters, sorting and summaries in the existing pure
frontend domain functions, operating on validated API collections. Discovery is
not yet server-owned. Collection reads are batched, with no requests per card.
Only simultaneous reads sharing one caller's AbortSignal are deduplicated; there
is no cross-consumer cancellation or persistent response cache.

`demoMetadata.ts` reads the canonical JSON during Vite/Vitest configuration and
extracts only `snapshotAt` into a build-time constant. That timestamp is fixed
fictional demo metadata, not live API state. Canonical medication, pharmacy and
availability records are not imported into the browser runtime. The local reader
and canonical fixtures remain available for tests only.

Failures retain the existing translated error/retry states; detail 404 responses
remain successful not-found results. Favorites remain browser-local with the
existing storage format. IDs are reconciled only after complete successful reads;
failed, invalid or cancelled requests never prune them. Each hook lifecycle owns
its AbortController and ignores obsolete results even if a reader ignores abort.

Run `npm test` for the complete suite, including native-fetch mocks exercising
all six default page readers, response validation, cancellation, stale requests,
Favorites safety and the browser entry import graph. These tests do not contact
the developer database. Backend regression tests use disposable Testcontainers.
