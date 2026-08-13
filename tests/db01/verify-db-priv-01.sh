#!/usr/bin/env bash
set -Eeuo pipefail

# DB-PRIV-01: API runtime must never authenticate or operate using the Migrator principal
# or credential. The mutation collapses that boundary, the oracle must go RED with a
# semantic signature, then the original wiring is restored.

readonly repository_root="$(git rev-parse --show-toplevel)"
readonly project_name="${DB01_PROJECT_NAME:-minimal-bank-system-db-priv-01-${RANDOM}${RANDOM}}"
readonly source_root="${DB01_SOURCE_ROOT:-$repository_root}"
readonly expected_migration='20260809113338_InitialFoundation'
readonly bootstrap_sentinel="${DB01_BOOTSTRAP_SENTINEL:-DB01_PRIV01_BOOTSTRAP_SENTINEL_NOT_A_CREDENTIAL}"
readonly migrator_sentinel="${DB01_MIGRATOR_SENTINEL:-DB01_PRIV01_MIGRATOR_SENTINEL_NOT_A_CREDENTIAL}"
readonly api_sentinel="${DB01_API_SENTINEL:-DB01_PRIV01_API_SENTINEL_NOT_A_CREDENTIAL}"
readonly collapse_table='__db01_priv01_collapse'
readonly override_file="$source_root/tests/db01/.tmp-db-priv-01-override.yaml"
declare -a compose=(docker compose --project-directory "$source_root" -p "$project_name" -f "$source_root/compose.yaml")

export MBS_BOOTSTRAP_PASSWORD="${MBS_BOOTSTRAP_PASSWORD:-$bootstrap_sentinel}"
export MBS_MIGRATOR_PASSWORD="${MBS_MIGRATOR_PASSWORD:-$migrator_sentinel}"
export MBS_API_PASSWORD="${MBS_API_PASSWORD:-$api_sentinel}"

# shellcheck source=lib.sh
source "$repository_root/tests/db01/lib.sh"

db01_postgres_exec() {
  "${compose[@]}" exec -T postgres "$@"
}

db01_api_container_id() {
  local id
  id="$("${compose[@]}" ps -aq api)"
  [[ -n "$id" ]] || {
    printf 'API container was not found.\n' >&2
    return 1
  }
  printf '%s\n' "$id"
}

assert_no_project_residue() {
  local containers volumes networks
  containers="$(docker ps -aq --filter "label=com.docker.compose.project=$project_name")"
  volumes="$(docker volume ls -q --filter "label=com.docker.compose.project=$project_name")"
  networks="$(docker network ls -q --filter "label=com.docker.compose.project=$project_name")"
  [[ -z "$containers" && -z "$volumes" && -z "$networks" ]] || {
    printf 'Project-scoped Docker residue remains after DB-PRIV-01.\n' >&2
    return 1
  }
}

cleanup() {
  "${compose[@]}" down --volumes --remove-orphans || true
  rm -f "$override_file"
  assert_no_project_residue
}

trap cleanup EXIT

for command_name in docker jq bash; do
  command -v "$command_name" >/dev/null || {
    printf 'DB-PRIV-01 prerequisite missing: %s\n' "$command_name" >&2
    exit 69
  }
done

db01_require_distinct_host_secrets

container_state() {
  docker inspect "$("${compose[@]}" ps -aq "$1")" | jq --raw-output '.[0].State.Status'
}

wait_for_state() {
  local service="$1" expected="$2" attempt
  for attempt in $(seq 1 90); do
    if [[ "$(container_state "$service")" == "$expected" ]]; then
      return 0
    fi
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
  printf 'API listener did not become reachable.\n' >&2
  return 1
}

set_compose_files() {
  compose=(docker compose --project-directory "$source_root" -p "$project_name" -f "$source_root/compose.yaml")
  while (($#)); do
    compose+=(-f "$1")
    shift
  done
}

bring_up() {
  "${compose[@]}" up --build --detach --remove-orphans
  wait_for_state migrator exited
  wait_for_state api running
  wait_for_api_listener
  [[ "$(db01_read_history_as "$DB01_MIGRATOR_ROLE")" == *"$expected_migration"* ]] || {
    printf 'Expected migration history is missing.\n' >&2
    return 1
  }
}

credential_boundary_oracle() {
  local actual_user designated_user api_digest migrator_digest ddl_output ddl_status
  designated_user="$DB01_API_ROLE"
  actual_user="$(docker inspect "$(db01_api_container_id)" | jq --raw-output '.[0].Config.Env[] | select(startswith("POSTGRES_USERNAME=")) | split("=")[1]')"
  api_digest="$(db01_api_secret_digest)"
  migrator_digest="$(db01_postgres_secret_digest "$DB01_MIGRATOR_SECRET_FILE")"

  db01_drop_table_as_migrator "$collapse_table"

  set +e
  ddl_output="$(db01_try_ddl_with_api_mounted_secret "$collapse_table" 2>&1)"
  ddl_status=$?
  set -e
  db01_drop_table_as_migrator "$collapse_table"

  if [[ "$actual_user" != "$designated_user" && "$api_digest" == "$migrator_digest" && "$ddl_status" -eq 0 ]]; then
    printf 'ORACLE_SIGNATURE=credential-boundary-collapse\n' >&2
    printf 'ORACLE_SIGNATURE=migrator-ddl-available-to-api-runtime-path\n' >&2
    printf 'ACTUAL_API_RUNTIME_PRINCIPAL=%s\n' "$actual_user" >&2
    printf 'DESIGNATED_API_RUNTIME_PRINCIPAL=%s\n' "$designated_user" >&2
    return 1
  fi

  if [[ "$actual_user" != "$designated_user" ]]; then
    printf 'ORACLE_SIGNATURE=credential-boundary-collapse\n' >&2
    printf 'ACTUAL_API_RUNTIME_PRINCIPAL=%s\n' "$actual_user" >&2
    return 1
  fi

  if [[ "$api_digest" == "$migrator_digest" ]]; then
    printf 'ORACLE_SIGNATURE=credential-boundary-collapse\n' >&2
    return 1
  fi

  if [[ "$ddl_status" -eq 0 ]]; then
    printf 'ORACLE_SIGNATURE=migrator-ddl-available-to-api-runtime-path\n' >&2
    return 1
  fi

  [[ "$(db01_current_user_as "$DB01_API_ROLE")" == "$DB01_API_ROLE" ]] || {
    printf 'ORACLE_SIGNATURE=api-principal-unauthenticated\n' >&2
    return 1
  }
  return 0
}

expect_red() {
  local expected_signature="$1"
  shift
  local output status
  set +e
  output="$("$@" 2>&1)"
  status=$?
  set -e
  (( status != 0 )) || {
    printf 'DB-PRIV-01 oracle unexpectedly returned GREEN.\n' >&2
    return 1
  }
  [[ "$output" == *"ORACLE_SIGNATURE=$expected_signature"* ]] || {
    printf 'DB-PRIV-01 oracle failed with an invalid signature.\n' >&2
    return 1
  }
  [[ "$output" == *'ORACLE_SIGNATURE=migrator-ddl-available-to-api-runtime-path'* ]] || {
    printf 'DB-PRIV-01 oracle did not prove newly available Migrator DDL.\n' >&2
    return 1
  }
  printf '%s\n' "$output"
}

set_compose_files
bring_up
credential_boundary_oracle
printf 'DB-PRIV-01: BASELINE_GREEN\n'

cat >"$override_file" <<'YAML'
services:
  api:
    environment:
      POSTGRES_USERNAME: mbs_migrator
      MBS_DATABASE_PASSWORD_FILE: /run/secrets/migrator_password
    secrets:
      - source: migrator_password
        target: migrator_password
YAML

"${compose[@]}" down --volumes --remove-orphans
set_compose_files "$override_file"
bring_up
printf 'DB-PRIV-01: MUTATION_APPLIED\n'
expect_red credential-boundary-collapse credential_boundary_oracle
printf 'DB-PRIV-01: MUTATION_RED\n'
printf 'DB-PRIV-01: SEMANTIC_FAILURE=credential-boundary-collapse+migrator-ddl-available-to-api-runtime-path\n'

"${compose[@]}" down --volumes --remove-orphans
set_compose_files
bring_up
credential_boundary_oracle
printf 'DB-PRIV-01: RESTORE_GREEN\n'
printf 'DB-PRIV-01: PASS\n'
