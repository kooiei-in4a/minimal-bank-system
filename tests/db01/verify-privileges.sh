#!/usr/bin/env bash
set -Eeuo pipefail

readonly repository_root="$(git rev-parse --show-toplevel)"
readonly project_name="${DB01_PROJECT_NAME:-minimal-bank-system-db01-${RANDOM}${RANDOM}}"
readonly expected_migration='20260809113338_InitialFoundation'
readonly fixture_name='__DbPrivVerificationFixture'
readonly migrator_override="$(mktemp)"

compose_run() {
  docker compose --project-directory "$repository_root" -p "$project_name" "$@"
}

for command_name in docker jq bash; do
  command -v "$command_name" >/dev/null || {
    printf 'DB-01 prerequisite missing: %s\n' "$command_name" >&2
    exit 69
  }
done

export MBS_DATABASE_BOOTSTRAP_PASSWORD="${MBS_DATABASE_BOOTSTRAP_PASSWORD:-DB01_BOOTSTRAP_NOT_A_CREDENTIAL}"
export MBS_DATABASE_MIGRATOR_PASSWORD="${MBS_DATABASE_MIGRATOR_PASSWORD:-DB01_MIGRATOR_NOT_A_CREDENTIAL}"
export MBS_DATABASE_RUNTIME_PASSWORD="${MBS_DATABASE_RUNTIME_PASSWORD:-DB01_RUNTIME_NOT_A_CREDENTIAL}"

container_id() {
  local service_name="$1"
  local id
  id="$(compose_run ps -aq "$service_name")"
  [[ -n "$id" ]] || {
    printf 'DB-01 container not found: %s\n' "$service_name" >&2
    return 1
  }
  printf '%s\n' "$id"
}

container_state() {
  docker inspect "$(container_id "$1")" | jq --raw-output '.[0].State.Status'
}

wait_for_state() {
  local service_name="$1"
  local expected_state="$2"
  local attempt
  for attempt in $(seq 1 90); do
    [[ "$(container_state "$service_name")" == "$expected_state" ]] && return 0
    sleep 1
  done
  printf 'DB-01 timed out waiting for %s=%s.\n' "$service_name" "$expected_state" >&2
  return 1
}

api_username() {
  docker inspect "$(container_id api)" |
    jq --raw-output '.[0].Config.Env[] | select(startswith("POSTGRES_USERNAME=")) | split("=")[1]'
}

api_psql() {
  local sql="$1"
  local username
  username="$(api_username)"
  compose_run exec -T api bash -c 'cat /run/secrets/runtime_password' |
    compose_run exec -T postgres bash -ceu '
      IFS= read -r PGPASSWORD || true
      export PGPASSWORD
      exec psql -h 127.0.0.1 -U "$1" -d minimal_bank --quiet --no-psqlrc -v ON_ERROR_STOP=1 -At -c "$2"
    ' bash "$username" "$sql"
}

migrator_psql() {
  local sql="$1"
  printf '%s\n' "$MBS_DATABASE_MIGRATOR_PASSWORD" |
    compose_run exec -T postgres bash -ceu '
      IFS= read -r PGPASSWORD || true
      export PGPASSWORD
      exec psql -h 127.0.0.1 -U mbs_migrator -d minimal_bank --quiet --no-psqlrc -v ON_ERROR_STOP=1 -At -c "$1"
    ' bash "$sql"
}

bootstrap_psql() {
  local sql="$1"
  compose_run exec -T postgres psql -U mbs_bootstrap -d minimal_bank --quiet --no-psqlrc -v ON_ERROR_STOP=1 -At -c "$sql"
}

expect_runtime_denied() {
  local label="$1"
  local sql="$2"
  local expected_error="$3"
  local output status
  set +e
  output="$(api_psql "$sql" 2>&1)"
  status=$?
  set -e
  (( status != 0 )) || {
    printf 'DB-01 negative privilege unexpectedly succeeded: %s\n' "$label" >&2
    return 1
  }
  [[ "$output" == *"$expected_error"* ]] || {
    printf 'DB-01 negative privilege was not semantically denied: %s\n' "$label" >&2
    return 1
  }
  printf 'DB01_NEGATIVE[%s]: PASS\n' "$label"
}

cleanup() {
  set +e
  migrator_psql "DROP TABLE IF EXISTS public.\"$fixture_name\";" >/dev/null 2>&1
  compose_run down --volumes --remove-orphans >/dev/null 2>&1
  rm -f "$migrator_override"
  set -e
}

trap cleanup EXIT

compose_run config --quiet
compose_run up --build --detach --remove-orphans
wait_for_state db-provisioner exited
wait_for_state migrator exited
wait_for_state api running

printf 'DB01_STAGE: PRINCIPAL_PROBE\n'
runtime_principal="$(api_psql 'SELECT current_user;')"
migrator_principal="$(migrator_psql 'SELECT current_user;')"
[[ "$runtime_principal" == 'mbs_runtime' && "$migrator_principal" == 'mbs_migrator' ]] || {
  printf 'DB-01 distinct principal proof failed.\n' >&2
  exit 1
}
printf 'DB01_DISTINCT_PRINCIPALS: PASS\n'

printf 'DB01_STAGE: ROLE_AUDIT\n'
role_audit="$(bootstrap_psql "SELECT rolname || '|' || rolsuper || '|' || rolcreatedb || '|' || rolcreaterole || '|' || rolreplication || '|' || rolbypassrls FROM pg_roles WHERE rolname IN ('mbs_migrator', 'mbs_runtime') ORDER BY rolname;")"
[[ "$role_audit" == *'mbs_migrator|false|false|false|false|false'* && "$role_audit" == *'mbs_runtime|false|false|false|false|false'* ]] || {
  printf 'DB-01 role privilege ceiling proof failed.\n' >&2
  exit 1
}
printf 'DB01_PRIVILEGE_CEILING: PASS\n'

printf 'DB01_STAGE: HISTORY_READ\n'
history_owner="$(bootstrap_psql "SELECT pg_get_userbyid(relowner) FROM pg_class WHERE oid = 'public.\"__EFMigrationsHistory\"'::regclass;")"
[[ "$(api_psql 'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId";')" == *"$expected_migration"* ]] || {
  printf 'DB-01 role-aware migration-history read proof failed.\n' >&2
  exit 1
}
[[ "$history_owner" == 'mbs_migrator' ]] || {
  printf 'DB-01 migration-history owner boundary proof failed.\n' >&2
  exit 1
}
printf 'DB01_ROLE_AWARE_HISTORY_READ: PASS\n'

printf 'DB01_STAGE: POSITIVE_DML\n'
migrator_psql "CREATE TABLE public.\"$fixture_name\" (id integer PRIMARY KEY, value text NOT NULL);"
dml_output="$(api_psql "INSERT INTO public.\"$fixture_name\" (id, value) VALUES (7, 'before') RETURNING value; UPDATE public.\"$fixture_name\" SET value = 'after' WHERE id = 7 RETURNING value; DELETE FROM public.\"$fixture_name\" WHERE id = 7 RETURNING id;")"
[[ "$dml_output" == *'before'* && "$dml_output" == *'after'* && "$dml_output" == *$'7'* ]] || {
  printf 'DB-01 positive DML proof failed.\n' >&2
  exit 1
}
[[ "$(api_psql "SELECT count(*) FROM public.\"$fixture_name\";")" == '0' ]] || {
  printf 'DB-01 fixture cleanup/readback proof failed.\n' >&2
  exit 1
}
printf 'DB01_POSITIVE_DML: PASS\n'

printf 'DB01_STAGE: NEGATIVE_PRIVILEGE\n'
expect_runtime_denied ddl \
  "CREATE TABLE public.\"__DbPrivRuntimeDdlProbe\" (id integer);" \
  'permission denied for schema public'
expect_runtime_denied role-admin \
  'CREATE ROLE "__DbPrivRuntimeRoleProbe" LOGIN;' \
  'permission denied to create role'
expect_runtime_denied history-mutation \
  'DELETE FROM public."__EFMigrationsHistory";' \
  'permission denied for table __EFMigrationsHistory'

printf 'DB01_STAGE: MIGRATION_APPLICATION_DENIAL\n'
history_product_version="$(migrator_psql "SELECT \"ProductVersion\" FROM public.\"__EFMigrationsHistory\" WHERE \"MigrationId\" = '$expected_migration';")"
migrator_psql "DELETE FROM public.\"__EFMigrationsHistory\" WHERE \"MigrationId\" = '$expected_migration';"
cat >"$migrator_override" <<'YAML'
services:
  migrator:
    environment:
      POSTGRES_USERNAME: mbs_runtime
    secrets:
      - source: runtime_password
        target: migrator_password
YAML

set +e
runtime_migration_output="$(docker compose --project-directory "$repository_root" -p "$project_name" -f "$repository_root/compose.yaml" -f "$migrator_override" run --rm --no-deps migrator 2>&1)"
runtime_migration_status=$?
set -e
(( runtime_migration_status != 0 )) || {
  printf 'DB-01 runtime credential unexpectedly applied a migration.\n' >&2
  exit 1
}
[[ "$runtime_migration_output" == *'Migration failed. The deployment must not continue.'* &&
   ( "$runtime_migration_output" == *'permission denied'* || "$runtime_migration_output" == *'must be owner'* ) ]] || {
  printf 'DB-01 migration application denial lacked a semantic failure signature.\n' >&2
  exit 1
}
migrator_psql "DELETE FROM public.\"__EFMigrationsHistory\" WHERE \"MigrationId\" = '$expected_migration'; INSERT INTO public.\"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('$expected_migration', '$history_product_version');"
printf 'DB01_MIGRATION_APPLICATION_DENIED: PASS\n'

[[ "$(api_psql 'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId";')" == *"$expected_migration"* ]] || {
  printf 'DB-01 history restore failed.\n' >&2
  exit 1
}
printf 'DB01_RESTORE_GREEN: PASS\n'
printf 'DB01_PRIVILEGE_VERIFICATION: PASS\n'
