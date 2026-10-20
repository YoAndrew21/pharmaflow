# Local development with Docker Compose

This is a local development environment, not a public deployment configuration.
Install Docker Desktop with Linux containers and Compose. No host Node, .NET SDK,
PostgreSQL, User Secrets, or HTTPS certificate is required for this workflow.

## Start from a clean clone

1. Copy `.env.example` to `.env` in the repository root.
2. Set two different, new local-only passwords. Use random ASCII letters/digits,
   underscores and hyphens (at least 32 characters) to avoid shell/Compose expansion.
   Do not reuse native PostgreSQL passwords. `.env` is ignored and excluded from builds.
3. Run `docker compose up --build` from the repository root.
4. Open http://localhost:5173.

Only frontend port 5173 is published, on loopback. Vite proxies `/api` to
`backend:8080`; the API connects to `postgres:5432/pharmaflow_compose`.
The native `pharmaflow_dev` database is never used. Do not override these targets.
The default Compose network provides service-name DNS; no host database port is used.

PostgreSQL 18 stores data in the `pharmaflow-compose_postgres-data` named volume,
mounted at `/var/lib/postgresql`. Its bootstrap superuser is separate from the
non-superuser application role. The application role owns this dedicated demo
database so the one-shot initializer can migrate it. This is a development tradeoff,
not a production runtime privilege recommendation.

On a new volume, PostgreSQL creates the role. Once healthy, `db-init` applies the
existing EF migration bundle, then invokes the existing Development-only `--seed`.
It exits zero only after both succeed. Backend waits for this completion; frontend
waits for backend readiness. Normal API startup never migrates or seeds.
The bundle is generated during image build; it contains only the existing migrations.

Subsequent starts keep data. Applying an already-applied migration does nothing;
the existing seed synchronizes canonical values without duplicates or deletes.
It can update edited canonical values and fails on conflicting slugs/extra canonical
child rows rather than deleting them. It is not an insert-only operation.
Changing `.env` does not rotate an existing PostgreSQL role's password: initialization
scripts run only for a new volume. Restore matching credentials or deliberately reset
only this disposable demo environment. Never alter the native database to fix Compose.

## Daily use

```sh
docker compose ps -a
docker compose logs -f
docker compose down
docker compose up --build
```

`down` preserves volumes. No automatic restart policy restarts the environment after
you stop it. PostgreSQL readiness uses `pg_isready`; API readiness checks database
connectivity and returns only Healthy/Unhealthy, not secrets or schema diagnostics.
Frontend readiness uses Node's built-in fetch. A healthy PostgreSQL process alone
does not prove application authentication; initialization and API readiness do.

Frontend files are bind-mounted with Vite HMR; `demo-data` is read-only. Linux
`node_modules` has its own named volume and is reconciled with `npm ci` at startup,
including after lockfile changes. Native Windows modules are never used by Linux.
Port 5173 is strict: stop another process using it rather than silently changing ports.
If Docker Desktop/WSL file notifications do not reach Vite, first use a WSL filesystem
checkout. Polling is deliberately not enabled by default because it increases CPU use.

Backend changes require an image rebuild (`docker compose up --build`). Canonical
data is embedded in the API at build time. Do not use `vite preview` as public hosting.
Native execution still uses the original HTTPS proxy fallback and HTTPS redirection;
only Compose explicitly disables that redirection for internal HTTP.

Credentials are runtime environment variables, never image build arguments or VITE
variables. Do not paste `docker compose config` or container inspection output:
those can contain the interpolated passwords. Use `docker compose config --quiet`
for validation. Do not enable sensitive EF logging.

## Deliberate full reset — DESTRUCTIVE

Only if you want to erase **all Compose demo data and Linux dependencies**:

```sh
docker compose down --volumes
docker compose up --build
```

This removes this Compose project's named volumes. Never use global Docker prune
commands or delete native PostgreSQL directories. Reset is never part of normal startup.
Do not reuse the Compose project name for unrelated data.

Tests stay separate: `npm test` in `frontend`, and `dotnet test PharmaFlow.slnx`
from the root. Backend tests create their own guarded disposable PostgreSQL containers;
they must not target either the Compose database or `pharmaflow_dev`.
