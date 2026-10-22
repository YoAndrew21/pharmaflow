# Canonical demo database seed

`demo-data/catalog.json` is the single source of fictional medication presentations,
ingredients, branches, weekly opening intervals, and recorded availability. The API
embeds that file at build time; rebuild after an approved dataset change. No separate
fixture or EF `HasData` copy is maintained.

The API's explicit `--seed` action is Development-only. Normal API startup never
seeds or migrates; public API requests use scoped database-backed read services.
Production seeding is available only through the separate explicit
[database release tooling](../docker/DATABASE_RELEASE.md), never web startup.

After the schema has been migrated separately, and the Development connection and
separate database password have been configured locally, run from the repository root:

```powershell
dotnet run --project backend/PharmaFlow.Api --launch-profile http -- --seed
```

The seed command executes once and exits without starting the HTTP server. It never
applies migrations or calls `EnsureCreated`. Do not run it against a database unless
writing the fictional demo dataset is explicitly intended. This implementation task
does not execute that command or seed `pharmaflow_dev`.

The canonical resource is validated before database access. The seeder then loads
existing rows in a serializable transaction, inserts missing canonical keys and
updates scalar values at known canonical keys. An unchanged second run produces no
duplicate rows. Unrelated keys and observations absent from the canonical source are
left untouched. A canonical slug owned by a different ID, or extra ingredient/hour
rows under a canonical parent, causes a clear failure rather than deleting those
rows. Such conflicts require a separate deliberate review. There are no deletes.

Only recorded availability pairs are inserted. Null prices remain null, including
both price columns, and unavailable observations may remain priced. UTC snapshot
timestamps are parsed with zero offset for PostgreSQL `timestamptz`. Closed days have
zero intervals; split schedules retain ISO weekdays and zero-based positions.

One `SaveChanges` and the commit occur inside the transaction; disposal rolls back
uncommitted changes on failure. Concurrent seeds may receive a serialization failure;
rerun deliberately after reviewing the failure rather than applying partial changes.

Run the complete regression suite with `dotnet test PharmaFlow.slnx` and Docker Desktop
running Linux containers. Offline seed mapping/tracking tests use
the unchanged Npgsql model, canonical JSON, EF tracking, and a connection-rejecting
interceptor. They do not use a real database or an EF InMemory/SQLite replacement.
They verify mapping, invalid inputs, repeat synchronization, conflict preservation,
and failure before transaction opening. Live PostgreSQL commit/rollback and concurrent
execution remain separate integration checks requiring an explicitly isolated database;
the offline tests do not claim to verify them. The isolated PostgreSQL fixture also
exercises the real migration and canonical seeder; it never uses `pharmaflow_dev`.

## PostgreSQL-backed API reads

The five existing GET routes use async EF Core queries with cancellation tokens,
`AsNoTracking`, and one SQL query per operation. Medication queries load ingredients
in the same query; pharmacy queries load intervals in the same query. Mapping returns
only the existing API models. Detail lookups require exact slug equality using an
ordinary equality predicate plus PostgreSQL `C` collation equality. No public write,
search, or filtering routes were added. Availability query strings remain ignored.

`CanonicalCatalogOrder` reads only identity/rank metadata from the embedded canonical
source. It does not supply response values or records, fill missing database values,
or hide database failures. Collections place canonical keys first in canonical order;
unknown medication/branch IDs follow in ordinal ID order, and unknown availability
pairs follow in ordinal medication-ID then branch-ID order. Missing database rows
remain absent, including when all collections are empty. Ingredients and opening
intervals use persisted positions; all seven weekdays remain present. Times retain
the existing `HH:mm` and UTC whole-second `yyyy-MM-ddTHH:mm:ssZ` response formats.

The retained in-memory readers are used only by an explicit offline test factory.
HTTP contract tests run against production registrations in disposable PostgreSQL 18
containers. Mutation/empty tests use a separate fixture and reset only their disposable
database; the canonical contract fixture is not modified by those scenarios. See
`PharmaFlow.Api.Tests/Infrastructure/README.md` for isolation and cleanup details.
