#!/bin/sh
set -eu

# Bootstrap / provisioning authority only. The official PostgreSQL image runs this once during
# initdb as POSTGRES_USER (mbs_bootstrap). The API and the normal Migrator never execute it and
# never receive the bootstrap credential. POSIX sh so the image can source this file.

if [ -z "${POSTGRES_USER:-}" ] || [ -z "${POSTGRES_DB:-}" ]; then
  printf 'bootstrap provisioning error: POSTGRES_USER and POSTGRES_DB are required.\n' >&2
  exit 1
fi

echo "$POSTGRES_DB" | grep -Eq '^[A-Za-z_][A-Za-z0-9_]*$' || {
  printf 'bootstrap provisioning error: POSTGRES_DB is not a safe identifier.\n' >&2
  exit 1
}

migrator_password_file="${MBS_MIGRATOR_PASSWORD_FILE:-/run/secrets/migrator_password}"
api_password_file="${MBS_API_PASSWORD_FILE:-/run/secrets/api_password}"

if [ ! -r "$migrator_password_file" ]; then
  printf 'bootstrap provisioning error: migrator password file is unavailable.\n' >&2
  exit 1
fi

if [ ! -r "$api_password_file" ]; then
  printf 'bootstrap provisioning error: API password file is unavailable.\n' >&2
  exit 1
fi

migrator_password="$(cat "$migrator_password_file")"
api_password="$(cat "$api_password_file")"

if [ -z "$migrator_password" ]; then
  printf 'bootstrap provisioning error: migrator password file is empty.\n' >&2
  exit 1
fi

if [ -z "$api_password" ]; then
  printf 'bootstrap provisioning error: API password file is empty.\n' >&2
  exit 1
fi

if [ "$migrator_password" = "$api_password" ]; then
  printf 'bootstrap provisioning error: migrator and API credentials must be distinct.\n' >&2
  exit 1
fi

dollar='$'

psql -v ON_ERROR_STOP=1 \
  --username "$POSTGRES_USER" \
  --dbname "$POSTGRES_DB" \
  --set=migrator_password="$migrator_password" \
  --set=api_password="$api_password" \
  <<SQL
CREATE ROLE mbs_migrator LOGIN
  PASSWORD :'migrator_password'
  NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;

CREATE ROLE mbs_api LOGIN
  PASSWORD :'api_password'
  NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;

REVOKE ALL ON DATABASE ${POSTGRES_DB} FROM PUBLIC;
GRANT CONNECT ON DATABASE ${POSTGRES_DB} TO mbs_migrator;
GRANT CONNECT ON DATABASE ${POSTGRES_DB} TO mbs_api;

REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT USAGE, CREATE ON SCHEMA public TO mbs_migrator;
GRANT USAGE ON SCHEMA public TO mbs_api;

ALTER DEFAULT PRIVILEGES FOR ROLE mbs_migrator IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO mbs_api;
ALTER DEFAULT PRIVILEGES FOR ROLE mbs_migrator IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO mbs_api;

CREATE FUNCTION public.mbs_protect_ef_migration_history()
RETURNS event_trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog
AS ${dollar}${dollar}
BEGIN
  IF NOT EXISTS (
    SELECT 1
    FROM pg_catalog.pg_class c
    JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = 'public'
      AND c.relname = '__EFMigrationsHistory'
      AND c.relkind = 'r'
  ) THEN
    RETURN;
  END IF;
  EXECUTE 'REVOKE INSERT, UPDATE, DELETE ON TABLE public."__EFMigrationsHistory" FROM mbs_api';
  EXECUTE 'GRANT SELECT ON TABLE public."__EFMigrationsHistory" TO mbs_api';
END;
${dollar}${dollar};

REVOKE ALL ON FUNCTION public.mbs_protect_ef_migration_history() FROM PUBLIC;

CREATE EVENT TRIGGER mbs_protect_ef_migration_history
  ON ddl_command_end
  WHEN TAG IN ('CREATE TABLE')
  EXECUTE FUNCTION public.mbs_protect_ef_migration_history();
SQL

unset migrator_password api_password
