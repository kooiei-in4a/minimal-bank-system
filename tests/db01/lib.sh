#!/usr/bin/env bash
# Shared WP2-DB-01 role/credential helpers. Sourced by Compose verification scripts.
# Secret values must never be printed.

readonly DB01_DATABASE="${DB01_DATABASE:-minimal_bank}"
readonly DB01_BOOTSTRAP_ROLE="${DB01_BOOTSTRAP_ROLE:-mbs_bootstrap}"
readonly DB01_MIGRATOR_ROLE="${DB01_MIGRATOR_ROLE:-mbs_migrator}"
readonly DB01_API_ROLE="${DB01_API_ROLE:-mbs_api}"
readonly DB01_BOOTSTRAP_SECRET_FILE="${DB01_BOOTSTRAP_SECRET_FILE:-/run/secrets/bootstrap_password}"
readonly DB01_MIGRATOR_SECRET_FILE="${DB01_MIGRATOR_SECRET_FILE:-/run/secrets/migrator_password}"
readonly DB01_API_SECRET_FILE="${DB01_API_SECRET_FILE:-/run/secrets/api_password}"
readonly DB01_DML_FIXTURE_TABLE="${DB01_DML_FIXTURE_TABLE:-__db01_dml_fixture}"
readonly DB01_HISTORY_TABLE="${DB01_HISTORY_TABLE:-__EFMigrationsHistory}"

db01_secret_file_for_role() {
  case "$1" in
    "$DB01_API_ROLE") printf '%s\n' "$DB01_API_SECRET_FILE" ;;
    "$DB01_MIGRATOR_ROLE") printf '%s\n' "$DB01_MIGRATOR_SECRET_FILE" ;;
    "$DB01_BOOTSTRAP_ROLE") printf '%s\n' "$DB01_BOOTSTRAP_SECRET_FILE" ;;
    *)
      printf 'Unknown database role: %s\n' "$1" >&2
      return 1
      ;;
  esac
}

db01_require_distinct_host_secrets() {
  : "${MBS_BOOTSTRAP_PASSWORD:?MBS_BOOTSTRAP_PASSWORD is required}"
  : "${MBS_MIGRATOR_PASSWORD:?MBS_MIGRATOR_PASSWORD is required}"
  : "${MBS_API_PASSWORD:?MBS_API_PASSWORD is required}"
  [[ "$MBS_BOOTSTRAP_PASSWORD" != "$MBS_MIGRATOR_PASSWORD" ]] || {
    printf 'Bootstrap and Migrator host secrets must be distinct.\n' >&2
    return 1
  }
  [[ "$MBS_BOOTSTRAP_PASSWORD" != "$MBS_API_PASSWORD" ]] || {
    printf 'Bootstrap and API host secrets must be distinct.\n' >&2
    return 1
  }
  [[ "$MBS_MIGRATOR_PASSWORD" != "$MBS_API_PASSWORD" ]] || {
    printf 'Migrator and API host secrets must be distinct.\n' >&2
    return 1
  }
}

# Run psql as a designated role using that role's mounted secret. The password is read inside
# the postgres container and is never written to the caller stdout/stderr.
db01_psql_as() {
  local role="$1"
  local secret_file
  secret_file="$(db01_secret_file_for_role "$role")"
  shift
  db01_postgres_exec bash -c '
    set -euo pipefail
    secret_file="$1"
    role="$2"
    database="$3"
    shift 3
    export PGPASSWORD="$(<"$secret_file")"
    exec psql -h 127.0.0.1 -U "$role" -d "$database" -v ON_ERROR_STOP=1 -At "$@"
  ' bash "$secret_file" "$role" "$DB01_DATABASE" "$@"
}

db01_psql_as_allow_failure() {
  local role="$1"
  local secret_file
  secret_file="$(db01_secret_file_for_role "$role")"
  shift
  db01_postgres_exec bash -c '
    set -euo pipefail
    secret_file="$1"
    role="$2"
    database="$3"
    shift 3
    export PGPASSWORD="$(<"$secret_file")"
    set +e
    psql -h 127.0.0.1 -U "$role" -d "$database" -At "$@"
    status=$?
    set -e
    exit "$status"
  ' bash "$secret_file" "$role" "$DB01_DATABASE" "$@"
}

db01_read_history_as() {
  local role="$1"
  db01_psql_as "$role" -c "SELECT \"MigrationId\" FROM public.\"${DB01_HISTORY_TABLE}\" ORDER BY \"MigrationId\";"
}

db01_read_public_tables_as() {
  local role="$1"
  db01_psql_as "$role" -c "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name;"
}

db01_current_user_as() {
  local role="$1"
  db01_psql_as "$role" -c 'SELECT current_user;'
}

db01_role_attribute_csv() {
  local role="$1"
  db01_psql_as "$DB01_BOOTSTRAP_ROLE" -c \
    "SELECT rolname FROM pg_roles WHERE rolname = '${role}' AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication AND NOT rolbypassrls;"
}

db01_assert_no_admin_attributes() {
  local role="$1"
  local observed
  observed="$(db01_role_attribute_csv "$role")"
  [[ "$observed" == "$role" ]] || {
    printf 'Role %s has a prohibited administration attribute.\n' "$role" >&2
    return 1
  }
}

db01_relation_owner() {
  local relation="$1"
  db01_psql_as "$DB01_BOOTSTRAP_ROLE" -c \
    "SELECT pg_catalog.pg_get_userbyid(c.relowner) FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'public' AND c.relname = '${relation}';"
}

db01_drop_dml_fixture() {
  db01_psql_as "$DB01_MIGRATOR_ROLE" -c "DROP TABLE IF EXISTS public.\"${DB01_DML_FIXTURE_TABLE}\";" >/dev/null 2>/dev/null
}

db01_create_dml_fixture() {
  db01_drop_dml_fixture
  db01_psql_as "$DB01_MIGRATOR_ROLE" -c \
    "CREATE TABLE public.\"${DB01_DML_FIXTURE_TABLE}\" (id integer PRIMARY KEY, payload text NOT NULL);" >/dev/null
}

db01_assert_positive_dml() {
  local inserted updated remaining
  db01_create_dml_fixture

  db01_psql_as "$DB01_API_ROLE" -c \
    "INSERT INTO public.\"${DB01_DML_FIXTURE_TABLE}\" (id, payload) VALUES (1, 'insert-ok');" >/dev/null
  inserted="$(db01_psql_as "$DB01_API_ROLE" -c "SELECT payload FROM public.\"${DB01_DML_FIXTURE_TABLE}\" WHERE id = 1;")"
  [[ "$inserted" == 'insert-ok' ]] || {
    printf 'API runtime INSERT did not persist.\n' >&2
    db01_drop_dml_fixture
    return 1
  }

  db01_psql_as "$DB01_API_ROLE" -c \
    "UPDATE public.\"${DB01_DML_FIXTURE_TABLE}\" SET payload = 'update-ok' WHERE id = 1;" >/dev/null
  updated="$(db01_psql_as "$DB01_API_ROLE" -c "SELECT payload FROM public.\"${DB01_DML_FIXTURE_TABLE}\" WHERE id = 1;")"
  [[ "$updated" == 'update-ok' ]] || {
    printf 'API runtime UPDATE did not persist.\n' >&2
    db01_drop_dml_fixture
    return 1
  }

  db01_psql_as "$DB01_API_ROLE" -c \
    "DELETE FROM public.\"${DB01_DML_FIXTURE_TABLE}\" WHERE id = 1;" >/dev/null
  remaining="$(db01_psql_as "$DB01_API_ROLE" -c "SELECT COUNT(*)::text FROM public.\"${DB01_DML_FIXTURE_TABLE}\";")"
  [[ "$remaining" == '0' ]] || {
    printf 'API runtime DELETE did not persist.\n' >&2
    db01_drop_dml_fixture
    return 1
  }

  db01_drop_dml_fixture
}

db01_sqlstate_or_empty() {
  local role="$1"
  local sql="$2"
  db01_postgres_exec bash -c '
    set -euo pipefail
    secret_file="$1"
    role="$2"
    database="$3"
    sql="$4"
    export PGPASSWORD="$(<"$secret_file")"
    set +e
    output="$(psql -h 127.0.0.1 -U "$role" -d "$database" -v ON_ERROR_STOP=1 -v VERBOSITY=verbose -At -c "$sql" 2>&1)"
    status=$?
    set -e
    printf "%s\n" "$output"
    exit "$status"
  ' bash "$(db01_secret_file_for_role "$role")" "$role" "$DB01_DATABASE" "$sql"
}

db01_assert_statement_denied() {
  local role="$1"
  local sql="$2"
  local label="$3"
  local output status
  set +e
  output="$(db01_sqlstate_or_empty "$role" "$sql" 2>&1)"
  status=$?
  set -e
  (( status != 0 )) || {
    printf 'Prohibited operation unexpectedly succeeded (%s).\n' "$label" >&2
    return 1
  }
  [[ "$output" != *'Connection refused'* && "$output" != *'password authentication failed'* && "$output" != *'no password supplied'* ]] || {
    printf 'Prohibited operation failed as a generic connection/auth error (%s).\n' "$label" >&2
    return 1
  }
  [[ "$output" == *'permission denied'* || "$output" == *'42501'* || "$output" == *'must be superuser'* || "$output" == *'must be owner'* ]] || {
    printf 'Prohibited operation failed without a privilege denial (%s).\n' "$label" >&2
    return 1
  }
}

db01_assert_negative_privilege() {
  local current_user
  current_user="$(db01_current_user_as "$DB01_API_ROLE")"
  [[ "$current_user" == "$DB01_API_ROLE" ]] || {
    printf 'API runtime did not authenticate as %s (observed %s).\n' "$DB01_API_ROLE" "$current_user" >&2
    return 1
  }

  db01_assert_statement_denied "$DB01_API_ROLE" \
    'CREATE TABLE public.__db01_ddl_probe (id integer PRIMARY KEY);' \
    'representative DDL'
  db01_assert_statement_denied "$DB01_API_ROLE" \
    'CREATE ROLE db01_role_probe NOLOGIN;' \
    'role administration'
  db01_assert_statement_denied "$DB01_API_ROLE" \
    "INSERT INTO public.\"${DB01_HISTORY_TABLE}\" (\"MigrationId\", \"ProductVersion\") VALUES ('db01_should_not_apply', '0.0.0');" \
    'migration application / history insert'
  db01_assert_statement_denied "$DB01_API_ROLE" \
    "DELETE FROM public.\"${DB01_HISTORY_TABLE}\";" \
    'migration-history mutation'
}

db01_assert_privilege_ceiling() {
  db01_assert_no_admin_attributes "$DB01_MIGRATOR_ROLE"
  db01_assert_no_admin_attributes "$DB01_API_ROLE"
  [[ "$(db01_relation_owner "$DB01_HISTORY_TABLE")" == "$DB01_MIGRATOR_ROLE" ]] || {
    printf 'API runtime must not own the EF migration-history object.\n' >&2
    return 1
  }
  [[ "$(db01_psql_as "$DB01_BOOTSTRAP_ROLE" -c "SELECT pg_catalog.pg_get_userbyid(nspowner) FROM pg_catalog.pg_namespace WHERE nspname = 'public';")" != "$DB01_API_ROLE" ]] || {
    printf 'API runtime must not own the application schema.\n' >&2
    return 1
  }
}

db01_assert_api_env_principal() {
  local expected="$1"
  local api_id actual
  api_id="$(db01_api_container_id)"
  actual="$(docker inspect "$api_id" | jq --raw-output '.[0].Config.Env[] | select(startswith("POSTGRES_USERNAME=")) | split("=")[1]')"
  [[ "$actual" == "$expected" ]] || {
    printf 'API runtime POSTGRES_USERNAME=%s (expected %s).\n' "$actual" "$expected" >&2
    return 1
  }
}

db01_api_secret_digest() {
  local api_id secret_file
  api_id="$(db01_api_container_id)"
  secret_file="$(docker inspect "$api_id" | jq --raw-output '.[0].Config.Env[] | select(startswith("MBS_DATABASE_PASSWORD_FILE=")) | split("=")[1]')"
  [[ -n "$secret_file" ]] || {
    printf 'API runtime secret file path is missing.\n' >&2
    return 1
  }
  docker exec "$api_id" sha256sum "$secret_file" | awk '{print $1}'
}

db01_postgres_secret_digest() {
  local secret_file="$1"
  db01_postgres_exec sha256sum "$secret_file" | awk '{print $1}'
}

db01_try_ddl_with_api_mounted_secret() {
  local api_id table="${1:-__db01_priv01_collapse}"
  api_id="$(db01_api_container_id)"
  local secret_file username
  secret_file="$(docker inspect "$api_id" | jq --raw-output '.[0].Config.Env[] | select(startswith("MBS_DATABASE_PASSWORD_FILE=")) | split("=")[1]')"
  username="$(docker inspect "$api_id" | jq --raw-output '.[0].Config.Env[] | select(startswith("POSTGRES_USERNAME=")) | split("=")[1]')"
  docker exec "$api_id" cat "$secret_file" | db01_postgres_exec bash -c 'cat > /tmp/db01-api-runtime.secret'
  db01_postgres_exec bash -c '
    set -euo pipefail
    export PGPASSWORD="$(</tmp/db01-api-runtime.secret)"
    rm -f /tmp/db01-api-runtime.secret
    psql -h 127.0.0.1 -U "$1" -d "$2" -v ON_ERROR_STOP=1 -At -c "$3"
  ' bash "$username" "$DB01_DATABASE" "CREATE TABLE public.\"${table}\" (id integer PRIMARY KEY);"
}

db01_drop_table_as_migrator() {
  local table="$1"
  db01_psql_as "$DB01_MIGRATOR_ROLE" -c "DROP TABLE IF EXISTS public.\"${table}\";" >/dev/null 2>/dev/null
}
