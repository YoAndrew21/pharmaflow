# Canonical demo database seed

`demo-data/catalog.json` is the single source of fictional medication presentations,
ingredients, branches, weekly opening intervals, and recorded availability. The API
embeds that file at build time; rebuild after an approved dataset change. No separate
fixture or EF `HasData` copy is maintained.

Seeding is explicit and Development-only. Normal API startup never invokes it and
the public API continues to use the Phase B in-memory services.

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

Run the offline regression suite with `dotnet test PharmaFlow.slnx`. Seed tests use
the unchanged Npgsql model, canonical JSON, EF tracking, and a connection-rejecting
interceptor. They do not use a real database or an EF InMemory/SQLite replacement.
They verify mapping, invalid inputs, repeat synchronization, conflict preservation,
and failure before transaction opening. Live PostgreSQL commit/rollback and concurrent
execution remain separate integration checks requiring an explicitly isolated database;
these tests do not claim to verify them.
