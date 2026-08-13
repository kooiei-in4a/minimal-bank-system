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
readonly probe_table='public.db_priv_01_probe'

export MBS_DATABASE_BOOTSTRAP_PASSWORD="${MBS_DATABASE_BOOTSTRAP_PASSWORD:-${sentinel}_BOOTSTRAP}"
export MBS_DATABASE_MIGRATOR_PASSWORD="${MBS_DATABASE_MIGRATOR_PASSWORD:-${sentinel}_MIGRATOR}"
export MBS_DATABASE_API_PASSWORD="${MBS_DATABASE_API_PASSWORD:-${sentinel}_API}"

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

psql_as() {
  local role="$1" sql="$2"
  "${compose[@]}" exec -T postgres psql -U "$role" -d minimal_bank -At -c "$sql"
}

configured_api_role() {
  "${compose[@]}" exec -T api printenv POSTGRES_USERNAME | tr -d '\r'
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

# Asserts the DB-PRIV-01 invariant holds: the API service's actual configured principal matches
# its designated principal, and that principal's credential path cannot perform representative
# DDL. Returns non-zero with a combined ORACLE_SIGNATURE describing exactly which part of the
# invariant failed, so the mutation below is killed by a semantic signature rather than a generic
# non-zero exit.
assert_boundary_intact() {
  local actual_role ddl_status principal_mismatch=false ddl_capability_gained=false

  actual_role="$(configured_api_role)"
  [[ "$actual_role" == "$designated_api_role" ]] || principal_mismatch=true

  set +e
  psql_as "$actual_role" "CREATE TABLE ${probe_table} (id integer);" >/dev/null 2>&1
  ddl_status=$?
  set -e

  if (( ddl_status == 0 )); then
    ddl_capability_gained=true
    psql_as "$migrator_role" "DROP TABLE IF EXISTS ${probe_table};" >/dev/null 2>&1 || true
  fi

  if $principal_mismatch && $ddl_capability_gained; then
    printf 'ORACLE_SIGNATURE=credential-boundary-collapse:actual-role=%s:designated-role=%s:migrator-ddl-capability-gained\n' \
      "$actual_role" "$designated_api_role" >&2
    return 1
  fi
  if $principal_mismatch; then
    printf 'ORACLE_SIGNATURE=principal-mismatch-without-ddl-capability:actual-role=%s:designated-role=%s\n' \
      "$actual_role" "$designated_api_role" >&2
    return 1
  fi
  if $ddl_capability_gained; then
    printf 'ORACLE_SIGNATURE=ddl-capability-gained-without-principal-mismatch:actual-role=%s\n' "$actual_role" >&2
    return 1
  fi
  return 0
}

expect_red() {
  local expected_signature="$1"
  local output status
  set +e
  output="$(assert_boundary_intact 2>&1)"
  status=$?
  set -e
  (( status != 0 )) || {
    printf 'DB-PRIV-01 mutation oracle unexpectedly returned GREEN.\n' >&2
    return 1
  }
  [[ "$output" == *"ORACLE_SIGNATURE=$expected_signature"* ]] || {
    printf 'DB-PRIV-01 mutation oracle failed with an unexpected signature: %s\n' "$output" >&2
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

  expect_red 'credential-boundary-collapse:actual-role=minimal_bank_migrator:designated-role=minimal_bank_api:migrator-ddl-capability-gained'
  printf 'DB-PRIV-01: MUTATION_RED\n'
  printf 'DB-PRIV-01: SEMANTIC_SIGNATURE=credential-boundary-collapse+migrator-ddl-capability-gained\n'
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
