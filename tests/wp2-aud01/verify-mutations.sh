#!/usr/bin/env bash
set -Eeuo pipefail

# Critical Mutation AUD-APPEND-01.
# The semantic kill condition is not a generic RED: after disabling only the intended trigger,
# the same actual API-mounted credential must authenticate as the API runtime principal and make
# both a representative UPDATE and DELETE persist. Restoring the trigger must restore both
# rejections while a fresh normal append still succeeds.

readonly script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly repository_root="$(cd -- "$script_directory/../.." && pwd)"
readonly project_name="${WP2AUD01_MUTATION_PROJECT_NAME:-minimal-bank-system-aud-append-01-${RANDOM}${RANDOM}}"
readonly sentinel="${WP2AUD01_SECRET_SENTINEL:-AUD_APPEND_01_SENTINEL_NOT_A_CREDENTIAL}"
readonly jwt_sentinel="${sentinel}_JWT"
readonly jwt_secret_sentinel="$(printf '%s' "$jwt_sentinel" | base64 | tr -d '\n')"
readonly api_role='minimal_bank_api'
readonly migrator_role='minimal_bank_migrator'
readonly postgres_network_host='postgres'
readonly audit_table='public.audit_logs'
readonly trigger_name='trg_audit_logs_append_only'
readonly baseline_id='018f0e20-7b80-7000-8000-000000000201'
readonly restore_id='018f0e20-7b80-7000-8000-000000000202'
readonly actor_id='018f0e20-7b80-7000-8000-000000000203'
readonly compose=(docker compose --project-directory "$repository_root" -p "$project_name" -f "$repository_root/compose.yaml")

export MBS_DATABASE_BOOTSTRAP_PASSWORD="${MBS_DATABASE_BOOTSTRAP_PASSWORD:-${sentinel}_BOOTSTRAP}"
export MBS_DATABASE_MIGRATOR_PASSWORD="${MBS_DATABASE_MIGRATOR_PASSWORD:-${sentinel}_MIGRATOR}"
export MBS_DATABASE_API_PASSWORD="${MBS_DATABASE_API_PASSWORD:-${sentinel}_API}"
export MBS_JWT_SIGNING_KEY="${MBS_JWT_SIGNING_KEY:-$jwt_secret_sentinel}"

container_id() {
  local id
  id="$("${compose[@]}" ps -aq "$1")"
  [[ -n "$id" ]] || return 1
  printf '%s\n' "$id"
}

state() {
  docker inspect "$(container_id "$1")" | jq --raw-output '.[0].State.Status'
}

wait_for_state() {
  local service="$1" expected="$2" attempt
  for attempt in $(seq 1 90); do
    [[ "$(state "$service")" == "$expected" ]] && return 0
    sleep 1
  done
  return 1
}

wait_for_listener() {
  local attempt
  for attempt in $(seq 1 90); do
    if "${compose[@]}" exec -T api bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080'; then
      return 0
    fi
    [[ "$(state api)" == running ]] || return 1
    sleep 1
  done
  return 1
}

psql_as() {
  local role="$1" sql="$2"
  "${compose[@]}" exec -T postgres psql -U "$role" -d minimal_bank --quiet --no-psqlrc \
    -v ON_ERROR_STOP=1 -v VERBOSITY=verbose -At -c "$sql"
}

configured_api_role() {
  "${compose[@]}" exec -T api printenv POSTGRES_USERNAME | tr -d '\r'
}

password_network_psql() {
  local role="$1" sql="$2"
  "${compose[@]}" exec -T postgres bash -ceu '
      IFS= read -r PGPASSWORD || true
      export PGPASSWORD PGSSLMODE=disable
      exec psql -h "$1" -U "$2" -d minimal_bank --quiet --no-psqlrc \
        -v ON_ERROR_STOP=1 -v VERBOSITY=verbose -At -c "$3"
    ' bash "$postgres_network_host" "$role" "$sql"
}

api_network_psql() {
  local sql="$1" role
  role="$(configured_api_role)"
  "${compose[@]}" exec -T api cat /run/secrets/database_password |
    password_network_psql "$role" "$sql"
}

cleanup() {
  "${compose[@]}" down --volumes --remove-orphans
  [[ -z "$(docker ps -aq --filter "label=com.docker.compose.project=$project_name")" ]]
  [[ -z "$(docker volume ls -q --filter "label=com.docker.compose.project=$project_name")" ]]
  [[ -z "$(docker network ls -q --filter "label=com.docker.compose.project=$project_name")" ]]
}
trap cleanup EXIT

append_row() {
  local audit_id="$1" correlation_id="$2"
  api_network_psql "
    INSERT INTO ${audit_table} (
      audit_id, actor_identifier, actor_role, operation_identifier, target_identifier,
      result, failure_business_error_code, correlation_id, audit_time
    ) VALUES (
      '${audit_id}', '${actor_id}', 'viewer', 'verification.audit.mutation',
      'account:000000000888', 'success', NULL, '${correlation_id}', clock_timestamp()
    );" >/dev/null
}

expect_trigger_denied() {
  local sql="$1" operation="$2" output status
  set +e
  output="$(api_network_psql "$sql" 2>&1)"
  status=$?
  set -e
  (( status != 0 )) || return 1
  [[ "$output" == *'Product Audit history is append-only.'* && "$output" == *'55000'* ]] || {
    printf 'AUD-APPEND-01 %s denial lacked trigger signature: %s\n' "$operation" "$output" >&2
    return 1
  }
  [[ "$output" != *'password authentication failed'* && "$output" != *'permission denied'* ]] || return 1
}

assert_actual_runtime_principal() {
  local configured authenticated
  configured="$(configured_api_role)"
  authenticated="$(api_network_psql 'SELECT current_user;')"
  [[ "$configured" == "$api_role" && "$authenticated" == "$api_role" ]] || {
    printf 'AUD-APPEND-01 did not use the designated actual API runtime principal.\n' >&2
    return 1
  }
}

assert_baseline_green() {
  local count target
  assert_actual_runtime_principal
  append_row "$baseline_id" 'aud-append-01-baseline'
  expect_trigger_denied \
    "UPDATE ${audit_table} SET target_identifier = 'account:baseline-mutated' WHERE audit_id = '${baseline_id}';" \
    UPDATE
  expect_trigger_denied \
    "DELETE FROM ${audit_table} WHERE audit_id = '${baseline_id}';" \
    DELETE
  count="$(api_network_psql "SELECT count(*) FROM ${audit_table} WHERE audit_id = '${baseline_id}';")"
  target="$(api_network_psql "SELECT target_identifier FROM ${audit_table} WHERE audit_id = '${baseline_id}';")"
  [[ "$count" == '1' && "$target" == 'account:000000000888' ]] || return 1
  printf 'AUD-APPEND-01: BASELINE_GREEN\n'
  printf 'AUD-APPEND-01: BASELINE_ACTUAL_RUNTIME_APPEND_SUCCEEDED\n'
  printf 'AUD-APPEND-01: BASELINE_UPDATE_DELETE_REJECTED_BY_TRIGGER\n'
}

assert_append_only_invariant() {
  local update_status delete_status target count
  assert_actual_runtime_principal
  set +e
  api_network_psql \
    "UPDATE ${audit_table} SET target_identifier = 'account:mutation-succeeded' WHERE audit_id = '${baseline_id}';" \
    >/dev/null 2>&1
  update_status=$?
  set -e
  target="$(api_network_psql "SELECT target_identifier FROM ${audit_table} WHERE audit_id = '${baseline_id}';")"

  set +e
  api_network_psql "DELETE FROM ${audit_table} WHERE audit_id = '${baseline_id}';" >/dev/null 2>&1
  delete_status=$?
  set -e
  count="$(api_network_psql "SELECT count(*) FROM ${audit_table} WHERE audit_id = '${baseline_id}';")"

  if (( update_status == 0 && delete_status == 0 )) &&
     [[ "$target" == 'account:mutation-succeeded' && "$count" == '0' ]]; then
    printf 'ORACLE_SIGNATURE=actual-api-runtime-update-became-persistent\n' >&2
    printf 'ORACLE_SIGNATURE=actual-api-runtime-delete-became-persistent\n' >&2
    printf 'ORACLE_SIGNATURE=append-only-trigger-bypass-exposed-prohibited-dml\n' >&2
    return 1
  fi
  return 0
}

expect_mutation_red() {
  local output status
  set +e
  output="$(assert_append_only_invariant 2>&1)"
  status=$?
  set -e
  (( status != 0 )) || {
    printf 'AUD-APPEND-01 mutation did not expose prohibited DML.\n' >&2
    return 1
  }
  [[ "$output" == *'ORACLE_SIGNATURE=actual-api-runtime-update-became-persistent'* &&
     "$output" == *'ORACLE_SIGNATURE=actual-api-runtime-delete-became-persistent'* &&
     "$output" == *'ORACLE_SIGNATURE=append-only-trigger-bypass-exposed-prohibited-dml'* ]] || {
    printf 'AUD-APPEND-01 mutation failed with an unexpected signature: %s\n' "$output" >&2
    return 1
  }
  printf 'AUD-APPEND-01: MUTATION_RED\n'
  printf 'AUD-APPEND-01: SEMANTIC_FAILURE=actual-api-runtime-update+delete-became-persistent\n'
}

assert_restore_green() {
  local count
  assert_actual_runtime_principal
  append_row "$restore_id" 'aud-append-01-restore'
  expect_trigger_denied \
    "UPDATE ${audit_table} SET target_identifier = 'account:restore-mutated' WHERE audit_id = '${restore_id}';" \
    UPDATE
  expect_trigger_denied \
    "DELETE FROM ${audit_table} WHERE audit_id = '${restore_id}';" \
    DELETE
  count="$(api_network_psql "SELECT count(*) FROM ${audit_table} WHERE audit_id = '${restore_id}';")"
  [[ "$count" == '1' ]] || return 1
  printf 'AUD-APPEND-01: RESTORE_GREEN\n'
  printf 'AUD-APPEND-01: RESTORE_ACTUAL_RUNTIME_APPEND_SUCCEEDED\n'
  printf 'AUD-APPEND-01: RESTORE_UPDATE_DELETE_REJECTED_BY_TRIGGER\n'
}

"${compose[@]}" up --build --detach --remove-orphans
wait_for_state migrator exited
wait_for_state api running
wait_for_listener

assert_baseline_green

psql_as "$migrator_role" "ALTER TABLE ${audit_table} DISABLE TRIGGER ${trigger_name};" >/dev/null
[[ "$(psql_as "$migrator_role" "SELECT tgenabled FROM pg_trigger WHERE tgrelid = '${audit_table}'::regclass AND tgname = '${trigger_name}';")" == 'D' ]]
printf 'AUD-APPEND-01: MUTATION_APPLIED=target-trigger-disabled\n'
expect_mutation_red

psql_as "$migrator_role" "ALTER TABLE ${audit_table} ENABLE TRIGGER ${trigger_name};" >/dev/null
[[ "$(psql_as "$migrator_role" "SELECT tgenabled FROM pg_trigger WHERE tgrelid = '${audit_table}'::regclass AND tgname = '${trigger_name}';")" == 'O' ]]
assert_restore_green

printf 'AUD-APPEND-01: KILLED\n'
printf 'WP2_AUD01_CRITICAL_MUTATION: PASS\n'
