#!/usr/bin/env bash
set -Eeuo pipefail

# WP2-DB-01 Critical Mutation DB-PRIV-01.
#
# Invariant: the API runtime must never authenticate or operate using the Migrator principal or
# credential. The mutation intentionally wires the API runtime service to the Migrator's own
# principal and credential, collapsing the boundary, and must be killed by a semantic failure
# signature that shows both (a) the API runtime's actual configured principal no longer matches
# its designated principal, and (b) the credential path now driving the API service gained
# representative DDL capability that is normally prohibited for it. A generic non-zero exit is not
# an acceptable oracle for this mutation.

readonly repository_root="$(git rev-parse --show-toplevel)"
readonly sentinel="${WP2DB01_SECRET_SENTINEL:-WP2DB01_MUTATION_SENTINEL_NOT_A_CREDENTIAL}"
readonly run_id="${RANDOM}${RANDOM}"
readonly designated_api_role='minimal_bank_api'
readonly migrator_role='minimal_bank_migrator'
readonly postgres_network_host='postgres'
readonly wrong_password="${sentinel}_WRONG_PASSWORD_CONTROL"
readonly probe_table='public.db_priv_01_probe'

export MBS_DATABASE_BOOTSTRAP_PASSWORD="${MBS_DATABASE_BOOTSTRAP_PASSWORD:-${sentinel}_BOOTSTRAP}"
export MBS_DATABASE_MIGRATOR_PASSWORD="${MBS_DATABASE_MIGRATOR_PASSWORD:-${sentinel}_MIGRATOR}"
export MBS_DATABASE_API_PASSWORD="${MBS_DATABASE_API_PASSWORD:-${sentinel}_API}"
[[ "$wrong_password" != "$MBS_DATABASE_MIGRATOR_PASSWORD" &&
   "$wrong_password" != "$MBS_DATABASE_API_PASSWORD" ]] || {
  printf 'Wrong-password control unexpectedly equals a configured credential.\n' >&2
  exit 78
}

project_name="minimal-bank-system-wp2-db01-mutation-$run_id"
readonly compose=(docker compose --project-directory "$repository_root" -p "$project_name" -f "$repository_root/compose.yaml")

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
  printf 'Timed out waiting for %s=%s.\n' "$service" "$expected" >&2
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

# Bootstrap/Migrator cleanup helper. The DB-PRIV-01 oracle itself must use the actual credential
# mounted in the running API container, not this local-trust path.
psql_as() {
  local role="$1" sql="$2"
  "${compose[@]}" exec -T postgres psql -U "$role" -d minimal_bank -At -c "$sql"
}

configured_api_role() {
  "${compose[@]}" exec -T api printenv POSTGRES_USERNAME | tr -d '\r'
}

# Runs psql through the PostgreSQL service's non-loopback Docker-network address. The HBA rule and
# wrong-password behavior for this exact path are mandatory preconditions below.
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

# Streams the API service's actual mounted credential to psql over stdin. Credential material never
# enters argv, logs, a temporary file or verification output.
api_network_psql() {
  local sql="$1" role
  role="$(configured_api_role)"
  "${compose[@]}" exec -T api cat /run/secrets/database_password |
    password_network_psql "$role" "$sql"
}

assert_password_authentication_controls() {
  local role="$1" network_address ignored hba_rule hba_line hba_method hba_address
  local wrong_output wrong_status actual_user actual_status

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

api_credential_matches() {
  local postgres_secret_file="$1"
  "${compose[@]}" exec -T api cat /run/secrets/database_password |
    "${compose[@]}" exec -T postgres cmp -s - "$postgres_secret_file"
}

assert_residue_zero() {
  local containers volumes networks
  containers="$(docker ps -aq --filter "label=com.docker.compose.project=$project_name")"
  volumes="$(docker volume ls -q --filter "label=com.docker.compose.project=$project_name")"
  networks="$(docker network ls -q --filter "label=com.docker.compose.project=$project_name")"
  [[ -z "$containers" && -z "$volumes" && -z "$networks" ]] || return 1
}

cleanup() {
  psql_as "$migrator_role" "DROP TABLE IF EXISTS ${probe_table};" >/dev/null 2>&1 || true
  "${compose[@]}" down --volumes --remove-orphans
  assert_residue_zero
}

trap cleanup EXIT

# Asserts the DB-PRIV-01 invariant using the API service's actual configured principal and actual
# mounted credential over TCP/password authentication. The complete intended mutation must expose
# principal mismatch, credential equality with the Migrator, and newly available DDL together.
assert_boundary_intact() {
  local actual_role authenticated_role auth_output auth_status ddl_output ddl_status
  local principal_mismatch=false credential_collapse=false ddl_capability_gained=false

  actual_role="$(configured_api_role)"
  assert_password_authentication_controls "$actual_role"
  set +e
  auth_output="$(api_network_psql 'SELECT current_user;' 2>&1)"
  auth_status=$?
  set -e
  (( auth_status == 0 )) || {
    printf 'ORACLE_SIGNATURE=api-runtime-network-authentication-failed\n' >&2
    return 1
  }
  authenticated_role="$auth_output"
  [[ "$actual_role" == "$designated_api_role" &&
     "$authenticated_role" == "$designated_api_role" ]] || principal_mismatch=true

  if api_credential_matches /run/secrets/database_migrator_password; then
    credential_collapse=true
  fi

  set +e
  ddl_output="$(api_network_psql \
    "CREATE TABLE ${probe_table} (id integer); DROP TABLE ${probe_table};" 2>&1)"
  ddl_status=$?
  set -e

  if (( ddl_status == 0 )); then
    ddl_capability_gained=true
  fi

  if $principal_mismatch && $credential_collapse && $ddl_capability_gained; then
    printf 'ORACLE_SIGNATURE=credential-boundary-collapse\n' >&2
    printf 'ORACLE_SIGNATURE=migrator-ddl-available-to-api-runtime-path\n' >&2
    printf 'ACTUAL_API_RUNTIME_PRINCIPAL=%s\n' "$authenticated_role" >&2
    printf 'DESIGNATED_API_RUNTIME_PRINCIPAL=%s\n' "$designated_api_role" >&2
    return 1
  fi
  if $principal_mismatch; then
    printf 'ORACLE_SIGNATURE=api-runtime-principal-mismatch\n' >&2
    return 1
  fi
  if $credential_collapse; then
    printf 'ORACLE_SIGNATURE=api-runtime-credential-equals-migrator-credential\n' >&2
    return 1
  fi
  if $ddl_capability_gained; then
    printf 'ORACLE_SIGNATURE=migrator-ddl-available-to-api-runtime-path\n' >&2
    return 1
  fi
  [[ "$ddl_output" != *'password authentication failed'* &&
     "$ddl_output" != *'no password supplied'* &&
     "$ddl_output" != *'connection refused'* &&
     ( "$ddl_output" == *'permission denied'* || "$ddl_output" == *'42501'* ) ]] || {
    printf 'ORACLE_SIGNATURE=api-runtime-ddl-denial-not-semantic\n' >&2
    return 1
  }
  api_credential_matches /run/secrets/database_api_password || {
    printf 'ORACLE_SIGNATURE=api-mounted-credential-source-mismatch\n' >&2
    return 1
  }
  return 0
}

expect_red() {
  local output status
  set +e
  output="$(assert_boundary_intact 2>&1)"
  status=$?
  set -e
  (( status != 0 )) || {
    printf 'DB-PRIV-01 mutation oracle unexpectedly returned GREEN.\n' >&2
    return 1
  }
  [[ "$output" == *'ORACLE_SIGNATURE=credential-boundary-collapse'* ]] || {
    printf 'DB-PRIV-01 mutation oracle failed with an unexpected signature: %s\n' "$output" >&2
    return 1
  }
  [[ "$output" == *'ORACLE_SIGNATURE=migrator-ddl-available-to-api-runtime-path'* ]] || {
    printf 'DB-PRIV-01 mutation oracle did not prove DDL through the API-mounted credential: %s\n' "$output" >&2
    return 1
  }
  [[ "$output" == *"ACTUAL_API_RUNTIME_PRINCIPAL=${migrator_role}"* &&
     "$output" == *"DESIGNATED_API_RUNTIME_PRINCIPAL=${designated_api_role}"* ]] || {
    printf 'DB-PRIV-01 mutation oracle did not prove the expected principal mismatch: %s\n' "$output" >&2
    return 1
  }
}

write_override() {
  cat >"$1"
}

run_db_priv_01() {
  local override
  "${compose[@]}" up --build --detach --remove-orphans
  wait_for_state migrator exited
  wait_for_state api running
  wait_for_listener
  assert_boundary_intact
  printf 'DB-PRIV-01: BASELINE_GREEN\n'
  printf 'DB-PRIV-01: BASELINE_ACTUAL_PRINCIPAL=designated-api-principal\n'
  printf 'DB-PRIV-01: BASELINE_MOUNTED_CREDENTIAL_DISTINCT_FROM_MIGRATOR\n'
  printf 'DB-PRIV-01: BASELINE_REPRESENTATIVE_DDL_DENIED\n'

  override="$(mktemp)"
  write_override "$override" <<YAML
services:
  api:
    environment:
      POSTGRES_USERNAME: ${migrator_role}
    secrets:
      - source: database_migrator_password
        target: database_password
YAML
  "${compose[@]}" -f "$override" up --build --detach --no-deps --force-recreate api
  wait_for_state api running
  wait_for_listener
  printf 'DB-PRIV-01: MUTATION_APPLIED\n'

  expect_red
  printf 'DB-PRIV-01: MUTATION_RED\n'
  printf 'DB-PRIV-01: MUTATION_ACTUAL_PRINCIPAL_MISMATCH\n'
  printf 'DB-PRIV-01: MUTATION_MOUNTED_CREDENTIAL_EQUALS_MIGRATOR\n'
  printf 'DB-PRIV-01: ACTUAL_MOUNTED_CREDENTIAL_USED\n'
  printf 'DB-PRIV-01: NETWORK_AUTHENTICATED_DDL_SUCCEEDED\n'
  printf 'DB-PRIV-01: SEMANTIC_FAILURE=credential-boundary-collapse+migrator-ddl-available-to-api-runtime-path\n'
  rm -f "$override"

  "${compose[@]}" up --build --detach --no-deps --force-recreate api
  wait_for_state api running
  wait_for_listener
  assert_boundary_intact
  printf 'DB-PRIV-01: RESTORE_GREEN\n'

  printf 'DB-PRIV-01: KILLED\n'
}

run_db_priv_01

printf 'WP2_DB01_CRITICAL_MUTATION: PASS\n'
