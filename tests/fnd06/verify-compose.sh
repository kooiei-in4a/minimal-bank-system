#!/usr/bin/env bash
set -Eeuo pipefail

readonly project_name="${FND06_PROJECT_NAME:-minimal-bank-system-fnd06}"
readonly secret_sentinel="${FND06_SECRET_SENTINEL:-FND06_TEST_SENTINEL_NOT_A_CREDENTIAL}"
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

export MBS_DATABASE_PASSWORD="${MBS_DATABASE_PASSWORD:-$secret_sentinel}"

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

container_health() {
  docker inspect "$(container_id "$1")" | jq --raw-output '.[0].State.Health.Status // "none"'
}

wait_for_state() {
  local service_name="$1" expected_state="$2" attempt
  for attempt in $(seq 1 90); do
    [[ "$(container_state "$service_name")" == "$expected_state" ]] && return 0
    sleep 1
  done
  printf '%s did not reach state %s.\n' "$service_name" "$expected_state" >&2
  return 1
}

wait_for_health() {
  local service_name="$1" expected_health="$2" attempt
  for attempt in $(seq 1 90); do
    [[ "$(container_health "$service_name")" == "$expected_health" ]] && return 0
    sleep 1
  done
  printf '%s did not reach health state %s.\n' "$service_name" "$expected_health" >&2
  return 1
}

probe() {
  local path="$1" expected_status="$2" expect_success="$3"
  local output exit_code

  set +e
  output="$("${compose[@]}" exec -T api dotnet MinimalBankSystem.Api.dll --health-probe "$path" 2>&1)"
  exit_code=$?
  set -e

  [[ "$output" == *"HEALTH_PROBE_STATUS=$expected_status"* ]] || {
    printf 'Unexpected probe result for %s: %s\n' "$path" "$output" >&2
    return 1
  }

  if [[ "$expect_success" == true ]]; then
    (( exit_code == 0 )) || return 1
  else
    (( exit_code != 0 )) || return 1
  fi
}

wait_for_ready_probe() {
  local attempt
  for attempt in $(seq 1 30); do
    if probe /health/ready 200 true; then
      return 0
    fi
    sleep 1
  done
  printf 'Readiness did not recover after PostgreSQL restarted.\n' >&2
  return 1
}

assert_no_project_residue() {
  local containers volumes networks
  containers="$(docker ps -aq --filter "label=com.docker.compose.project=$project_name")"
  volumes="$(docker volume ls -q --filter "label=com.docker.compose.project=$project_name")"
  networks="$(docker network ls -q --filter "label=com.docker.compose.project=$project_name")"
  [[ -z "$containers" && -z "$volumes" && -z "$networks" ]] || {
    printf 'Project-scoped Docker residue remains.\n' >&2
    return 1
  }
}

cleanup() {
  "${compose[@]}" down --volumes --remove-orphans
  assert_no_project_residue
}

trap cleanup EXIT

"${compose[@]}" config --quiet
"${compose[@]}" up --build --detach --remove-orphans
wait_for_state migrator exited
wait_for_state api running
wait_for_health api healthy

probe /health/live 200 true
probe /health/ready 200 true

api_id_before="$(container_id api)"
api_started_before="$(docker inspect "$api_id_before" | jq --raw-output '.[0].State.StartedAt')"

"${compose[@]}" stop postgres
wait_for_state postgres exited
[[ "$(container_state api)" == running ]]
[[ "$(container_id api)" == "$api_id_before" ]]

probe /health/live 200 true
probe /health/ready 503 false
wait_for_health api unhealthy

"${compose[@]}" start postgres
wait_for_health postgres healthy
wait_for_ready_probe
wait_for_health api healthy

[[ "$(container_id api)" == "$api_id_before" ]]
[[ "$(docker inspect "$api_id_before" | jq --raw-output '.[0].State.StartedAt')" == "$api_started_before" ]]
probe /health/live 200 true
probe /health/ready 200 true

printf 'LIVE_NORMAL: PASS\n'
printf 'READY_NORMAL: PASS\n'
printf 'POSTGRES_STOP_LIVE: PASS\n'
printf 'POSTGRES_STOP_READY: PASS\n'
printf 'POSTGRES_RECOVERY_WITHOUT_API_RESTART: PASS\n'
printf 'RUNTIME_DOTNET_ENDPOINT_PROBE: PASS\n'
printf 'COMPOSE_HEALTH_SEMANTICS: PASS\n'
