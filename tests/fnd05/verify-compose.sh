#!/usr/bin/env bash
set -Eeuo pipefail

readonly project_name="${FND05_PROJECT_NAME:-minimal-bank-system-fnd05}"
readonly expected_migration='20260809113338_InitialFoundation'
readonly sentinel="${FND05_SECRET_SENTINEL:-FND05_TEST_SENTINEL_NOT_A_CREDENTIAL}"
readonly compose=(docker compose -p "$project_name")
declare -a cleanup_project_names=("$project_name")
declare -a secret_materials=()

require_command() {
  command -v "$1" >/dev/null
}

for command_name in docker jq bash; do
  require_command "$command_name" || {
    printf 'Compose verification prerequisite missing: %s\n' "$command_name" >&2
    exit 69
  }
done

export MBS_DATABASE_BOOTSTRAP_PASSWORD="${MBS_DATABASE_BOOTSTRAP_PASSWORD:-FND05_BOOTSTRAP_NOT_A_CREDENTIAL}"
export MBS_DATABASE_MIGRATOR_PASSWORD="${MBS_DATABASE_MIGRATOR_PASSWORD:-FND05_MIGRATOR_NOT_A_CREDENTIAL}"
export MBS_DATABASE_RUNTIME_PASSWORD="${MBS_DATABASE_RUNTIME_PASSWORD:-$sentinel}"
secret_materials=(
  "$MBS_DATABASE_BOOTSTRAP_PASSWORD"
  "$MBS_DATABASE_MIGRATOR_PASSWORD"
  "$MBS_DATABASE_RUNTIME_PASSWORD"
)

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

api_username() {
  docker inspect "$(container_id api)" |
    jq --raw-output '.[0].Config.Env[] | select(startswith("POSTGRES_USERNAME=")) | split("=")[1]'
}

api_psql() {
  local sql="$1"
  local username
  username="$(api_username)"
  "${compose[@]}" exec -T api bash -c 'cat /run/secrets/runtime_password' |
    "${compose[@]}" exec -T postgres bash -ceu '
      IFS= read -r PGPASSWORD || true
      export PGPASSWORD
      exec psql -h 127.0.0.1 -U "$1" -d minimal_bank -v ON_ERROR_STOP=1 -At -c "$2"
    ' bash "$username" "$sql"
}

read_history() {
  api_psql 'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId";'
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
    docker compose -p "$cleanup_project_name" down --volumes --remove-orphans
    assert_no_project_residue_for "$cleanup_project_name"
  done
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
  [[ "$(api_psql 'SELECT current_user;')" == 'mbs_runtime' ]] || {
    printf 'API did not use the designated runtime principal.\n' >&2
    return 1
  }
  for observation_surface in "$rendered" "$logs" "$inspect" "$top"; do
    for secret_material in "${secret_materials[@]}"; do
      [[ "$observation_surface" != *"$secret_material"* ]] || {
        printf 'Secret material was exposed by an external observation surface.\n' >&2
        return 1
      }
    done
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
  local missing_variable="$1" missing_label="$2" missing_project_name="$3" missing_up_output="$4" missing_up_exit_code="$5"
  local api_id service_id service_name inspect
  local -a container_ids=()
  local missing_compose=(docker compose -p "$missing_project_name")
  (( missing_up_exit_code != 0 )) || {
    printf 'Missing-secret probe incorrectly returned success.\n' >&2
    return 1
  }
  [[ "$missing_up_output" == *"$missing_variable"* && "$missing_up_output" == *'required by secret'* ]] || {
    printf 'Missing-secret probe did not report the required-secret configuration failure.\n' >&2
    return 1
  }
  api_id="$("${missing_compose[@]}" ps -aq api)"
  if [[ -n "$api_id" ]]; then
    docker inspect "$api_id" | jq --exit-status '
      .[0].State.Status != "running" and
      (.[0].State.StartedAt == "0001-01-01T00:00:00Z" or .[0].State.ExitCode != 0)
    ' >/dev/null || {
      printf 'Missing-secret probe allowed API startup.\n' >&2
      return 1
    }
  fi
  case "$missing_label" in
    bootstrap) service_name='db-provisioner' ;;
    migrator) service_name='migrator' ;;
    runtime) service_name='api' ;;
    *)
      printf 'Unknown missing-secret probe label: %s\n' "$missing_label" >&2
      return 1
      ;;
  esac
  service_id="$("${missing_compose[@]}" ps -aq "$service_name")"
  if [[ -n "$service_id" ]]; then
    docker inspect "$service_id" | jq --exit-status '.[0].State.Status != "running"' >/dev/null || {
      printf 'Missing-secret probe left %s running before fail-closed handling.\n' "$service_name" >&2
      return 1
    }
  fi
  mapfile -t container_ids < <(docker ps -aq --filter "label=com.docker.compose.project=$missing_project_name")
  if ((${#container_ids[@]})); then
    inspect="$(docker inspect "${container_ids[@]}")"
  else
    inspect='[]'
  fi
  for observation_surface in "$missing_up_output" "$inspect"; do
    for secret_material in "${secret_materials[@]}"; do
      [[ "$observation_surface" != *"$secret_material"* ]] || {
        printf 'Missing-secret probe exposed secret material.\n' >&2
        return 1
      }
    done
  done
}

run_missing_secret_probe() {
  local missing_variable="$1"
  local missing_label="$2"
  local missing_project_name="${project_name}-missing-${missing_label}-${RANDOM}${RANDOM}"
  local -a missing_compose=(docker compose -p "$missing_project_name")
  local missing_up_output missing_up_exit_code

  cleanup_project_names+=("$missing_project_name")

  set +e
  missing_up_output="$(env -u "$missing_variable" "${missing_compose[@]}" up --build --detach --remove-orphans 2>&1)"
  missing_up_exit_code=$?
  set -e
  assert_missing_secret_contract "$missing_variable" "$missing_label" "$missing_project_name" "$missing_up_output" "$missing_up_exit_code"
  "${missing_compose[@]}" down --volumes --remove-orphans
  assert_no_project_residue_for "$missing_project_name"
  printf 'MISSING_SECRET: NEGATIVE_PROBE_EXECUTED\n'
  printf 'MISSING_SECRET: FAIL_CLOSED_OBSERVED\n'
  printf 'MISSING_SECRET: EXPECTED_FAILURE_SIGNATURE=required-secret-configuration\n'
  printf 'MISSING_SECRET: API_NOT_SERVING\n'
  printf 'MISSING_SECRET: NO_LEAK\n'
  printf 'MISSING_SECRET: CLEANUP\n'
  printf 'MISSING_SECRET: RESIDUE_ZERO\n'
  printf 'MISSING_SECRET_PROBE[%s]: PASS (compose-up-exit=%s)\n' "$missing_label" "$missing_up_exit_code"
}

"${compose[@]}" config --quiet
bash tests/fnd05/static-gate.sh

run_missing_secret_probe MBS_DATABASE_BOOTSTRAP_PASSWORD bootstrap
run_missing_secret_probe MBS_DATABASE_MIGRATOR_PASSWORD migrator
run_missing_secret_probe MBS_DATABASE_RUNTIME_PASSWORD runtime

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
