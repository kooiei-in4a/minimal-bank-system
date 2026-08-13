#!/usr/bin/env bash
set -Eeuo pipefail

for required_name in \
  POSTGRES_HOST \
  POSTGRES_PORT \
  POSTGRES_DATABASE \
  POSTGRES_USERNAME \
  MBS_BOOTSTRAP_PASSWORD_FILE \
  MBS_MIGRATOR_PASSWORD_FILE \
  MBS_RUNTIME_PASSWORD_FILE; do
  if [[ -z "${!required_name:-}" ]]; then
    printf 'Database provisioning configuration error: %s is required.\n' "$required_name" >&2
    exit 78
  fi
done

read_secret() {
  local variable_name="$1"
  local secret_file="${!variable_name}"
  if [[ ! -r "$secret_file" ]]; then
    printf 'Database provisioning configuration error: %s is unavailable.\n' "$variable_name" >&2
    exit 78
  fi

  local secret_value
  secret_value="$(<"$secret_file")"
  if [[ -z "$secret_value" ]]; then
    printf 'Database provisioning configuration error: %s is empty.\n' "$variable_name" >&2
    exit 78
  fi
  printf '%s' "$secret_value"
}

bootstrap_password="$(read_secret MBS_BOOTSTRAP_PASSWORD_FILE)"
migrator_password="$(read_secret MBS_MIGRATOR_PASSWORD_FILE)"
runtime_password="$(read_secret MBS_RUNTIME_PASSWORD_FILE)"

export PGPASSWORD="$bootstrap_password"
psql_args=(
  --host "$POSTGRES_HOST"
  --port "$POSTGRES_PORT"
  --username "$POSTGRES_USERNAME"
  --dbname "$POSTGRES_DATABASE"
  --no-password
  --set ON_ERROR_STOP=1
)

psql "${psql_args[@]}" <<'SQL'
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'mbs_migrator') THEN
        CREATE ROLE mbs_migrator LOGIN;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'mbs_runtime') THEN
        CREATE ROLE mbs_runtime LOGIN;
    END IF;
END
$$;

ALTER ROLE mbs_migrator LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
ALTER ROLE mbs_runtime LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;

REVOKE ALL PRIVILEGES ON DATABASE minimal_bank FROM PUBLIC;
GRANT CONNECT ON DATABASE minimal_bank TO mbs_migrator;
GRANT CONNECT ON DATABASE minimal_bank TO mbs_runtime;

REVOKE ALL PRIVILEGES ON SCHEMA public FROM PUBLIC;
GRANT USAGE, CREATE ON SCHEMA public TO mbs_migrator;
GRANT USAGE ON SCHEMA public TO mbs_runtime;

ALTER DEFAULT PRIVILEGES FOR ROLE mbs_migrator IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO mbs_runtime;
ALTER DEFAULT PRIVILEGES FOR ROLE mbs_migrator IN SCHEMA public
    GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO mbs_runtime;

DO $$
BEGIN
    IF to_regclass('public."__EFMigrationsHistory"') IS NOT NULL THEN
        REVOKE ALL PRIVILEGES ON TABLE public."__EFMigrationsHistory" FROM mbs_runtime;
        GRANT SELECT ON TABLE public."__EFMigrationsHistory" TO mbs_runtime;
    END IF;
END
$$;
SQL

set_role_password() {
  local role_name="$1"
  local role_password="$2"
  printf '%s\n%s\n' "$role_password" "$role_password" |
    psql "${psql_args[@]}" --command="\\password $role_name" >/dev/null
}

set_role_password mbs_migrator "$migrator_password"
set_role_password mbs_runtime "$runtime_password"

unset PGPASSWORD bootstrap_password migrator_password runtime_password
printf 'DATABASE_PROVISIONING: PASS\n'
