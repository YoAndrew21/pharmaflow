# Production image and hosting

The public demo runs on Render with PostgreSQL on Neon. This guide documents the
versionable image and configuration contract; provider resources and credentials
are managed outside Git. Development uses `compose.yaml` and the existing
frontend/backend Dockerfiles.

## Build and run locally

From the repository root:

```sh
docker build --no-cache -f Dockerfile.production -t pharmaflow-production-local .
```

Node 24.21.0 builds React with `npm ci` and `npm run build`. SDK 10.0.401 publishes
the API. The final ASP.NET 10.0.12 image contains the API and `wwwroot` only: no
Node, npm, SDK, Vite server, bind mounts, or frontend dependency directory. It runs
as the image's non-root `app` user. The canonical catalog remains an embedded API
resource, not an alternative response-data source in the browser.

For local validation, start the existing Compose environment first, then supply
an **ignored** `.env.production.local` runtime file with:

- `ConnectionStrings__PharmaFlow`: the existing Compose base target
  `Host=postgres;Port=5432;Database=pharmaflow_compose;Username=pharmaflow_app`.
- `Database__Password`: the matching local Compose application password.

Never commit this file or reuse native PostgreSQL, Render, or Neon credentials.

```sh
docker run --name pharmaflow-production-check --network pharmaflow-compose_default \
  --env-file .env.production.local -e PORT=8080 \
  -e Http__DisableHttpsRedirection=true -p 127.0.0.1:5180:8080 \
  pharmaflow-production-local
```

Open http://localhost:5180. This HTTP override is for **local validation**, not a
prescription for a public proxy. Stop/remove only this validation container when
finished; do not delete the Compose volume. No schema/seed operation happens at
normal API startup, and the existing `--seed` still refuses Production.

## HTTP behavior

React and `/api` share one origin. Static assets are served by ASP.NET. Only
extensionless frontend paths fall back to `index.html`; `/api`, `/health`,
`/openapi`, `/swagger`, and `/assets` are reserved, case-insensitively, and never
fall back. Unknown APIs/assets keep 404; existing API write methods keep 405.
Unknown frontend routes reach the existing React not-found screen. The local API-only
Development image has no frontend webroot and retains the Vite workflow.

Outside Development, unexpected errors produce generic HTTP 500 Problem Details,
with no exception details in the response; server logging retains diagnostics.
Responses add `nosniff`, `strict-origin-when-cross-origin`, and `X-Frame-Options: DENY`.
No complex CSP, public Development exception page, or Production OpenAPI endpoint
is enabled. `/health/ready` returns only Healthy/Unhealthy (200/503) based on DB
connectivity. It does not certify migration state or data counts. No liveness
endpoint is added without a provider-specific operational need.

## Port and trusted proxies

- Image default: `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_HTTP_PORTS=8080`.
- `PORT`, when present in Production, takes precedence and binds HTTP on all
  container interfaces. Invalid port values fail startup. Native Development
  launch profiles are unaffected; standard ASP.NET URL/port settings still work
  when `PORT` is absent.
- Proxy processing is disabled unless `ReverseProxy__Enabled=true`.
- Enabling it requires explicit valid `ReverseProxy__KnownProxies__0`, `__1`, etc.
  Only IP addresses are supported in this increment; no blanket network trust.
- Framework loopback defaults are retained. Neither KnownProxies nor KnownNetworks
  is cleared. Only forwarded client address/scheme are enabled, with one trusted hop;
  forwarded host is not enabled. Do not use the blanket
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED` switch as a shortcut.
- Forwarded headers run before HTTPS redirection. The native redirection default
  remains enabled unless explicitly disabled with the existing HTTP setting.

For each Render deployment, verify the actual ingress topology, trusted proxy addresses
or a narrowly justified network configuration, forwarded header behavior, HTTP
ingress restrictions, TLS termination, and redirect policy. If Render cannot offer
stable individual proxy IPs, a reviewed provider-specific solution is still required;
do not trust every sender just to suppress redirect loops. Verify AllowedHosts and
the public hostname there as well. Provider dashboard settings are managed outside Git.

## Render and Neon configuration contract

Render should build repository-root `Dockerfile.production`, final `runtime` target,
using the root build context. It can supply `PORT`. Required runtime settings are
Production environment, a base `ConnectionStrings__PharmaFlow`, and secret
`Database__Password`; proxy/HTTPS/host restrictions depend on the verified ingress.
Use `/health/ready` as the readiness path, and verify the provider's
healthcheck/restart semantics before choosing its monitor behavior.

Neon still requires its real hostname, port, database, username, a separate password
secret, and reviewed TLS settings, preferably `SSL Mode=VerifyFull` with certificate
validation. Do not invent endpoints or embed a password/URL into React. Choose
runtime connection pooling limits and direct versus pooled release connections after
reviewing the actual service limits. Use separate release privileges and a read-only
runtime identity. Provisioning always requires separate explicit authorization.

The existing EF migration bundle remains built by the Development backend Dockerfile;
existing migrations are unchanged. The Production runtime deliberately carries no
migration command and performs no migration or seed. The separate one-shot release artifact
uses the same reviewed migration bundle and the separate explicit runner described in
[Database release](DATABASE_RELEASE.md). It requires explicit action/target confirmation,
has no HTTP listener, and never runs on web restart. Real provider execution still
requires separate authorization. Never switch the public server to Development or
weaken the current seed guard. Canonical synchronization can update values; it is
not an insert-only operation.

## Validation and known follow-up

Validate deep-route reloads, asset loading, API 404/405, readiness, generic DB failures,
security headers, Favorites, EN/ES/JA, theme changes, trusted/untrusted forwarded
headers, configurable ports, and DB stop/recovery using only Compose PostgreSQL.
Re-run all frontend/backend tests and the Development Compose/HMR smoke after changes.
Do not test failures by deleting data. Host `pharmaflow_dev` must never be used.

On Windows bind mounts, Docker Desktop can stop forwarding filesystem events after
an engine restart even though the HMR WebSocket is connected. Validate this separately
from application routing. An opt-in, uncommitted Compose override setting the frontend
environment `CHOKIDAR_USEPOLLING=true` can recover updates (at additional CPU cost).
Prefer a WSL filesystem checkout where appropriate; polling is not enabled in the
versioned Development defaults or used by the Production image.

The existing Vite 8.3.2 dependency currently reports a high-severity development
`launch-editor` advisory in `npm audit`. Vite and its editor server are absent from
the Production runtime; no package upgrades are included in this hosting increment.
Review/update the development tooling in a separate scoped maintenance task.
