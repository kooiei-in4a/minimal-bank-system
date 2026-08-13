#!/usr/bin/env bash
set -Eeuo pipefail

# WP2-DB-01 PostgreSQL privilege / runtime credential boundary verification against the shipped
# Compose runtime. This owns the DB-01-specific proof obligations that FND-05/FND-06 do not
# already cover: a non-vacuous positive application-DML proof, a semantic negative-privilege
# proof for the API runtime principal, and distinct-principal/distinct-credential confirmation.
# Missing-secret fail-closed probes for the Migrator and API runtime credentials are owned by
# tests/fnd05/verify-compose.sh; role-aware migration-history reads for FND-05/FND-06 are owned
# by those scripts directly.

readonly project_name="${WP2DB01_PROJECT_NAME:-minimal-bank-system-wp2-db01-${RANDOM}${RANDOM}}"
readonly sentinel="${WP2DB01_SECRET_SENTINEL:-WP2DB01_TEST_SENTINEL_NOT_A_CREDENTIAL}"
readonly bootstrap_sentinel="${sentinel}_BOOTSTRAP"
readonly migrator_sentinel="${sentinel}_MIGRATOR"
readonly api_sentinel="${sentinel}_API"
readonly migrator_role='minimal_bank_migrator'
readonly api_role='minimal_bank_api'
readonly fixture_table='public.wp2_db01_verification_fixture'
readonly history_table='public."__EFMigrationsHistory"'
readonly compose=(docker compose -p "$project_name")

for command_name in docker jq bash; do
  command -v "$command_name" >/dev/null || {
    printf 'WP2-DB-01 prerequisite missing: %s\n' "$command_name" >&2
    exit 69
  }
done

export MBS_DATABASE_BOOTSTRAP_PASSWORD="${MBS_DATABASE_BOOTSTRAP_PASSWORD:-$bootstrap_sentinel}"
export MBS_DATABASE_MIGRATOR_PASSWORD="${MBS_DATABASE_MIGRATOR_PASSWORD:-$migrator_sentinel}"
export MBS_DATABASE_API_PASSWORD="${MBS_DATABASE_API_PASSWORD:-$api_sentinel}"

container_id() {
  local service_name="$1" id
  id="$("${compose[@]}" ps -aq "$service_name")"
  [[ -n "$id" ]] || {
    printf 'Expected %s container was not found.\n' "$service_name" >&2
    return 1
  }
  printf '%s\n' "$id"
}

container_state() {
  docker inspect "$(container_id "$1")" | jq --raw-output '.[0].State.Status'
}

wait_for_state() {
  local service_name="$1" expected_state="$2" attempt
  for attempt in $(seq 1 90); do
    [[ "$(container_state "$service_name")" == "$expected_state" ]] && return 0
    sleep 1
  done
  printf '%s did not reach %s.\n' "$service_name" "$expected_state" >&2
  return 1
}

wait_for_api_listener() {
  local attempt
  for attempt in $(seq 1 90); do
    if "${compose[@]}" exec -T api bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080'; then
      return 0
    fi
    [[ "$(container_state api)" == 'running' ]] || return 1
    sleep 1
  done
  printf 'API listener did not become reachable.\n' >&2
  return 1
}

assert_no_project_residue() {
  local containers volumes networks
  containers="$(docker ps -aq --filter "label=com.docker.compose.project=$project_name")"
  volumes="$(docker volume ls -q --filter "label=com.docker.compose.project=$project_name")"
  networks="$(docker network ls -q --filter "label=com.docker.compose.project=$project_name")"
  [[ -z "$containers" && -z "$volumes" && -z "$networks" ]] || {
    printf 'Project-scoped Docker residue remains after cleanup.\n' >&2
    return 1
  }
}

cleanup() {
  "${compose[@]}" exec -T postgres psql -U "$migrator_role" -d minimal_bank \
    -c "DROP TABLE IF EXISTS ${fixture_table};" >/dev/null 2>&1 || true
  "${compose[@]}" down --volumes --remove-orphans
  assert_no_project_residue
}

trap cleanup EXIT

# Runs a statement as the given PostgreSQL role over the local Unix-socket connection inside the
# postgres container (trust-authenticated for local connections, the same mechanism the existing
# FND-05/FND-06 read_history()/tables() probes already rely on). This exercises the role's actual
# grant/privilege boundary, independent of how the API or Migrator authenticate over the network.
psql_as() {
  local role="$1" sql="$2"
  "${compose[@]}" exec -T postgres psql -U "$role" -d minimal_bank -At -c "$sql"
}

expect_denied() {
  # PostgreSQL reports privilege denials with different wording depending on the check: a GRANT-
  # based check (schema usage/create, role administration, table DML) says "permission denied";
  # an ownership-based check (structural ALTER on a table the role does not own, such as the
  # Migrator-owned migration-history table) says "must be owner of". Both are genuine denials.
  local role="$1" sql="$2" label="$3" output status
  set +e
  output="$(psql_as "$role" "$sql" 2>&1)"
  status=$?
  set -e
  (( status != 0 )) || {
    printf '%s: unexpectedly succeeded; the privilege boundary was violated.\n' "$label" >&2
    return 1
  }
  [[ "$output" == *'permission denied'* || "$output" == *'must be owner'* ]] || {
    printf '%s: failed for an unexpected reason: %s\n' "$label" "$output" >&2
    return 1
  }
  printf '%s: DENIED (%s)\n' "$label" "$output"
}

assert_distinct_principals_and_credentials() {
  local rendered
  rendered="$("${compose[@]}" config --format json)"
  jq --exit-status '
      .services.postgres.environment.POSTGRES_USER == "minimal_bank_bootstrap" and
      .services.migrator.environment.POSTGRES_USERNAME == "minimal_bank_migrator" and
      .services.api.environment.POSTGRES_USERNAME == "minimal_bank_api" and
      .services.migrator.secrets[0].source == "database_migrator_password" and
      .services.api.secrets[0].source == "database_api_password" and
      .services.migrator.secrets[0].source != .services.api.secrets[0].source
    ' <<<"$rendered" >/dev/null || {
    printf 'Migrator and API runtime principals/credentials are not distinct in the shipped Compose contract.\n' >&2
    return 1
  }
  printf 'DISTINCT_PRINCIPALS: PASS\n'
  printf 'DISTINCT_CREDENTIALS: PASS\n'
}

assert_positive_dml() {
  local readback
  # The fixture is a disposable, non-business relation created by the Migrator role. It receives
  # runtime DML capability through the exact same bootstrap default-privilege rule that will apply
  # to real future application tables the Migrator creates -- not through a bespoke grant or a
  # test-only superuser bypass.
  psql_as "$migrator_role" \
    "CREATE TABLE ${fixture_table} (id integer PRIMARY KEY, note text);" >/dev/null
  printf 'POSITIVE_DML: FIXTURE_CREATED_BY_MIGRATOR\n'

  psql_as "$api_role" \
    "INSERT INTO ${fixture_table} (id, note) VALUES (1, 'wp2-db01-positive-dml');" >/dev/null
  printf 'POSITIVE_DML: API_INSERT_SUCCEEDED\n'

  psql_as "$api_role" \
    "UPDATE ${fixture_table} SET note = 'wp2-db01-positive-dml-updated' WHERE id = 1;" >/dev/null
  printf 'POSITIVE_DML: API_UPDATE_SUCCEEDED\n'

  readback="$(psql_as "$api_role" "SELECT note FROM ${fixture_table} WHERE id = 1;")"
  [[ "$readback" == 'wp2-db01-positive-dml-updated' ]] || {
    printf 'POSITIVE_DML: readback after UPDATE did not reflect the API write (%s).\n' "$readback" >&2
    return 1
  }
  printf 'POSITIVE_DML: API_SELECT_READBACK_CONFIRMED\n'

  psql_as "$api_role" "DELETE FROM ${fixture_table} WHERE id = 1;" >/dev/null
  readback="$(psql_as "$api_role" "SELECT count(*) FROM ${fixture_table};")"
  [[ "$readback" == '0' ]] || {
    printf 'POSITIVE_DML: fixture row remained after API DELETE.\n' >&2
    return 1
  }
  printf 'POSITIVE_DML: API_DELETE_SUCCEEDED_AND_CONFIRMED\n'

  psql_as "$migrator_role" "DROP TABLE ${fixture_table};" >/dev/null
  printf 'POSITIVE_DML: FIXTURE_CLEANED_UP\n'
  printf 'POSITIVE_DML: PASS\n'
}

assert_negative_privilege() {
  expect_denied "$api_role" \
    'CREATE TABLE public.wp2_db01_negative_ddl_probe (id integer);' \
    'NEGATIVE_PRIVILEGE: API_DDL'
  expect_denied "$api_role" \
    "CREATE ROLE wp2_db01_negative_role_probe LOGIN;" \
    'NEGATIVE_PRIVILEGE: API_ROLE_ADMINISTRATION'
  expect_denied "$api_role" \
    "ALTER TABLE ${history_table} ADD COLUMN wp2_db01_negative_probe text;" \
    'NEGATIVE_PRIVILEGE: API_MIGRATION_APPLICATION'
  expect_denied "$api_role" \
    "INSERT INTO ${history_table} (\"MigrationId\", \"ProductVersion\") VALUES ('wp2-db01-negative-probe', '0.0.0');" \
    'NEGATIVE_PRIVILEGE: API_HISTORY_MUTATION_INSERT'
  expect_denied "$api_role" \
    "DELETE FROM ${history_table};" \
    'NEGATIVE_PRIVILEGE: API_HISTORY_MUTATION_DELETE'

  # The API runtime remains able to read migration history for readiness purposes: the boundary is
  # SELECT-only, not zero access.
  psql_as "$api_role" "SELECT count(*) FROM ${history_table};" >/dev/null
  printf 'NEGATIVE_PRIVILEGE: API_HISTORY_SELECT_STILL_ALLOWED\n'
  printf 'NEGATIVE_PRIVILEGE: PASS\n'
}

assert_non_disclosure() {
  local rendered logs inspect top postgres_id migrator_id api_id observation_surface probed_sentinel
  postgres_id="$(container_id postgres)"
  migrator_id="$(container_id migrator)"
  api_id="$(container_id api)"
  rendered="$("${compose[@]}" config --format json)"
  logs="$("${compose[@]}" logs --no-color --timestamps)"
  inspect="$(docker inspect "$postgres_id" "$migrator_id" "$api_id")"
  top="$(docker top "$api_id"; docker top "$migrator_id" 2>/dev/null || true)"
  for observation_surface in "$rendered" "$logs" "$inspect" "$top"; do
    for probed_sentinel in "$bootstrap_sentinel" "$migrator_sentinel" "$api_sentinel"; do
      [[ "$observation_surface" != *"$probed_sentinel"* ]] || {
        printf 'Secret sentinel was exposed by an external observation surface.\n' >&2
        return 1
      }
    done
  done
  printf 'NON_DISCLOSURE: PASS\n'
}

"${compose[@]}" config --quiet

assert_distinct_principals_and_credentials

"${compose[@]}" up --build --detach --remove-orphans
wait_for_state migrator exited
wait_for_state api running
wait_for_api_listener

assert_positive_dml
assert_negative_privilege
assert_non_disclosure

printf 'WP2_DB01_PRIVILEGE_BOUNDARY_VERIFICATION: PASS\n'
