#!/usr/bin/env bash
set -Eeuo pipefail

readonly project_name="${FND05_PROJECT_NAME:-minimal-bank-system-fnd05}"
readonly expected_foundation_migration='20260809113338_InitialFoundation'
readonly expected_identity_migration='20260813181449_AddOperatorIdentity'
readonly expected_audit_migration='20260814234733_AddAuditPersistence'
readonly sentinel="${FND05_SECRET_SENTINEL:-FND05_TEST_SENTINEL_NOT_A_CREDENTIAL}"
readonly bootstrap_sentinel="${sentinel}_BOOTSTRAP"
readonly migrator_sentinel="${sentinel}_MIGRATOR"
readonly api_sentinel="${sentinel}_API"
readonly jwt_sentinel="${sentinel}_JWT"
readonly jwt_secret_sentinel="$(printf '%s' "$jwt_sentinel" | base64 | tr -d '\n')"
readonly compose=(docker compose -p "$project_name")
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

# WP2-DB-01: the bootstrap, Migrator and API runtime credentials are independent secrets. Each
# gets its own distinguishable sentinel so a leak can be attributed to the credential path that
# actually leaked it, and so the missing-secret probes below can unset exactly one at a time.
export MBS_DATABASE_BOOTSTRAP_PASSWORD="${MBS_DATABASE_BOOTSTRAP_PASSWORD:-$bootstrap_sentinel}"
export MBS_DATABASE_MIGRATOR_PASSWORD="${MBS_DATABASE_MIGRATOR_PASSWORD:-$migrator_sentinel}"
export MBS_DATABASE_API_PASSWORD="${MBS_DATABASE_API_PASSWORD:-$api_sentinel}"
export MBS_JWT_SIGNING_KEY="${MBS_JWT_SIGNING_KEY:-$jwt_secret_sentinel}"

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
  # Role-aware: reads as the Migrator role, which owns the EF migration-history table under the
  # WP2-DB-01 privilege boundary. The historical shared "minimal_bank" role no longer exists.
  "${compose[@]}" exec -T postgres psql -U minimal_bank_migrator -d minimal_bank -At \
    -c 'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId";'
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
  [[ "$history" == *"$expected_foundation_migration"* ]] || {
    printf 'Expected foundation migration history is missing.\n' >&2
    return 1
  }
  [[ "$history" == *"$expected_identity_migration"* ]] || {
    printf 'Expected operator identity migration history is missing.\n' >&2
    return 1
  }
  [[ "$history" == *"$expected_audit_migration"* ]] || {
    printf 'Expected Product Audit migration history is missing.\n' >&2
    return 1
  }

  rendered="$("${compose[@]}" config --format json)"
  logs="$("${compose[@]}" logs --no-color --timestamps)"
  inspect="$(docker inspect "$postgres_id" "$migrator_id" "$api_id")"
  top="$(docker top "$api_id")"
  for observation_surface in "$rendered" "$logs" "$inspect" "$top"; do
    for probed_sentinel in "$bootstrap_sentinel" "$migrator_sentinel" "$api_sentinel" "$jwt_sentinel" "$jwt_secret_sentinel"; do
      [[ "$observation_surface" != *"$probed_sentinel"* ]] || {
        printf 'Secret sentinel was exposed by an external observation surface.\n' >&2
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
  local missing_project_name="$1" missing_env_var="$2" missing_up_output="$3" missing_up_exit_code="$4"
  local api_id rendered inspect
  local -a container_ids=()
  local missing_compose=(docker compose -p "$missing_project_name")
  (( missing_up_exit_code != 0 )) || {
    printf 'Missing-secret probe incorrectly returned success.\n' >&2
    return 1
  }
  [[ "$missing_up_output" == *"$missing_env_var"* && "$missing_up_output" == *'required by secret'* ]] || {
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
  rendered="$(env -u "$missing_env_var" "${missing_compose[@]}" config --format json)"
  for observation_surface in "$missing_up_output" "$rendered" "$inspect"; do
    for probed_sentinel in "$bootstrap_sentinel" "$migrator_sentinel" "$api_sentinel" "$jwt_sentinel" "$jwt_secret_sentinel"; do
      [[ "$observation_surface" != *"$probed_sentinel"* ]] || {
        printf 'Missing-secret probe exposed the sentinel.\n' >&2
        return 1
      }
    done
  done
}

assert_missing_jwt_secret_contract() {
  local missing_project_name="$1" missing_up_output="$2"
  local api_id api_state rendered inspect serving
  local -a container_ids=()
  local missing_compose=(docker compose -p "$missing_project_name")

  # FS-03: the required fail-closed oracle is that the API is not serving. PostgreSQL or
  # Migrator may start without a JWT secret. Do not require that no service ever started.
  api_id="$("${missing_compose[@]}" ps -aq api)"
  serving=0
  if [[ -n "$api_id" ]]; then
    api_state="$(docker inspect "$api_id" | jq --raw-output '.[0].State.Status')"
    if [[ "$api_state" == running ]] && "${missing_compose[@]}" exec -T api bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080'; then
      serving=1
    fi
  fi
  (( serving == 0 )) || {
    printf 'Missing JWT secret allowed the API to serve.\n' >&2
    return 1
  }

  mapfile -t container_ids < <(docker ps -aq --filter "label=com.docker.compose.project=$missing_project_name")
  if ((${#container_ids[@]})); then
    inspect="$(docker inspect "${container_ids[@]}")"
  else
    inspect='[]'
  fi
  rendered="$(env -u MBS_JWT_SIGNING_KEY "${missing_compose[@]}" config --format json 2>/dev/null || printf '{}')"
  for observation_surface in "$missing_up_output" "$rendered" "$inspect"; do
    for probed_sentinel in "$bootstrap_sentinel" "$migrator_sentinel" "$api_sentinel" "$jwt_sentinel" "$jwt_secret_sentinel"; do
      [[ "$observation_surface" != *"$probed_sentinel"* ]] || {
        printf 'Missing JWT secret probe exposed the sentinel.\n' >&2
        return 1
      }
    done
  done
}

# WP2-DB-01: Migrator and API runtime credential injection are independently fail-closed. Each is
# probed on its own by unsetting exactly one of the two secret-source environment variables while
# leaving the other populated, so a missing Migrator secret cannot be masked by a present API
# secret and vice versa.
run_missing_secret_probe() {
  local probe_label="$1" missing_env_var="$2"
  local probe_slug
  probe_slug="$(tr '[:upper:]_' '[:lower:]-' <<<"$probe_label")"
  local missing_project_name="${project_name}-missing-secret-${probe_slug}-${RANDOM}${RANDOM}"
  local -a missing_compose=(docker compose -p "$missing_project_name")
  local missing_up_output missing_up_exit_code

  cleanup_project_names+=("$missing_project_name")

  set +e
  missing_up_output="$(env -u "$missing_env_var" "${missing_compose[@]}" up --build --detach --remove-orphans 2>&1)"
  missing_up_exit_code=$?
  set -e
  assert_missing_secret_contract "$missing_project_name" "$missing_env_var" "$missing_up_output" "$missing_up_exit_code"
  "${missing_compose[@]}" down --volumes --remove-orphans
  assert_no_project_residue_for "$missing_project_name"
  printf '%s: NEGATIVE_PROBE_EXECUTED\n' "$probe_label"
  printf '%s: FAIL_CLOSED_OBSERVED\n' "$probe_label"
  printf '%s: EXPECTED_FAILURE_SIGNATURE=required-secret-configuration\n' "$probe_label"
  printf '%s: API_NOT_SERVING\n' "$probe_label"
  printf '%s: NO_LEAK\n' "$probe_label"
  printf '%s: CLEANUP\n' "$probe_label"
  printf '%s: RESIDUE_ZERO\n' "$probe_label"
  printf '%s_PROBE: PASS (compose-up-exit=%s)\n' "$probe_label" "$missing_up_exit_code"
}

run_missing_jwt_secret_probe() {
  local missing_project_name="${project_name}-missing-secret-jwt-${RANDOM}${RANDOM}"
  local -a missing_compose=(docker compose -p "$missing_project_name")
  local missing_up_output missing_up_exit_code

  cleanup_project_names+=("$missing_project_name")

  set +e
  missing_up_output="$(env -u MBS_JWT_SIGNING_KEY "${missing_compose[@]}" up --build --detach --remove-orphans 2>&1)"
  missing_up_exit_code=$?
  set -e
  assert_missing_jwt_secret_contract "$missing_project_name" "$missing_up_output"
  "${missing_compose[@]}" down --volumes --remove-orphans
  assert_no_project_residue_for "$missing_project_name"
  printf 'MISSING_JWT_SECRET: NEGATIVE_PROBE_EXECUTED\n'
  printf 'MISSING_JWT_SECRET: FAIL_CLOSED_OBSERVED\n'
  printf 'MISSING_JWT_SECRET: EXPECTED_FAILURE_SIGNATURE=api-not-serving\n'
  printf 'MISSING_JWT_SECRET: API_NOT_SERVING\n'
  printf 'MISSING_JWT_SECRET: NO_LEAK\n'
  printf 'MISSING_JWT_SECRET: CLEANUP\n'
  printf 'MISSING_JWT_SECRET: RESIDUE_ZERO\n'
  printf 'MISSING_JWT_SECRET_PROBE: PASS (compose-up-exit=%s)\n' "$missing_up_exit_code"
}

"${compose[@]}" config --quiet
bash tests/fnd05/static-gate.sh

run_missing_secret_probe MISSING_MIGRATOR_SECRET MBS_DATABASE_MIGRATOR_PASSWORD
run_missing_secret_probe MISSING_API_SECRET MBS_DATABASE_API_PASSWORD
run_missing_jwt_secret_probe

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
