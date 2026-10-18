# Isolated PostgreSQL tests

Run `dotnet test PharmaFlow.slnx` with Docker Desktop running Linux containers.
The suite uses Testcontainers.PostgreSql 4.16.0 and `postgres:18`; the daemon pulls
the PostgreSQL and resource-reaper images when needed. No Compose setup or locally
installed PostgreSQL is used. Docker being unavailable is a failure, never a reason
to skip silently or fall back to a developer database.

`PostgresFixture` owns one disposable container per xUnit collection lifetime. It
uses a random host port, a `pharmaflow_test_<GUID>` database, and a generated
test-only password. It applies the real InitialCreate migration through the
application's DI-resolved Npgsql DbContext, then invokes the existing canonical
seeder. `SeedCanonicalAsync` supports deliberate repeat seeding inside that fixture.
The unchanged Phase B HTTP assertions now run against production database-backed
read services using this collection and its factory.
Tests sharing this database must coordinate mutations; a separate fixture/container
is needed for independent empty-database or destructive scenarios.

`PostgresApiFactory` preserves the production DbContext and scoped database-reader
registrations. It replaces all application configuration providers with
explicit in-memory container settings for BOTH `ConnectionStrings:PharmaFlow`
(without its password) and `Database:Password`. User Secrets, environment variables,
and appsettings cannot supply a fallback. Application logging is disabled in this
host, and initialization failures expose only a stage, exception type, and safe
origin information, never credentials or container environment dumps.

Before initialization and each EF connection opening, the test guard checks the
container database name, host, mapped port, user, and password. `pharmaflow_dev` and
unidentified destinations fail closed. Npgsql hides passwords after first opening;
only the same previously verified connection object may subsequently omit its
public password, and its destination is still checked every time. Credentials are
not restored into the actual connection or logged.

The existing offline `ApiFactory` also replaces external configuration with an
unreachable test-only target and explicitly substitutes the retained in-memory
readers for offline metadata/seed-startup tests. It is not the factory used by the
PostgreSQL-backed endpoint contract tests.

`DatabaseReadServiceTests` owns a separate fixture/container for mutations and empty
database scenarios. It clears only that positively identified test database before
each case, then reuses the canonical seed. It checks changed PostgreSQL values,
missing observations, reverse insertion, ordinal fallback for extra test records,
empty results, cancellation, and injected query failures. Its command interceptor
proves each read operation runs one async SELECT and leaves no entities tracked.
It also proves production startup/reader construction execute no SQL. A specialized
factory can add command instrumentation without changing production registrations.

Fixture cleanup disposes the application before removing the container, including
initialization failures. Testcontainers' default resource reaper remains enabled
as additional cleanup protection. Images may remain cached; containers and test
databases are disposable. No production source, schema, migration, or developer
database is changed by this infrastructure.
