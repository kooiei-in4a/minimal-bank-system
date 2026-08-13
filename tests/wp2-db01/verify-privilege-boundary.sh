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
readonly postgres_network_host='postgres'
readonly wrong_password="${sentinel}_WRONG_PASSWORD_CONTROL"
readonly fixture_table='public.wp2_db01_verification_fixture'
readonly history_table='public."__EFMigrationsHistory"'
readonly compose=(docker compose -p "$project_name")
declare -a cleanup_project_names=("$project_name")

for command_name in docker jq bash; do
  command -v "$command_name" >/dev/null || {
    printf 'WP2-DB-01 prerequisite missing: %s\n' "$command_name" >&2
    exit 69
  }
done

export MBS_DATABASE_BOOTSTRAP_PASSWORD="${MBS_DATABASE_BOOTSTRAP_PASSWORD:-$bootstrap_sentinel}"
export MBS_DATABASE_MIGRATOR_PASSWORD="${MBS_DATABASE_MIGRATOR_PASSWORD:-$migrator_sentinel}"
export MBS_DATABASE_API_PASSWORD="${MBS_DATABASE_API_PASSWORD:-$api_sentinel}"
[[ "$wrong_password" != "$MBS_DATABASE_MIGRATOR_PASSWORD" &&
   "$wrong_password" != "$MBS_DATABASE_API_PASSWORD" ]] || {
  printf 'Wrong-password control unexpectedly equals a configured credential.\n' >&2
  exit 78
}
declare -ar credential_materials=(
  "$MBS_DATABASE_BOOTSTRAP_PASSWORD"
  "$MBS_DATABASE_MIGRATOR_PASSWORD"
  "$MBS_DATABASE_API_PASSWORD"
)

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

configured_api_role() {
  docker inspect "$(container_id api)" |
    jq --raw-output '.[0].Config.Env[] | select(startswith("POSTGRES_USERNAME=")) | split("=")[1]'
}

# Runs psql through the PostgreSQL service's non-loopback Docker-network address. The shipped HBA
# grants trust only to loopback; this path is therefore independently checked below to match the
# scram-sha-256 rule before any credential, DML or privilege oracle is accepted.
password_network_psql() {
  local role="$1" sql="$2"
  "${compose[@]}" exec -T postgres bash -ceu '
      IFS= read -r PGPASSWORD || true
      export PGPASSWORD
      export PGSSLMODE=disable
      exec psql -h "$1" -U "$2" -d minimal_bank --quiet --no-psqlrc \
        -v ON_ERROR_STOP=1 -v VERBOSITY=verbose -At -c "$3"
    ' bash "$postgres_network_host" "$role" "$sql"
}

# Uses the credential mounted into the running API container and the principal configured on that
# same container. The credential is streamed over stdin, never placed in argv, logs or output.
api_network_psql() {
  local sql="$1" role
  role="$(configured_api_role)"
  "${compose[@]}" exec -T api cat /run/secrets/database_password |
    password_network_psql "$role" "$sql"
}

assert_password_authentication_controls() {
  local role network_address ignored hba_rule hba_line hba_method hba_address
  local wrong_output wrong_status actual_user actual_status
  role="$(configured_api_role)"

  IFS=' ' read -r network_address ignored < <(
    "${compose[@]}" exec -T postgres getent ahostsv4 "$postgres_network_host"
  )
  network_address="${network_address//$'\r'/}"
  [[ "$network_address" =~ ^([0-9]{1,3}\.){3}[0-9]{1,3}$ &&
     "$network_address" != 127.* ]] || {
    printf 'Password-authentication target did not resolve to a non-loopback IPv4 address.\n' >&2
    return 1
  }

  [[ "$role" =~ ^[a-z_][a-z0-9_]*$ ]] || {
    printf 'Configured API principal is not a safe PostgreSQL role identifier.\n' >&2
    return 1
  }
  hba_rule="$(psql_as minimal_bank_bootstrap "
    SELECT line_number || '|' || auth_method || '|' || address
    FROM pg_hba_file_rules
    WHERE error IS NULL
      AND type IN ('host', 'hostnossl')
      AND ('all' = ANY(database) OR 'minimal_bank' = ANY(database))
      AND ('all' = ANY(user_name) OR '${role}' = ANY(user_name))
      AND CASE
            WHEN address = 'all' THEN true
            ELSE inet('${network_address}') <<= inet(address)
          END
    ORDER BY line_number
    LIMIT 1;")"
  IFS='|' read -r hba_line hba_method hba_address <<<"$hba_rule"
  [[ -n "$hba_line" && "$hba_method" == 'scram-sha-256' ]] || {
    printf 'Non-loopback API network path is not governed by scram-sha-256.\n' >&2
    return 1
  }
  printf 'PASSWORD_AUTH_NETWORK_PATH: non-loopback (%s)\n' "$network_address"
  printf 'PG_HBA_AUTH_METHOD: scram-sha-256\n'

  set +e
  wrong_output="$(printf '%s\n' "$wrong_password" |
    password_network_psql "$role" 'SELECT current_user;' 2>&1)"
  wrong_status=$?
  set -e
  (( wrong_status != 0 )) &&
    [[ "$wrong_output" == *'password authentication failed'* ]] || {
    printf 'Wrong-password control was not rejected with PostgreSQL authentication failure.\n' >&2
    return 1
  }
  printf 'WRONG_PASSWORD_CONTROL: REJECTED\n'

  set +e
  actual_user="$(api_network_psql 'SELECT current_user;' 2>&1)"
  actual_status=$?
  set -e
  (( actual_status == 0 )) && [[ "$actual_user" == "$role" ]] || {
    printf 'Actual API-mounted credential did not authenticate as the configured API principal.\n' >&2
    return 1
  }
  printf 'ACTUAL_API_MOUNTED_CREDENTIAL_AUTHENTICATION: PASS\n'
}

# Compares secret bytes without returning either the bytes or a digest. The comparison target is
# a bootstrap-mounted copy inside PostgreSQL, while the input is always the API container's actual
# mounted credential path.
api_credential_matches() {
  local postgres_secret_file="$1"
  "${compose[@]}" exec -T api cat /run/secrets/database_password |
    "${compose[@]}" exec -T postgres cmp -s - "$postgres_secret_file"
}

api_http_response() {
  "${compose[@]}" exec -T api bash -ceu '
    exec 3<>/dev/tcp/127.0.0.1/8080
    printf "GET /health/ready HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3
    cat <&3
  '
}

assert_no_project_residue_for() {
  local target_project_name="$1"
  local containers volumes networks
  containers="$(docker ps -aq --filter "label=com.docker.compose.project=$target_project_name")"
  volumes="$(docker volume ls -q --filter "label=com.docker.compose.project=$target_project_name")"
  networks="$(docker network ls -q --filter "label=com.docker.compose.project=$target_project_name")"
  [[ -z "$containers" && -z "$volumes" && -z "$networks" ]] || {
    printf 'Project-scoped Docker residue remains after cleanup.\n' >&2
    return 1
  }
}

cleanup() {
  local cleanup_project_name
  "${compose[@]}" exec -T postgres psql -U "$migrator_role" -d minimal_bank \
    -c "DROP TABLE IF EXISTS ${fixture_table};" >/dev/null 2>&1 || true
  for cleanup_project_name in "${cleanup_project_names[@]}"; do
    docker compose -p "$cleanup_project_name" down --volumes --remove-orphans
    assert_no_project_residue_for "$cleanup_project_name"
  done
}

trap cleanup EXIT

# Bootstrap/Migrator administration helper. API positive and negative proofs intentionally do not
# use this local-trust path; they use api_network_psql above.
psql_as() {
  local role="$1" sql="$2"
  "${compose[@]}" exec -T postgres psql -U "$role" -d minimal_bank -At -c "$sql"
}

expect_api_denied() {
  # PostgreSQL reports privilege denials with different wording depending on the check: a GRANT-
  # based check (schema usage/create, role administration, table DML) says "permission denied";
  # an ownership-based check (structural ALTER on a table the role does not own, such as the
  # Migrator-owned migration-history table) says "must be owner of". Both are genuine denials.
  local sql="$1" label="$2" output status
  set +e
  output="$(api_network_psql "$sql" 2>&1)"
  status=$?
  set -e
  (( status != 0 )) || {
    printf '%s: unexpectedly succeeded; the privilege boundary was violated.\n' "$label" >&2
    return 1
  }
  [[ "$output" != *'password authentication failed'* &&
     "$output" != *'no password supplied'* &&
     "$output" != *'connection refused'* ]] || {
    printf '%s: failed before the intended authenticated privilege check.\n' "$label" >&2
    return 1
  }
  [[ "$output" == *'permission denied'* || "$output" == *'must be owner'* || "$output" == *'42501'* ]] || {
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
  api_credential_matches /run/secrets/database_api_password || {
    printf 'The API-mounted credential does not match the configured API credential source.\n' >&2
    return 1
  }
  if api_credential_matches /run/secrets/database_migrator_password; then
    printf 'Migrator and API runtime credential values are equal.\n' >&2
    return 1
  fi
  printf 'DISTINCT_PRINCIPALS: PASS\n'
  printf 'DISTINCT_SECRET_SOURCES: PASS\n'
  printf 'DISTINCT_CREDENTIAL_VALUES: PASS\n'
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

  api_network_psql \
    "INSERT INTO ${fixture_table} (id, note) VALUES (1, 'wp2-db01-positive-dml');" >/dev/null
  printf 'POSITIVE_DML: API_INSERT_SUCCEEDED\n'

  api_network_psql \
    "UPDATE ${fixture_table} SET note = 'wp2-db01-positive-dml-updated' WHERE id = 1;" >/dev/null
  printf 'POSITIVE_DML: API_UPDATE_SUCCEEDED\n'

  readback="$(api_network_psql "SELECT note FROM ${fixture_table} WHERE id = 1;")"
  [[ "$readback" == 'wp2-db01-positive-dml-updated' ]] || {
    printf 'POSITIVE_DML: readback after UPDATE did not reflect the API write (%s).\n' "$readback" >&2
    return 1
  }
  printf 'POSITIVE_DML: API_SELECT_READBACK_CONFIRMED\n'

  api_network_psql "DELETE FROM ${fixture_table} WHERE id = 1;" >/dev/null
  readback="$(api_network_psql "SELECT count(*) FROM ${fixture_table};")"
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
  local actual_user api_history migrator_history schema_owner history_owner role_audit
  actual_user="$(api_network_psql 'SELECT current_user;')"
  [[ "$actual_user" == "$api_role" && "$(configured_api_role)" == "$api_role" ]] || {
    printf 'API network authentication used an unexpected principal.\n' >&2
    return 1
  }
  printf 'API_NETWORK_AUTHENTICATION: PASS\n'

  role_audit="$(psql_as minimal_bank_bootstrap \
    "SELECT rolname || '|' || rolsuper || '|' || rolcreatedb || '|' || rolcreaterole || '|' || rolreplication || '|' || rolbypassrls FROM pg_roles WHERE rolname IN ('${migrator_role}', '${api_role}') ORDER BY rolname;")"
  [[ "$role_audit" == *"${migrator_role}|false|false|false|false|false"* &&
     "$role_audit" == *"${api_role}|false|false|false|false|false"* ]] || {
    printf 'Migrator/API role privilege ceiling was not enforced.\n' >&2
    return 1
  }
  printf 'MIGRATOR_AND_API_PRIVILEGE_CEILING: PASS\n'

  schema_owner="$(psql_as minimal_bank_bootstrap \
    "SELECT pg_catalog.pg_get_userbyid(nspowner) FROM pg_catalog.pg_namespace WHERE nspname = 'public';")"
  history_owner="$(psql_as minimal_bank_bootstrap \
    "SELECT pg_catalog.pg_get_userbyid(c.relowner) FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'public' AND c.relname = '__EFMigrationsHistory';")"
  [[ "$schema_owner" != "$api_role" && "$history_owner" != "$api_role" ]] || {
    printf 'API runtime owns a protected schema or migration-history object.\n' >&2
    return 1
  }
  printf 'API_OWNERSHIP_BOUNDARY: PASS\n'

  expect_api_denied \
    'CREATE TABLE public.wp2_db01_negative_ddl_probe (id integer);' \
    'NEGATIVE_PRIVILEGE: API_DDL'
  expect_api_denied \
    "CREATE ROLE wp2_db01_negative_role_probe LOGIN;" \
    'NEGATIVE_PRIVILEGE: API_ROLE_ADMINISTRATION'
  expect_api_denied \
    "ALTER TABLE ${history_table} ADD COLUMN wp2_db01_negative_probe text;" \
    'NEGATIVE_PRIVILEGE: API_MIGRATION_APPLICATION'
  expect_api_denied \
    "INSERT INTO ${history_table} (\"MigrationId\", \"ProductVersion\") VALUES ('wp2-db01-negative-probe', '0.0.0');" \
    'NEGATIVE_PRIVILEGE: API_HISTORY_MUTATION_INSERT'
  expect_api_denied \
    "UPDATE ${history_table} SET \"ProductVersion\" = '0.0.0';" \
    'NEGATIVE_PRIVILEGE: API_HISTORY_MUTATION_UPDATE'
  expect_api_denied \
    "DELETE FROM ${history_table};" \
    'NEGATIVE_PRIVILEGE: API_HISTORY_MUTATION_DELETE'

  # The API runtime remains able to read migration history for readiness purposes: the boundary is
  # SELECT-only, not zero access.
  api_history="$(api_network_psql "SELECT \"MigrationId\" FROM ${history_table} ORDER BY \"MigrationId\";")"
  migrator_history="$(psql_as "$migrator_role" "SELECT \"MigrationId\" FROM ${history_table} ORDER BY \"MigrationId\";")"
  [[ -n "$api_history" && "$api_history" == "$migrator_history" ]] || {
    printf 'API and Migrator role-aware history reads diverged.\n' >&2
    return 1
  }
  printf 'NEGATIVE_PRIVILEGE: API_HISTORY_SELECT_STILL_ALLOWED\n'
  printf 'NEGATIVE_PRIVILEGE: PASS\n'
}

assert_non_disclosure() {
  local rendered logs inspect top http_response postgres_id migrator_id api_id observation_surface credential_material
  postgres_id="$(container_id postgres)"
  migrator_id="$(container_id migrator)"
  api_id="$(container_id api)"
  rendered="$("${compose[@]}" config --format json)"
  logs="$("${compose[@]}" logs --no-color --timestamps)"
  inspect="$(docker inspect "$postgres_id" "$migrator_id" "$api_id")"
  top="$(docker top "$api_id"; docker top "$migrator_id" 2>/dev/null || true)"
  http_response="$(api_http_response)"
  for observation_surface in "$rendered" "$logs" "$inspect" "$top" "$http_response"; do
    for credential_material in "${credential_materials[@]}"; do
      [[ "$observation_surface" != *"$credential_material"* ]] || {
        printf 'Credential material was exposed by an external observation surface.\n' >&2
        return 1
      }
    done
  done
  printf 'NON_DISCLOSURE: PASS\n'
}

run_equal_credential_probe() {
  local equal_project_name="${project_name}-equal-credentials-${RANDOM}${RANDOM}"
  local -a equal_compose=(docker compose -p "$equal_project_name")
  local output status logs api_id api_state observation_surface credential_material
  cleanup_project_names+=("$equal_project_name")

  set +e
  output="$(MBS_DATABASE_API_PASSWORD="$MBS_DATABASE_MIGRATOR_PASSWORD" \
    "${equal_compose[@]}" up --build --detach --remove-orphans 2>&1)"
  status=$?
  set -e
  (( status != 0 )) || {
    printf 'Equal Migrator/API credential probe did not fail closed.\n' >&2
    return 1
  }
  logs="$("${equal_compose[@]}" logs --no-color postgres 2>&1 || true)"
  [[ "$logs" == *'ORACLE_SIGNATURE=equal-database-credential-values'* ]] || {
    printf 'Equal credential probe lacked the expected semantic signature.\n' >&2
    return 1
  }
  api_id="$("${equal_compose[@]}" ps -aq api)"
  if [[ -n "$api_id" ]]; then
    api_state="$(docker inspect "$api_id" | jq --raw-output '.[0].State.Status')"
    [[ "$api_state" != running ]] || {
      printf 'Equal credential probe allowed API startup.\n' >&2
      return 1
    }
  fi
  for observation_surface in "$output" "$logs"; do
    for credential_material in "${credential_materials[@]}"; do
      [[ "$observation_surface" != *"$credential_material"* ]] || {
        printf 'Equal credential probe disclosed credential material.\n' >&2
        return 1
      }
    done
  done
  "${equal_compose[@]}" down --volumes --remove-orphans
  assert_no_project_residue_for "$equal_project_name"
  printf 'EQUAL_CREDENTIAL_VALUES: FAIL_CLOSED\n'
  printf 'EQUAL_CREDENTIAL_VALUES: API_NOT_SERVING\n'
  printf 'EQUAL_CREDENTIAL_VALUES: PASS\n'
}

"${compose[@]}" config --quiet

run_equal_credential_probe

"${compose[@]}" up --build --detach --remove-orphans
wait_for_state migrator exited
wait_for_state api running
wait_for_api_listener

assert_password_authentication_controls
assert_distinct_principals_and_credentials
assert_positive_dml
assert_negative_privilege
assert_non_disclosure

printf 'WP2_DB01_PRIVILEGE_BOUNDARY_VERIFICATION: PASS\n'
