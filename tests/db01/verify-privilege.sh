#!/usr/bin/env bash
set -Eeuo pipefail

readonly project_name="${DB01_PROJECT_NAME:-minimal-bank-system-db01-privilege-${RANDOM}${RANDOM}}"
readonly source_root="${DB01_SOURCE_ROOT:-$(git rev-parse --show-toplevel)}"
readonly expected_migration='20260809113338_InitialFoundation'
readonly bootstrap_sentinel="${DB01_BOOTSTRAP_SENTINEL:-DB01_BOOTSTRAP_SENTINEL_NOT_A_CREDENTIAL}"
readonly migrator_sentinel="${DB01_MIGRATOR_SENTINEL:-DB01_MIGRATOR_SENTINEL_NOT_A_CREDENTIAL}"
readonly api_sentinel="${DB01_API_SENTINEL:-DB01_API_SENTINEL_NOT_A_CREDENTIAL}"
readonly compose=(docker compose --project-directory "$source_root" -p "$project_name" -f "$source_root/compose.yaml")

export MBS_BOOTSTRAP_PASSWORD="${MBS_BOOTSTRAP_PASSWORD:-$bootstrap_sentinel}"
export MBS_MIGRATOR_PASSWORD="${MBS_MIGRATOR_PASSWORD:-$migrator_sentinel}"
export MBS_API_PASSWORD="${MBS_API_PASSWORD:-$api_sentinel}"

# shellcheck source=lib.sh
source "$source_root/tests/db01/lib.sh"

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
    printf 'Project-scoped Docker residue remains after DB-01 privilege verification.\n' >&2
    return 1
  }
}

cleanup() {
  "${compose[@]}" down --volumes --remove-orphans
  assert_no_project_residue
}

trap cleanup EXIT

for command_name in docker jq bash; do
  command -v "$command_name" >/dev/null || {
    printf 'DB-01 privilege verification prerequisite missing: %s\n' "$command_name" >&2
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

assert_no_disclosure() {
  local postgres_id migrator_id api_id rendered logs inspect top surface sentinel
  postgres_id="$("${compose[@]}" ps -aq postgres)"
  migrator_id="$("${compose[@]}" ps -aq migrator)"
  api_id="$(db01_api_container_id)"
  rendered="$("${compose[@]}" config --format json)"
  logs="$("${compose[@]}" logs --no-color --timestamps)"
  inspect="$(docker inspect "$postgres_id" "$migrator_id" "$api_id")"
  top="$(docker top "$api_id")"
  for sentinel in "$bootstrap_sentinel" "$migrator_sentinel" "$api_sentinel"; do
    for surface in "$rendered" "$logs" "$inspect" "$top"; do
      [[ "$surface" != *"$sentinel"* ]] || {
        printf 'DB credential sentinel was exposed by an observation surface.\n' >&2
        return 1
      }
    done
  done
}

"${compose[@]}" up --build --detach --remove-orphans
wait_for_state migrator exited
wait_for_state api running
wait_for_api_listener

[[ "$(db01_current_user_as "$DB01_API_ROLE")" == "$DB01_API_ROLE" ]]
[[ "$(db01_current_user_as "$DB01_MIGRATOR_ROLE")" == "$DB01_MIGRATOR_ROLE" ]]
db01_assert_api_env_principal "$DB01_API_ROLE"
[[ "$(db01_api_secret_digest)" == "$(db01_postgres_secret_digest "$DB01_API_SECRET_FILE")" ]]
[[ "$(db01_api_secret_digest)" != "$(db01_postgres_secret_digest "$DB01_MIGRATOR_SECRET_FILE")" ]]

history="$(db01_read_history_as "$DB01_API_ROLE")"
[[ "$history" == *"$expected_migration"* ]] || {
  printf 'Role-aware API runtime migration-history read failed.\n' >&2
  exit 1
}
migrator_history="$(db01_read_history_as "$DB01_MIGRATOR_ROLE")"
[[ "$migrator_history" == "$history" ]] || {
  printf 'Migrator and API runtime saw different migration history.\n' >&2
  exit 1
}

db01_assert_privilege_ceiling
db01_assert_positive_dml
db01_assert_negative_privilege
assert_no_disclosure

printf 'DB01_DISTINCT_PRINCIPALS: PASS\n'
printf 'DB01_DISTINCT_CREDENTIALS: PASS\n'
printf 'DB01_BOOTSTRAP_BOUNDARY: PASS\n'
printf 'DB01_MIGRATOR_PRIVILEGE_CEILING: PASS\n'
printf 'DB01_API_LEAST_PRIVILEGE: PASS\n'
printf 'DB01_ROLE_AWARE_HISTORY: PASS\n'
printf 'DB01_POSITIVE_DML: PASS\n'
printf 'DB01_NEGATIVE_PRIVILEGE: PASS\n'
printf 'DB01_NON_DISCLOSURE: PASS\n'
printf 'DB01_PRIVILEGE_VERIFICATION: PASS\n'
