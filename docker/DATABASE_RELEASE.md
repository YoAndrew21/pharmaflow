# Explicit database release (local validation completed; Neon not contacted)

The public API performs only SELECT queries: medication/ingredient reads, pharmacy/
opening-interval reads, and recorded availability. It needs no writes or sequences.
Its normal Production startup still neither migrates nor seeds. The existing API
`--seed` remains Development-only and is not weakened by this command.

## Artifact and explicit action

```sh
docker build -f backend/PharmaFlow.Api/Dockerfile --target database-release \
  -t pharmaflow-database-release .
docker run --rm --env-file .env.release.local \
  pharmaflow-database-release --release-database
```

The root build context is required. `.env.release.local` must remain ignored, outside
the build context contents, and must never be printed or committed. No environment
file is supplied in Git. Running the image without the exact action exits nonzero.
Do not run this against a remote database until that release is separately authorized.

The `database-release` target is a separate non-root executable, not the web server.
It reuses **the existing EF bundle** from the backend Dockerfile's `build` stage,
then calls the existing `CanonicalDatabaseSeeder`, then applies/verifies runtime ACLs.
It has no HTTP listener and exits. No SDK or tools are added to the web runtime;
the final default `runtime` and Development `db-init` targets retain their behavior.
`Dockerfile.production` does not copy or invoke the release executable or bundle.

## Runtime environment for the release only

| Key                                    | Meaning                                                                                      |
| -------------------------------------- | -------------------------------------------------------------------------------------------- |
| `ConnectionStrings__PharmaFlow`        | Base connection settings for the **release owner**, including TLS settings, with no password |
| `Database__Password`                   | Separate release-owner password secret                                                       |
| `DatabaseRelease__ExpectedHost`        | Exact host matching the parsed base settings                                                 |
| `DatabaseRelease__ExpectedDatabase`    | Exact intended database name                                                                 |
| `DatabaseRelease__ExpectedReleaseRole` | Exact intended release login/database owner                                                  |
| `DatabaseRelease__RuntimeRole`         | Already-provisioned, distinct runtime login to restrict                                      |

Use a **direct PostgreSQL endpoint**, not a transaction pooler, for release. A session
advisory lock serializes this job's stages; the bundle also retains EF migration locking.
The command loads environment configuration only, never local User Secrets. It uses
`NpgsqlConnectionStringBuilder.Password`; secrets never enter build arguments or CLI
arguments. The bundle receives the same base/password configuration in its environment
and uses the API's actual DbContext registration in Production. Bundle stdout/stderr
is drained without forwarding it. Logs contain stages, fixed safety codes, exception
types and SQLSTATE only, not raw exception messages or connection strings.

Before any migration, the command verifies current database/user and database ownership.
The runtime role must exist, allow login, own no databases/schemas/relations, have no
elevated flags or role memberships, and have no unrelated data/schema-create grants.
It does not create roles or set passwords. Provision roles through the separately
authorized provider/admin workflow. SQL identifiers are quoted and catalog values
are parameterized. The release action never constructs password-bearing DDL; the
separate provisioning modes below have their own explicit password policy.

## Permission policy — dedicated application database

This policy is for a **dedicated PharmaFlow database**, not a shared application schema.
It intentionally removes PUBLIC CREATE/TEMP and public-schema data privileges that
could otherwise bypass a runtime-role revoke. Review this impact before a real release.

Runtime receives:

- CONNECT to the target DB and USAGE of schema `public`.
- SELECT, without grant option, on exactly `medications`,
  `medication_active_ingredients`, `pharmacy_branches`, `opening_intervals`, `availability`.
- No INSERT/UPDATE/DELETE/TRUNCATE/REFERENCES/TRIGGER, schema/database CREATE,
  temporary tables, sequences, or migration-history access.

Effective permissions are checked before the ACL transaction commits, including
column-level write/grant-option privileges. Revocation must remove these grants;
any remaining unsafe effective privilege causes failure rather than a false success.
Release-owner default table/sequence grants to PUBLIC/runtime are revoked globally
and in `public`. **Future tables receive no automatic runtime SELECT**: extend the
explicit read allowlist and its validation only after reviewing a future API change.
Objects created by a different owner are outside this default-privilege policy.

The release role retains database/object ownership for future migrations and canonical
synchronization. The web process must receive only runtime credentials; no owner
secret, release environment file, or release confirmation variables belong in Render.

## Failure and repeat execution

Migration, seed or ACL failure returns nonzero and prevents further stages. Migration,
seed and ACL are **not one distributed/whole-release transaction**: an applied migration
can remain after seed failure. The canonical seed retains its serializable transaction,
conflict checks, no-delete behavior and idempotency. ACL updates have their own atomic
transaction and validation. Stop the deployment, inspect the safe stage/code, resolve
the problem deliberately and rerun; never delete/recreate the DB automatically.

A second unchanged release leaves one migration-history entry and the same canonical
9 medications / 10 ingredients / 3 branches / 18 intervals / 21 observations. Canonical
values may be updated on deliberate later releases; this is not an insert-only seed.
Unrelated data is not deleted. No release action is attached to a web restart.

## Local verification performed

Validation used new PostgreSQL 18 Docker instances on an internal network, independent
of `pharmaflow_dev` and persistent Compose data, with separate non-superuser owner and
runtime roles. Checks covered:

- Empty DB and normal Production web startup creating zero tables.
- Real bundle, canonical seed, ACLs and a second release with unchanged row values/xmin.
- All five real Production GET endpoints running under runtime credentials, matching
  the canonical JSON, plus readiness and unknown-resource 404.
- Runtime CREATE/ALTER/DROP/INSERT/UPDATE/DELETE, TRUNCATE, CREATE SCHEMA, TEMP tables
  and migration-history reads denied; owner DDL/DML allowed within rollback probes.
- Future table/sequence grants denied by default.
- An injected extra canonical child row making seed fail nonzero without deleting it;
  the test-only row was then removed deliberately in the isolated fixture.
- No secret values in command output, image metadata or versionable files.

## Next authorized Neon/Render checkpoint

1. Confirm the dedicated DB, region/version, direct hostname, TLS trust and role names
   using actual provider settings. No endpoint/credential is invented here.
2. Provision the existing owner/release and non-owner runtime logins, checking memberships.
3. Prepare release-only secrets outside Git. Prefer `SSL Mode=VerifyFull` and preserve
   certificate verification; never reuse local credentials.
4. Build the `database-release` image from the reviewed commit and invoke the exact action
   once, with explicit target confirmation. Stop if any stage fails.
5. Verify migration history, counts, canonical values, ownership and effective runtime
   permissions. Repeat the negative permission probes with rollback where appropriate.
6. Configure Render's web `runtime` from `Dockerfile.production` with Production, its
   port/proxy settings, a base connection using **the runtime login**, and only that
   login's separate password. Do not give Render the owner password.
7. Verify public reads/readiness. Future releases remain explicit jobs, never startup hooks.

No Neon/Render resource, remote connection, or deployment was used for this checkpoint.

## Initial runtime role provisioning (explicit, separate action)

Build the existing artifact:

```sh
docker build --target database-release -t pharmaflow-database-release -f backend/PharmaFlow.Api/Dockerfile .
```

Supply the owner connection and expected target keys documented above, plus
`RuntimeRoleProvisioning__Password`, through a private ignored environment file.
Never put passwords in shell arguments or SQL Editor. The initial runtime secret
must contain at least 48 ASCII characters, exclusively `[A-Za-z0-9_-]`.

```sh
docker run --rm --env-file .env.provision.local pharmaflow-database-release --provision-runtime-role
```

This action does not migrate, seed, grant application access, or start HTTP.
It creates a conventional SQL LOGIN role with NOSUPERUSER, NOCREATEDB,
NOCREATEROLE, NOREPLICATION, NOBYPASSRLS and NOINHERIT. It never grants
`neon_superuser`. Owner identity, database ownership and CREATEROLE capability
are checked independently. Names are limited to lowercase ASCII identifiers
starting with a letter, up to 63 characters, excluding `pg_`, and are quoted.

### Explicit password mode

`RuntimeRoleProvisioning__PasswordMode` accepts exactly `ScramVerifier` or
`NeonPlaintext`. An absent key defaults to `ScramVerifier`, preserving the original
PostgreSQL-compatible behavior. Empty, misspelled or unknown values fail. There
is no automatic mode fallback or automatic retry on any error.

- **ScramVerifier:** derives a SCRAM-SHA-256 verifier locally using a random
  16-byte salt and 16384 PBKDF2 iterations. The original password stays out of SQL.
  TLS/GSS settings retain their existing generic PostgreSQL behavior.
- **NeonPlaintext:** explicit provider-specific choice. Neon documents that hashed
  SQL passwords are unsupported, so the original password is used only in the
  CREATE ROLE password literal. This mode requires `SSL Mode=VerifyFull` before
  opening a connection, sets `GssEncryptionMode=Disable` on the in-memory builder,
  and executes `SET LOCAL password_encryption = 'scram-sha-256'` inside the creation
  transaction. It never downgrades TLS. No Kerberos library is needed for this
  TLS/password flow; GSS is unrelated to the previous XX000.

The plaintext literal helper first enforces the existing >=48-character ASCII
`[A-Za-z0-9_-]` policy. It also escapes backslashes and quotes in an E-literal as
future-proof defense, even though both characters are currently rejected.
Identifiers remain strictly validated and quoted with QuoteIdentifier. PASSWORD
parameters are not supported by PostgreSQL's CREATE ROLE grammar. No function,
control-plane bypass, API key or administrative runtime membership is introduced.

The provisioning data source explicitly disables command/batch SQL tracing,
physical-open tracing and parameter logging, and uses a null logger factory.
Only fixed status/error metadata is reported. Do not enable external profilers,
statement capture, memory dumps or other instrumentation that records secrets.
The original password and constructed CommandText exist in managed memory; .NET
strings cannot be reliably zeroed. The verifier is sensitive too.

TLS protects transport, not server-side visibility: PostgreSQL activity,
provider-managed logs/tracing and authorized operators may see password-bearing
SQL. Do not promise provider redaction. Environment variables are also visible
to privileged Docker/host operators. Use a short-lived `--rm` container. A temporary
ignored local env-file is a pragmatic disk-storage exception, not memory-only
secret delivery. Never create additional credential artifacts or put values in
CLI arguments/history. After successful provisioning and read-only verification,
remove `RuntimeRoleProvisioning__Password` from the bootstrap file/environment;
retain the runtime credential only in the application's private operational
configuration. Role creation is not password rotation.

Neon reference: https://neon.com/docs/manage/roles#manage-roles-with-sql
No remote validation is implied by local PostgreSQL tests.

Creation, unchanged-role verification and security checks run in one transaction
under a role-name advisory transaction lock. Cooperating callers must connect to
the same administrative database because advisory locks are database-scoped.
A duplicate-name race from other tools fails safely. PUBLIC-derived CREATE or
new-role data privileges cause rollback; provisioning does not repair shared ACLs.
The creator can receive administrative membership *in* the newly created role;
this does not make the runtime role a member of the creator or an elevated role.

Cases:
- Missing role + valid secret: create, verify, commit.
- Safe existing role + absent secret: verify only; no password change.
- Unsafe existing role: fail without repair.
- Missing role + absent/invalid secret: fail without creation.
- Existing role + supplied secret (including empty): fail without rotation.

After successful provisioning, remove the provisioning secret from subsequent
job environments. The regular `--release-database` action rejects that key,
including an empty value, and still requires a safe existing role. No bootstrap
secret is forwarded to the migration child. Run the release separately with its
owner-only environment file. A later release failure does not delete the role.
A failed pre-commit transaction rolls creation back. A failure during CommitAsync
reports `commit-outcome-unknown-verify-before-retry`. Run the explicit read-only
`--verify-runtime-role` action before any retry decision;
never automatically drop a role to compensate.

Before the first remote use, validate the artifact against disposable PostgreSQL
18, including authentication, rollback, concurrency, permissions, the real bundle,
canonical counts 9/10/3/18/21 and repeat-release idempotency. SQL-created Neon roles
avoid Console/API/CLI automatic elevated membership. No remote execution is
implied by building or testing this artifact. Normal Production web startup never
provisions roles, migrates, seeds or invokes the release command.

## Read-only verification after initial provisioning / uncertain commit

The explicit `--verify-runtime-role` action checks the state immediately after
initial provisioning and **before** database release. It is not a post-release
runtime permission checker: even SELECT on application data makes this initial
profile UNSAFE. It neither authenticates as runtime nor proves password validity.
It does not establish who created the observed role or the historical outcome of
an earlier transaction; it verifies the current committed catalog snapshot.

```sh
docker build --target database-release -t pharmaflow-database-release -f backend/PharmaFlow.Api/Dockerfile .
docker run --rm --env-file .env.release.local pharmaflow-database-release --verify-runtime-role
```

Do not run provisioning or release to diagnose an uncertain provisioning commit.
The verifier opens one owner connection, confirms the explicit expected target,
current user, current database, database ownership and owner role capability.
It uses a REPEATABLE READ transaction explicitly set READ ONLY. Catalog queries
and built-in privilege checks are parameterized; there are no advisory locks,
role/password changes, migrations, seed, bundle processes, ACL changes or repairs.
The read transaction ends with rollback. Errors, cancellation or cleanup failures
produce UNSAFE, never NOT_FOUND. Configuration is environment-only, as before.
`RuntimeRoleProvisioning__Password` is deliberately never read or used by this
action; supplying it does not enable creation, rotation, authentication or repair.
Prefer leaving it out of verification environments to minimize secret exposure.

Output is limited to 21 fixed YES/NO checks and one final line:
`runtime_role_verification: SAFE`, `UNSAFE`, or `NOT_FOUND`.
Exit codes are 0 (SAFE), 1 (UNSAFE/incomplete), 2 (confirmed NOT_FOUND).
No identifiers, configuration values, SQL, exception details or secrets are
reported. Existing native GSSAPI diagnostic behavior is unchanged.

SAFE requires LOGIN, NOINHERIT, no elevated flags, no direct runtime memberships,
no database/schema/relation ownership, no effective CREATE, and no application
data privileges, including column/sequence and PUBLIC-derived permissions.
Database ownership checks are cluster-wide; schemas, relations and object
privileges are inspected in the connected database. No password catalog is read.
The shared read-only safety evaluator also serves provisioning and legacy release
preflight, retaining their existing profiles and error codes. The verifier does not
read PasswordMode or change its READ-ONLY contract. Its own GSS/TLS settings continue
to come from the supplied base connection configuration.

## Provisioning commit and cleanup outcomes

The lifecycle distinguishes `before_commit`, `commit_in_progress`,
`commit_acknowledged`, `transaction_disposal`, `connection_disposal` and `reporting`.
Only a failure during CommitAsync is labeled
`commit-outcome-unknown-verify-before-retry`, regardless of SQLSTATE. Confirmation
is recorded immediately when CommitAsync returns. Cleanup or reporting failures
following that point never turn a confirmed commit into an ambiguous one.
Transaction and connection/data-source cleanup are attempted independently.
Secondary cleanup/reporting errors never replace a primary failure. Reporting
runs after cleanup and is never retried; if the output sink itself throws, the
process exits nonzero without forwarding that exception. No compensating DROP
or automatic retry is performed. Do not infer commit outcome from XX000 alone.

Local validation uses disposable PostgreSQL 18 with disposable TLS certificates,
real VerifyFull authentication, boolean-only SCRAM storage checks, A/B/C/D/E,
rollback/concurrency/advisory lock, and deterministic lifecycle fault injection.
It does not reproduce Neon's managed control plane. Normal Production web startup,
release migrations/seed/ACL and the read-only verifier remain separate actions.

## Independent post-release verification

`--verify-database-release` audits the committed state **after** a successful
database release. It is a separate action, not a release retry or repair. Build
the `database-release` target normally; when remote verification is explicitly
authorized, the image accepts this action with the existing environment-only
owner configuration:

```sh
docker run --rm --env-file .env.release.local pharmaflow-database-release:latest --verify-database-release
```

This command is documented for a later operator-authorized run; local validation
does not contact Neon or Render. Do not supply the provisioning password: this
action never reads it or PasswordMode, and never authenticates as runtime.
It requires the existing base connection, separate owner password and explicit
expected host/database/owner/runtime configuration. Use VerifyFull with the
direct remote endpoint. The verifier disables GSS for its password-only owner
connection; it does not weaken or override the supplied TLS settings.

The verifier opens one owner connection and starts REPEATABLE READ, sets READ
ONLY before audit queries, confirms both effective settings, and checks actual
database identity/ownership. All database work uses that transaction, which ends
with rollback. It does not call migrations, bundle, provisioning, seed,
SaveChanges, grants/revokes, DDL/DML or SET ROLE. It has no retry/repair path.
Its private data source disables SQL logging, parameter logging and command/open
tracing. EF supplies metadata only; row reads use direct Npgsql commands to avoid
EF command diagnostic events. No password catalog is read.

Checks include exactly InitialCreate once, no unexpected migration, the known
assembly migration set, the five actual application tables and history inventory,
application column types/nullability, PK/FK columns and delete behavior, validated
and enforced CHECK constraints, and valid unique/ordinary indexes. Expectations
come from InitialCreate operations, not a separately authored schema fixture.
The limited CHECK expression parser fails closed on unsupported syntax and
preserves boolean grouping while accepting PostgreSQL IN/ANY and BETWEEN forms.
Future migrations or new expression forms require explicit verifier review.

All five complete row sets are compared to `CanonicalSeedData.Load()` in memory,
including keys, values, null prices, ingredient/interval positions, missing
availability pairs and UTC instants. Counts must be exactly 9/10/3/18/21. No rows
are synthesized, changed or printed.

The post-release role profile requires LOGIN/NOINHERIT, no elevated flags,
memberships or ownership; CONNECT, public USAGE, and table SELECT on exactly the
five allowlisted tables. It forbids effective write/MAINTAIN/CREATE/TEMPORARY,
sequence permissions, table/column access to history or unexpected user
relations, and relevant database/schema/table/column/sequence grant options.
Checks include PUBLIC-derived privileges. Global and schema-specific table and
sequence default ACLs for all creators are checked for grants to runtime/PUBLIC,
including creators other than the release owner.
Ordinary system-catalog SELECT and built-in system views (for example the session
configuration interface) are not application data permissions; the table/column
data allowlist concerns non-system relations throughout the connected database.

Output contains only the 39 fixed YES/NO checks and exactly one final line:
`database_release_verification: SAFE` or `database_release_verification: UNSAFE`.
Exit codes: 0 for SAFE; 1 for any unsafe, failed or incomplete verification.
Uncompleted checks remain NO. Errors, cancellation, rollback or disposal failures
cannot yield SAFE; no exception messages, SQLSTATE, SQL, identifiers or values are
printed. `verification_complete: YES` means all audit steps and cleanup completed,
not that all the checks passed.

The result describes a single committed snapshot, not a guarantee against future
administrative changes. Database ownership and direct memberships are global;
schema/relation/default ACL checks concern the connected database. It is not an
audit of every SECURITY DEFINER routine, extension or managed-platform policy.
Read-only connections may still produce server logs/statistics and ordinary
internal PostgreSQL activity. This command does **not** test the runtime password,
runtime authentication, Render networking, public API or browser behavior.

Automated verification uses only disposable PostgreSQL 18 containers, with a
separate database/owner/runtime per case. Test setup may migrate/seed/grant inside
those disposable databases; the verifier itself never does. Success, deliberate
schema/data/ACL drift, unsafe defaults, errors, cancellation, transaction modes,
rejected writes, unchanged row xmin/catalog/ACL snapshots, sanitized child-process
output and disabled SQL tracing are covered locally.
