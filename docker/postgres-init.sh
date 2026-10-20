#!/bin/sh
# Sourced by the official entrypoint on a new volume. Never interpolate a password into SQL.
set -eu
psql --username "$POSTGRES_USER" --dbname pharmaflow_compose --no-psqlrc --set ON_ERROR_STOP=1 <<'SQL'
\getenv app_password PHARMAFLOW_APP_PASSWORD
SELECT format('CREATE ROLE pharmaflow_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD %L', :'app_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'pharmaflow_app')
\gexec
ALTER DATABASE pharmaflow_compose OWNER TO pharmaflow_app;
SQL
