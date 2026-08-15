#!/usr/bin/env bash
set -Eeuo pipefail

# WP2-AUD-01 shipped-Compose proof. Every runtime oracle authenticates with the credential
# mounted in the API container and uses PostgreSQL's non-loopback SCRAM network path.

readonly project_name="${WP2AUD01_PROJECT_NAME:-minimal-bank-system-wp2-aud01-${RANDOM}${RANDOM}}"
readonly sentinel="${WP2AUD01_SECRET_SENTINEL:-WP2AUD01_TEST_SENTINEL_NOT_A_CREDENTIAL}"
readonly bootstrap_sentinel="${sentinel}_BOOTSTRAP"
readonly migrator_sentinel="${sentinel}_MIGRATOR"
readonly api_sentinel="${sentinel}_API"
readonly jwt_sentinel="${sentinel}_JWT"
readonly sensitive_input_sentinel="${sentinel}_OPERATOR_PASSWORD_HASH"
readonly jwt_secret_sentinel="$(printf '%s' "$jwt_sentinel" | base64 | tr -d '\n')"
readonly api_role='minimal_bank_api'
readonly migrator_role='minimal_bank_migrator'
readonly postgres_network_host='postgres'
readonly audit_table='public.audit_records'
readonly operator_table='public.operators'
readonly history_table='public."__EFMigrationsHistory"'
readonly audit_id='018f0e20-7b80-7000-8000-000000000101'
readonly actor_id='018f0e20-7b80-7000-8000-000000000102'
readonly operator_id='018f0e20-7b80-7000-8000-000000000103'
readonly correlation_id='wp2-aud01-runtime-boundary'
readonly compose=(docker compose -p "$project_name")

for command_name in docker jq bash; do
  command -v "$command_name" >/dev/null || {
    printf 'WP2-AUD-01 prerequisite missing: %s\n' "$command_name" >&2
    exit 69
  }
done

export MBS_DATABASE_BOOTSTRAP_PASSWORD="${MBS_DATABASE_BOOTSTRAP_PASSWORD:-$bootstrap_sentinel}"
export MBS_DATABASE_MIGRATOR_PASSWORD="${MBS_DATABASE_MIGRATOR_PASSWORD:-$migrator_sentinel}"
export MBS_DATABASE_API_PASSWORD="${MBS_DATABASE_API_PASSWORD:-$api_sentinel}"
export MBS_JWT_SIGNING_KEY="${MBS_JWT_SIGNING_KEY:-$jwt_secret_sentinel}"

container_id() {
  local id
  id="$("${compose[@]}" ps -aq "$1")"
  [[ -n "$id" ]] || return 1
  printf '%s\n' "$id"
}

container_state() {
  docker inspect "$(container_id "$1")" | jq --raw-output '.[0].State.Status'
}

wait_for_state() {
  local service="$1" expected="$2" attempt
  for attempt in $(seq 1 90); do
    [[ "$(container_state "$service")" == "$expected" ]] && return 0
    sleep 1
  done
  printf '%s did not reach %s.\n' "$service" "$expected" >&2
  return 1
}

wait_for_api_listener() {
  local attempt
  for attempt in $(seq 1 90); do
    if "${compose[@]}" exec -T api bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080'; then
      return 0
    fi
    [[ "$(container_state api)" == running ]] || return 1
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

assert_project_residue_zero() {
  local containers volumes networks
  containers="$(docker ps -aq --filter "label=com.docker.compose.project=$project_name")"
  volumes="$(docker volume ls -q --filter "label=com.docker.compose.project=$project_name")"
  networks="$(docker network ls -q --filter "label=com.docker.compose.project=$project_name")"
  [[ -z "$containers" && -z "$volumes" && -z "$networks" ]]
}

cleanup() {
  "${compose[@]}" down --volumes --remove-orphans
  assert_project_residue_zero
}
trap cleanup EXIT

expect_append_only_denied() {
  local sql="$1" label="$2" output status
  set +e
  output="$(api_network_psql "$sql" 2>&1)"
  status=$?
  set -e
  (( status != 0 )) || {
    printf '%s unexpectedly succeeded.\n' "$label" >&2
    return 1
  }
  [[ "$output" == *'Product Audit records are append-only'* && "$output" == *'55000'* ]] || {
    printf '%s lacked the append-only semantic signature: %s\n' "$label" "$output" >&2
    return 1
  }
  [[ "$output" != *'password authentication failed'* &&
     "$output" != *'permission denied'* &&
     "$output" != *'connection refused'* ]] || {
    printf '%s failed before the trigger boundary: %s\n' "$label" "$output" >&2
    return 1
  }
  printf '%s: REJECTED_BY_APPEND_ONLY_TRIGGER\n' "$label"
}

expect_api_privilege_denied() {
  local sql="$1" label="$2" output status
  set +e
  output="$(api_network_psql "$sql" 2>&1)"
  status=$?
  set -e
  (( status != 0 )) &&
    [[ "$output" == *'permission denied'* || "$output" == *'must be owner'* || "$output" == *'42501'* ]] || {
    printf '%s did not fail at the intended privilege boundary: %s\n' "$label" "$output" >&2
    return 1
  }
  [[ "$output" != *'password authentication failed'* && "$output" != *'connection refused'* ]] || return 1
  printf '%s: DENIED\n' "$label"
}

assert_runtime_authentication() {
  local role current_user network_address ignored hba_method mounted_api_secret mounted_jwt_secret
  role="$(configured_api_role)"
  [[ "$role" == "$api_role" ]] || return 1

  mounted_api_secret="$("${compose[@]}" exec -T api cat /run/secrets/database_password)"
  mounted_jwt_secret="$("${compose[@]}" exec -T api cat /run/secrets/jwt_signing_key)"
  [[ "$mounted_api_secret" == "$MBS_DATABASE_API_PASSWORD" &&
     "$mounted_jwt_secret" == "$MBS_JWT_SIGNING_KEY" ]] || return 1
  printf 'MOUNTED_SECRET_POSITIVE_CONTROL: PASS\n'

  current_user="$(api_network_psql 'SELECT current_user;')"
  [[ "$current_user" == "$api_role" ]] || return 1

  IFS=' ' read -r network_address ignored < <(
    "${compose[@]}" exec -T postgres getent ahostsv4 "$postgres_network_host"
  )
  network_address="${network_address//$'\r'/}"
  [[ "$network_address" =~ ^([0-9]{1,3}\.){3}[0-9]{1,3}$ && "$network_address" != 127.* ]] || return 1
  hba_method="$(psql_as minimal_bank_bootstrap "
    SELECT auth_method
    FROM pg_hba_file_rules
    WHERE error IS NULL
      AND type IN ('host', 'hostnossl')
      AND ('all' = ANY(database) OR 'minimal_bank' = ANY(database))
      AND ('all' = ANY(user_name) OR '${api_role}' = ANY(user_name))
      AND CASE WHEN address = 'all' THEN true ELSE inet('${network_address}') <<= inet(address) END
    ORDER BY line_number LIMIT 1;")"
  [[ "$hba_method" == 'scram-sha-256' ]] || return 1
  printf 'ACTUAL_API_RUNTIME_PRINCIPAL: %s\n' "$current_user"
  printf 'ACTUAL_API_MOUNTED_CREDENTIAL_AUTHENTICATION: PASS\n'
  printf 'PASSWORD_AUTH_NETWORK_PATH: non-loopback+scram-sha-256\n'
}

assert_sensitive_input_positive_control() {
  local stored
  api_network_psql "
    INSERT INTO ${operator_table} (
      id, user_name, normalized_user_name, password_hash, security_stamp,
      state, fixed_role, authorization_state_version, created_at, updated_at
    ) VALUES (
      '${operator_id}', 'audit.sentinel@example.invalid', 'AUDIT.SENTINEL@EXAMPLE.INVALID',
      '${sensitive_input_sentinel}', 'audit-sentinel-security-stamp',
      'active', 'viewer', 1, clock_timestamp(), clock_timestamp()
    );" >/dev/null
  stored="$(api_network_psql "SELECT password_hash FROM ${operator_table} WHERE id = '${operator_id}';")"
  [[ "$stored" == "$sensitive_input_sentinel" ]] || return 1
  printf 'SENSITIVE_INPUT_SENTINEL_POSITIVE_CONTROL: PASS\n'
}

assert_append_only_runtime_boundary() {
  local readback trigger_state row_count
  trigger_state="$(psql_as "$migrator_role" "
    SELECT tgenabled
    FROM pg_trigger
    WHERE tgrelid = '${audit_table}'::regclass
      AND tgname = 'trg_audit_records_append_only'
      AND NOT tgisinternal;")"
  [[ "$trigger_state" == 'O' ]] || return 1

  api_network_psql "
    INSERT INTO ${audit_table} (
      audit_id, actor_identifier, actor_role, operation_identifier, target_identifier,
      result, failure_business_error_code, correlation_id, audit_time
    ) VALUES (
      '${audit_id}', '${actor_id}', 'administrator', 'verification.audit.runtime',
      'account:000000000777', 'failure', 'operation_rejected', '${correlation_id}',
      TIMESTAMPTZ '2036-08-09 10:11:12+00'
    );" >/dev/null
  printf 'ACTUAL_API_RUNTIME_APPEND: SUCCEEDED\n'

  readback="$(api_network_psql "
    SELECT audit_id || '|' || actor_identifier || '|' || actor_role || '|' ||
           operation_identifier || '|' || target_identifier || '|' || result || '|' ||
           failure_business_error_code || '|' || correlation_id || '|' ||
           to_char(audit_time AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS')
    FROM ${audit_table} WHERE audit_id = '${audit_id}';")"
  [[ "$readback" == "${audit_id}|${actor_id}|administrator|verification.audit.runtime|account:000000000777|failure|operation_rejected|${correlation_id}|2036-08-09 10:11:12" ]] || return 1
  printf 'ALL_LOGICAL_FIELDS_POSITIVE_READBACK: PASS\n'

  expect_append_only_denied \
    "UPDATE ${audit_table} SET target_identifier = 'account:mutated' WHERE audit_id = '${audit_id}';" \
    'ACTUAL_API_RUNTIME_UPDATE'
  expect_append_only_denied \
    "DELETE FROM ${audit_table} WHERE audit_id = '${audit_id}';" \
    'ACTUAL_API_RUNTIME_DELETE'
  row_count="$(api_network_psql "SELECT count(*) FROM ${audit_table} WHERE audit_id = '${audit_id}';")"
  [[ "$row_count" == '1' ]] || return 1
  printf 'APPEND_ONLY_ROW_REMAINS_UNCHANGED: PASS\n'
}

assert_db01_regression_boundary() {
  local role_audit
  role_audit="$(psql_as minimal_bank_bootstrap "
    SELECT rolname || '|' || rolsuper || '|' || rolcreatedb || '|' || rolcreaterole || '|' || rolreplication || '|' || rolbypassrls
    FROM pg_roles WHERE rolname IN ('${migrator_role}', '${api_role}') ORDER BY rolname;")"
  [[ "$role_audit" == *"${migrator_role}|false|false|false|false|false"* &&
     "$role_audit" == *"${api_role}|false|false|false|false|false"* ]] || return 1
  expect_api_privilege_denied \
    'CREATE TABLE public.wp2_aud01_ddl_probe (id integer);' \
    'DB01_REGRESSION_API_DDL'
  expect_api_privilege_denied \
    'CREATE ROLE wp2_aud01_role_probe;' \
    'DB01_REGRESSION_ROLE_ADMINISTRATION'
  expect_api_privilege_denied \
    "UPDATE ${history_table} SET \"ProductVersion\" = '0.0.0';" \
    'DB01_REGRESSION_HISTORY_MUTATION'
  printf 'DB01_LEAST_PRIVILEGE_REGRESSION: PASS\n'
}

assert_sensitive_non_persistence() {
  local persisted
  persisted="$(api_network_psql "SELECT row_to_json(a)::text FROM ${audit_table} a;")"
  [[ "$persisted" != *"$sensitive_input_sentinel"* &&
     "$persisted" != *"$MBS_DATABASE_BOOTSTRAP_PASSWORD"* &&
     "$persisted" != *"$MBS_DATABASE_MIGRATOR_PASSWORD"* &&
     "$persisted" != *"$MBS_DATABASE_API_PASSWORD"* &&
     "$persisted" != *"$MBS_JWT_SIGNING_KEY"* ]] || return 1
  printf 'SENSITIVE_DATA_NEGATIVE_PERSISTENCE: PASS\n'
}

"${compose[@]}" config --quiet
"${compose[@]}" up --build --detach --remove-orphans
wait_for_state migrator exited
wait_for_state api running
wait_for_api_listener

assert_runtime_authentication
assert_sensitive_input_positive_control
assert_append_only_runtime_boundary
assert_db01_regression_boundary
assert_sensitive_non_persistence

printf 'WP2_AUD01_RUNTIME_BOUNDARY_VERIFICATION: PASS\n'
