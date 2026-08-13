#!/usr/bin/env bash
set -Eeuo pipefail

# WP2-DB-01 bootstrap-only PostgreSQL role and privilege provisioning.
#
# docker-entrypoint-initdb.d scripts run exactly once, only while PostgreSQL initializes an empty
# data directory, under the POSTGRES_USER bootstrap superuser. This is the sole place normal
# Migrator and API runtime credentials, roles and default privileges are established. Neither the
# Migrator nor the API ever creates or alters a role during ordinary execution, and the bootstrap
# credential itself is never wired into either service.

: "${MBS_MIGRATOR_ROLE:?MBS_MIGRATOR_ROLE is required}"
: "${MBS_API_ROLE:?MBS_API_ROLE is required}"
: "${MBS_MIGRATOR_PASSWORD_FILE:?MBS_MIGRATOR_PASSWORD_FILE is required}"
: "${MBS_API_PASSWORD_FILE:?MBS_API_PASSWORD_FILE is required}"
: "${POSTGRES_USER:?POSTGRES_USER is required}"
: "${POSTGRES_DB:?POSTGRES_DB is required}"

[[ -r "$MBS_MIGRATOR_PASSWORD_FILE" ]] || {
  printf 'Bootstrap configuration error: Migrator secret file is unavailable.\n' >&2
  exit 78
}
[[ -r "$MBS_API_PASSWORD_FILE" ]] || {
  printf 'Bootstrap configuration error: API runtime secret file is unavailable.\n' >&2
  exit 78
}

# Escapes a value for placement inside a single-quoted SQL string literal.
sql_literal() {
  printf '%s' "$1" | sed "s/'/''/g"
}

migrator_password="$(<"$MBS_MIGRATOR_PASSWORD_FILE")"
api_password="$(<"$MBS_API_PASSWORD_FILE")"

[[ -n "$migrator_password" ]] || {
  printf 'Bootstrap configuration error: Migrator secret file is empty.\n' >&2
  exit 78
}
[[ -n "$api_password" ]] || {
  printf 'Bootstrap configuration error: API runtime secret file is empty.\n' >&2
  exit 78
}
[[ "$migrator_password" != "$api_password" ]] || {
  unset migrator_password api_password
  printf 'Bootstrap configuration error: Migrator and API runtime credentials must be distinct.\n' >&2
  printf 'ORACLE_SIGNATURE=equal-database-credential-values\n' >&2
  exit 78
}

migrator_password_literal="$(sql_literal "$migrator_password")"
api_password_literal="$(sql_literal "$api_password")"
unset migrator_password api_password

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-SQL
    -- Least-privilege Migrator principal: may connect and perform DDL in schema "public" (EF
    -- migrations, required application DDL, EF migration-history maintenance) but never
    -- SUPERUSER, CREATEDB, CREATEROLE, REPLICATION or BYPASSRLS, and never creates/alters roles
    -- during normal execution -- that authority stops here, at bootstrap.
    CREATE ROLE "${MBS_MIGRATOR_ROLE}" LOGIN PASSWORD '${migrator_password_literal}'
        NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;

    -- Least-privilege API runtime principal: may connect and use schema "public" but has no DDL,
    -- no role administration and no migration-history mutation capability. DML on tables the
    -- Migrator creates (including future application tables) is delivered exclusively through the
    -- default-privilege rule below, never through direct ownership or ad hoc grants.
    CREATE ROLE "${MBS_API_ROLE}" LOGIN PASSWORD '${api_password_literal}'
        NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;

    GRANT CONNECT ON DATABASE "${POSTGRES_DB}" TO "${MBS_MIGRATOR_ROLE}";
    GRANT CONNECT ON DATABASE "${POSTGRES_DB}" TO "${MBS_API_ROLE}";

    GRANT USAGE, CREATE ON SCHEMA public TO "${MBS_MIGRATOR_ROLE}";
    GRANT USAGE ON SCHEMA public TO "${MBS_API_ROLE}";

    -- Every table the Migrator subsequently creates (EF migration history included) automatically
    -- grants the API runtime SELECT/INSERT/UPDATE/DELETE. The Migrator narrows this back down to
    -- SELECT-only on the EF migration-history table specifically, immediately after each run, so
    -- the API can never mutate migration history even though it can perform ordinary application
    -- DML on every other Migrator-owned table.
    ALTER DEFAULT PRIVILEGES FOR ROLE "${MBS_MIGRATOR_ROLE}" IN SCHEMA public
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO "${MBS_API_ROLE}";
SQL

unset migrator_password_literal api_password_literal

printf 'WP2-DB-01 bootstrap: Migrator and API runtime roles provisioned.\n'
