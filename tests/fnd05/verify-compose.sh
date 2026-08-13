#!/usr/bin/env bash
set -Eeuo pipefail

readonly project_name="${FND05_PROJECT_NAME:-minimal-bank-system-fnd05}"
readonly expected_migration='20260809113338_InitialFoundation'
readonly source_root="${FND05_SOURCE_ROOT:-$(git rev-parse --show-toplevel)}"
readonly bootstrap_sentinel="${FND05_BOOTSTRAP_SENTINEL:-FND05_BOOTSTRAP_SENTINEL_NOT_A_CREDENTIAL}"
readonly migrator_sentinel="${FND05_MIGRATOR_SENTINEL:-FND05_MIGRATOR_SENTINEL_NOT_A_CREDENTIAL}"
readonly api_sentinel="${FND05_API_SENTINEL:-FND05_API_SENTINEL_NOT_A_CREDENTIAL}"
readonly compose=(docker compose --project-directory "$source_root" -p "$project_name" -f "$source_root/compose.yaml")
declare -a cleanup_project_names=("$project_name")

require_command() {
  command -v "$1" >/dev/null
}

for command_name in docker jq bash; do
  require_command "$command_name" || {
    printf 'Compose verification prerequisite missing: %s\n' "$command_name" >&2
    exit 69
  }
done

export MBS_BOOTSTRAP_PASSWORD="${MBS_BOOTSTRAP_PASSWORD:-$bootstrap_sentinel}"
export MBS_MIGRATOR_PASSWORD="${MBS_MIGRATOR_PASSWORD:-$migrator_sentinel}"
export MBS_API_PASSWORD="${MBS_API_PASSWORD:-$api_sentinel}"

# shellcheck source=../db01/lib.sh
source "$source_root/tests/db01/lib.sh"

db01_postgres_exec() {
  "${compose[@]}" exec -T postgres "$@"
}

db01_api_container_id() {
  container_id api
}

db01_require_distinct_host_secrets

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
  db01_read_history_as "$DB01_API_ROLE"
}

assert_no_project_residue() {
  assert_no_project_residue_for "$project_name"
}

assert_no_project_residue_for() {
  local target_project_name="$1"
  local containers volumes networks
  containers="$(docker ps -aq --filter "label=com.docker.compose.project=$target_project_name")"
  volumes="$(docker volume ls -q --filter "label=com.docker.compose.project=$target_project_name")"
  networks="$(docker network ls -q --filter "label=com.docker.compose.project=$target_project_name")"
  [[ -z "$containers" && -z "$volumes" && -z "$networks" ]] || {
    printf 'Project-scoped Docker residue remains after canonical clean reset.\n' >&2
    return 1
  }
}

cleanup_safety() {
  local cleanup_project_name
  for cleanup_project_name in "${cleanup_project_names[@]}"; do
    docker compose --project-directory "$source_root" -p "$cleanup_project_name" -f "$source_root/compose.yaml" down --volumes --remove-orphans
    assert_no_project_residue_for "$cleanup_project_name"
  done
}

trap cleanup_safety EXIT

assert_no_secret_disclosure() {
  local observation_surface="$1"
  local sentinel
  for sentinel in "$bootstrap_sentinel" "$migrator_sentinel" "$api_sentinel"; do
    [[ "$observation_surface" != *"$sentinel"* ]] || {
      printf 'Secret sentinel was exposed by an external observation surface.\n' >&2
      return 1
    }
  done
}

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
  [[ "$(db01_current_user_as "$DB01_API_ROLE")" == "$DB01_API_ROLE" ]] || {
    printf 'API runtime principal was not the designated API role.\n' >&2
    return 1
  }
  [[ "$(jq --raw-output '.[0].Config.Env[] | select(startswith("POSTGRES_USERNAME=")) | split("=")[1]' <<<"$api")" == "$DB01_API_ROLE" ]] || {
    printf 'API container is not wired to the designated API principal.\n' >&2
    return 1
  }
  [[ "$(jq --raw-output '.[0].Config.Env[] | select(startswith("POSTGRES_USERNAME=")) | split("=")[1]' <<<"$migrator")" == "$DB01_MIGRATOR_ROLE" ]] || {
    printf 'Migrator container is not wired to the designated Migrator principal.\n' >&2
    return 1
  }
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
    assert_no_secret_disclosure "$observation_surface"
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

assert_missing_secret_contract() {
  local missing_project_name="$1" missing_up_output="$2" missing_up_exit_code="$3" missing_secret_name="$4"
  local api_id rendered inspect
  local -a container_ids=()
  local missing_compose=(docker compose --project-directory "$source_root" -p "$missing_project_name" -f "$source_root/compose.yaml")
  (( missing_up_exit_code != 0 )) || {
    printf 'Missing-secret probe incorrectly returned success.\n' >&2
    return 1
  }
  [[ "$missing_up_output" == *"$missing_secret_name"* && "$missing_up_output" == *'required by secret'* ]] || {
    printf 'Missing-secret probe did not report the required-secret configuration failure.\n' >&2
    return 1
  }
  api_id="$("${missing_compose[@]}" ps -aq api)"
  if [[ -n "$api_id" ]]; then
    docker inspect "$api_id" | jq --exit-status '
      .[0].State.Status == "created" and
      .[0].State.StartedAt == "0001-01-01T00:00:00Z"
    ' >/dev/null || {
      printf 'Missing-secret probe allowed API startup.\n' >&2
      return 1
    }
  fi
  mapfile -t container_ids < <(docker ps -aq --filter "label=com.docker.compose.project=$missing_project_name")
  if ((${#container_ids[@]})); then
    inspect="$(docker inspect "${container_ids[@]}")"
    jq --exit-status 'all(.[]; .State.StartedAt == "0001-01-01T00:00:00Z")' <<<"$inspect" >/dev/null || {
      printf 'Missing-secret probe started a service before fail-closed handling.\n' >&2
      return 1
    }
  else
    inspect='[]'
  fi
  set +e
  rendered="$(env -u "$missing_secret_name" "${missing_compose[@]}" config --format json 2>/dev/null)"
  set -e
  rendered="${rendered:-}"
  for observation_surface in "$missing_up_output" "$rendered" "$inspect"; do
    assert_no_secret_disclosure "$observation_surface"
  done
}

run_missing_secret_probe() {
  local missing_secret_name="$1"
  local probe_label="$2"
  local missing_project_name="${project_name}-${probe_label}-${RANDOM}${RANDOM}"
  local -a missing_compose=(docker compose --project-directory "$source_root" -p "$missing_project_name" -f "$source_root/compose.yaml")
  local missing_up_output missing_up_exit_code

  cleanup_project_names+=("$missing_project_name")

  set +e
  missing_up_output="$(env -u "$missing_secret_name" "${missing_compose[@]}" up --build --detach --remove-orphans 2>&1)"
  missing_up_exit_code=$?
  set -e
  assert_missing_secret_contract "$missing_project_name" "$missing_up_output" "$missing_up_exit_code" "$missing_secret_name"
  "${missing_compose[@]}" down --volumes --remove-orphans
  assert_no_project_residue_for "$missing_project_name"
  printf 'MISSING_SECRET: NEGATIVE_PROBE_EXECUTED name=%s\n' "$missing_secret_name"
  printf 'MISSING_SECRET: FAIL_CLOSED_OBSERVED name=%s\n' "$missing_secret_name"
  printf 'MISSING_SECRET: EXPECTED_FAILURE_SIGNATURE=required-secret-configuration name=%s\n' "$missing_secret_name"
  printf 'MISSING_SECRET: API_NOT_SERVING name=%s\n' "$missing_secret_name"
  printf 'MISSING_SECRET: NO_LEAK name=%s\n' "$missing_secret_name"
  printf 'MISSING_SECRET: CLEANUP name=%s\n' "$missing_secret_name"
  printf 'MISSING_SECRET: RESIDUE_ZERO name=%s\n' "$missing_secret_name"
  printf 'MISSING_SECRET_PROBE: PASS name=%s (compose-up-exit=%s)\n' "$missing_secret_name" "$missing_up_exit_code"
}

"${compose[@]}" config --quiet
FND05_SOURCE_ROOT="$source_root" FND05_PROJECT_NAME="$project_name" bash "$source_root/tests/fnd05/static-gate.sh"

run_missing_secret_probe MBS_API_PASSWORD runtime
run_missing_secret_probe MBS_MIGRATOR_PASSWORD migrator

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
failure_up_output="$("${compose[@]}" -f "$source_root/tests/fnd05/failure-compose.yaml" up --build --detach --remove-orphans 2>&1)"
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
