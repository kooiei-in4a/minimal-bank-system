#!/usr/bin/env bash
set -Eeuo pipefail

readonly project_name="${FND05_PROJECT_NAME:-minimal-bank-system-fnd05}"
readonly expected_migration='20260809113338_InitialFoundation'
readonly sentinel="${FND05_SECRET_SENTINEL:-FND05_TEST_SENTINEL_NOT_A_CREDENTIAL}"
readonly compose=(docker compose -p "$project_name")

require_command() {
  command -v "$1" >/dev/null
}

for command_name in docker jq bash; do
  require_command "$command_name" || {
    printf 'Compose verification prerequisite missing: %s\n' "$command_name" >&2
    exit 69
  }
done

export MBS_DATABASE_PASSWORD="${MBS_DATABASE_PASSWORD:-$sentinel}"

container_id() {
  local service_name="$1"
  local id
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
  local service_name="$1"
  local expected_state="$2"
  local attempt

  for attempt in $(seq 1 90); do
    if [[ "$(container_state "$service_name")" == "$expected_state" ]]; then
      return 0
    fi
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

read_history() {
  "${compose[@]}" exec -T postgres psql -U minimal_bank -d minimal_bank -At \
    -c 'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId";'
}

assert_no_project_residue() {
  local containers volumes networks
  containers="$(docker ps -aq --filter "label=com.docker.compose.project=$project_name")"
  volumes="$(docker volume ls -q --filter "label=com.docker.compose.project=$project_name")"
  networks="$(docker network ls -q --filter "label=com.docker.compose.project=$project_name")"
  [[ -z "$containers" && -z "$volumes" && -z "$networks" ]] || {
    printf 'Project-scoped Docker residue remains after canonical clean reset.\n' >&2
    return 1
  }
}

cleanup_safety() {
  "${compose[@]}" down --volumes --remove-orphans
  assert_no_project_residue
}

trap cleanup_safety EXIT

assert_success_contract() {
  local postgres_id migrator_id api_id migrator api history rendered logs inspect top
  postgres_id="$(container_id postgres)"
  migrator_id="$(container_id migrator)"
  api_id="$(container_id api)"
  migrator="$(docker inspect "$migrator_id")"
  api="$(docker inspect "$api_id")"

  jq --exit-status --arg project "$project_name" '
    .[0].State.Status == "exited" and
    .[0].State.ExitCode == 0 and
    .[0].Config.Labels["com.docker.compose.project"] == $project and
    .[0].Config.Labels["com.docker.compose.service"] == "migrator"
  ' <<<"$migrator" >/dev/null
  jq --exit-status \
    --arg project "$project_name" \
    --arg migrator_finished "$(jq --raw-output '.[0].State.FinishedAt' <<<"$migrator")" \
    '
      .[0].State.Status == "running" and
      .[0].Config.Labels["com.docker.compose.project"] == $project and
      .[0].Config.Labels["com.docker.compose.service"] == "api" and
      .[0].State.StartedAt >= $migrator_finished
    ' <<<"$api" >/dev/null

  wait_for_api_listener
  history="$(read_history)"
  [[ "$history" == *"$expected_migration"* ]] || {
    printf 'Expected migration history is missing.\n' >&2
    return 1
  }

  rendered="$("${compose[@]}" config --format json)"
  logs="$("${compose[@]}" logs --no-color --timestamps)"
  inspect="$(docker inspect "$postgres_id" "$migrator_id" "$api_id")"
  top="$(docker top "$api_id")"
  for observation_surface in "$rendered" "$logs" "$inspect" "$top"; do
    [[ "$observation_surface" != *"$sentinel"* ]] || {
      printf 'Secret sentinel was exposed by an external observation surface.\n' >&2
      return 1
    }
  done
}

assert_failure_contract() {
  local migrator_id api_id migrator api logs
  migrator_id="$(container_id migrator)"
  api_id="$(container_id api)"
  migrator="$(docker inspect "$migrator_id")"
  api="$(docker inspect "$api_id")"
  logs="$("${compose[@]}" logs --no-color --timestamps migrator)"

  jq --exit-status '.[0].State.Status == "exited" and .[0].State.ExitCode != 0' <<<"$migrator" >/dev/null
  jq --exit-status '.[0].State.StartedAt == "0001-01-01T00:00:00Z" or .[0].State.Status == "created"' <<<"$api" >/dev/null
  [[ "$logs" == *'Migration failed. The deployment must not continue.'* ]] || {
    printf 'The intended Migrator failure-path marker is missing.\n' >&2
    return 1
  }
}

"${compose[@]}" config --quiet
bash tests/fnd05/static-gate.sh

"${compose[@]}" up --build --detach --remove-orphans
wait_for_state migrator exited
wait_for_state api running
assert_success_contract
initial_history="$(read_history)"

"${compose[@]}" down --remove-orphans
volume_id="$(docker volume ls -q --filter "label=com.docker.compose.project=$project_name")"
[[ -n "$volume_id" ]] || {
  printf 'Retained-data stop removed the PostgreSQL named volume.\n' >&2
  exit 1
}
docker volume inspect "$volume_id" | jq --exit-status --arg project "$project_name" '
  .[0].Labels["com.docker.compose.project"] == $project and
  .[0].Labels["com.docker.compose.volume"] == "postgres_data"
' >/dev/null

"${compose[@]}" up --build --detach --remove-orphans
wait_for_state migrator exited
wait_for_state api running
assert_success_contract
[[ "$(read_history)" == "$initial_history" ]] || {
  printf 'Migrator rerun changed retained migration history.\n' >&2
  exit 1
}

"${compose[@]}" down --volumes --remove-orphans
assert_no_project_residue

set +e
failure_up_output="$("${compose[@]}" -f compose.yaml -f tests/fnd05/failure-compose.yaml up --build --detach --remove-orphans 2>&1)"
failure_up_exit_code=$?
set -e
if (( failure_up_exit_code != 0 )); then
  expected_failure_compose_message="service \"migrator\" didn't complete successfully"
  [[ "$failure_up_output" == *"$expected_failure_compose_message"* ]] || {
    printf 'Failure fixture stopped before the intended Migrator failure contract.\n' >&2
    exit 1
  }
fi
wait_for_state migrator exited
assert_failure_contract

printf 'COMPOSE_RUNTIME_VERIFICATION: PASS\n'
