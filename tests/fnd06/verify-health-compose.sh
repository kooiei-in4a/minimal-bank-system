#!/usr/bin/env bash
set -Eeuo pipefail

# FND-06 live/ready health contract verification on the shipped Docker Compose runtime.
#
# The probe deliberately uses only bash builtins. The shipped API runtime image contains no curl,
# wget, nc or python3, so an external probe tool would have to be installed into the image.

readonly project_name="${FND06_PROJECT_NAME:-minimal-bank-system-fnd06}"
readonly sentinel="${FND06_SECRET_SENTINEL:-FND06_TEST_SENTINEL_NOT_A_CREDENTIAL}"
readonly expected_migration='20260809113338_InitialFoundation'
readonly compose=(docker compose -p "$project_name")
readonly api_port=8080
readonly transition_budget=120

for command_name in docker jq bash; do
  command -v "$command_name" >/dev/null || {
    printf 'FND-06 verification prerequisite missing: %s\n' "$command_name" >&2
    exit 69
  }
done

export MBS_DATABASE_PASSWORD="${MBS_DATABASE_PASSWORD:-$sentinel}"

container_id() {
  local id
  id="$("${compose[@]}" ps -aq "$1")"
  [[ -n "$id" ]] || {
    printf 'Expected %s container was not found.\n' "$1" >&2
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

api_started_at() {
  docker inspect "$(container_id api)" | jq --raw-output '.[0].State.StartedAt'
}

wait_for_state() {
  local service="$1" expected="$2" attempt
  for attempt in $(seq 1 90); do
    [[ "$(container_state "$service")" == "$expected" ]] && return 0
    sleep 1
  done
  printf 'ORACLE_SIGNATURE=state-timeout:%s!=%s\n' "$service" "$expected" >&2
  return 1
}

wait_for_container_health() {
  local expected="$1" attempt observed
  for attempt in $(seq 1 "$transition_budget"); do
    observed="$(container_health api)"
    [[ "$observed" == "$expected" ]] && {
      printf 'CONTAINER_HEALTH: %s after %ss\n' "$expected" "$attempt"
      return 0
    }
    sleep 1
  done
  printf 'ORACLE_SIGNATURE=container-health-timeout:expected=%s,observed=%s\n' \
    "$expected" "$(container_health api)" >&2
  return 1
}

wait_for_api_listener() {
  local attempt
  for attempt in $(seq 1 90); do
    if "${compose[@]}" exec -T api bash -c "exec 3<>/dev/tcp/127.0.0.1/$api_port"; then
      return 0
    fi
    [[ "$(container_state api)" == 'running' ]] || return 1
    sleep 1
  done
  printf 'ORACLE_SIGNATURE=api-listener-unreachable\n' >&2
  return 1
}

# HTTP/1.0 keeps the response close-delimited, so the body needs no chunk decoding.
api_response() {
  "${compose[@]}" exec -T api bash -c \
    "exec 3<>/dev/tcp/127.0.0.1/$api_port && printf 'GET $1 HTTP/1.0\r\nHost: localhost\r\n\r\n' >&3 && cat <&3" |
    tr -d '\r'
}

response_status_line() {
  printf '%s\n' "$1" | head -n 1
}

response_body() {
  printf '%s\n' "$1" | sed -n '/^$/,$p' | tail -n +2
}

# AC-06 applies to the health response: no connection detail, no exception detail, no stack trace.
assert_no_response_disclosure() {
  local surface="$1" forbidden
  for forbidden in "$sentinel" 'Password=' 'Host=' 'Username=' 'ConnectionStrings' 'Npgsql' \
    'Exception' 'StackTrace' 'stack trace' 'at Microsoft.' 'at System.' \
    'database_unreachable' 'migrations_pending' 'dependency_failure'; do
    case "$surface" in
      *"$forbidden"*)
        printf 'ORACLE_SIGNATURE=health-response-disclosure:%s\n' "$forbidden" >&2
        return 1
        ;;
    esac
  done
}

# ADR-0008 keeps dependency failures in the technical log. The allow-list there matches FND-02:
# a fixed reason and an exception type, never a credential, connection string or stack trace.
assert_no_log_disclosure() {
  local surface="$1" forbidden
  for forbidden in "$sentinel" 'Password=' 'ConnectionStrings__' 'StackTrace' 'stack trace' \
    '   at System.' '   at Microsoft.' '   at Npgsql.'; do
    case "$surface" in
      *"$forbidden"*)
        printf 'ORACLE_SIGNATURE=health-log-disclosure:%s\n' "$forbidden" >&2
        return 1
        ;;
    esac
  done
}

assert_not_business_envelope() {
  local surface="$1" forbidden
  for forbidden in '"code"' '"message"' 'internal_error' 'validation_failed' 'endpoint_not_found'; do
    case "$surface" in
      *"$forbidden"*)
        printf 'ORACLE_SIGNATURE=health-mapped-to-business-envelope:%s\n' "$forbidden" >&2
        return 1
        ;;
    esac
  done
}

assert_health() {
  local path="$1" expected_status="$2" expected_body="$3"
  local response status body

  response="$(api_response "$path")"
  status="$(response_status_line "$response")"
  body="$(response_body "$response")"

  [[ "$status" == "HTTP/1.1 $expected_status "* ]] || {
    printf 'ORACLE_SIGNATURE=health-status-mismatch:%s expected=%s observed=%s\n' \
      "$path" "$expected_status" "$status" >&2
    return 1
  }
  [[ "$body" == "$expected_body" ]] || {
    printf 'ORACLE_SIGNATURE=health-body-mismatch:%s expected=%s observed=%s\n' \
      "$path" "$expected_body" "$body" >&2
    return 1
  }
  [[ "$response" == *'Content-Type: text/plain'* ]] || {
    printf 'ORACLE_SIGNATURE=health-content-type-mismatch:%s\n' "$path" >&2
    return 1
  }
  assert_no_response_disclosure "$response"
  assert_not_business_envelope "$response"
}

assert_live() { assert_health /health/live 200 healthy; }
assert_ready() { assert_health /health/ready 200 healthy; }
assert_not_ready() { assert_health /health/ready 503 unhealthy; }

assert_api_process_alive() {
  docker inspect "$(container_id api)" | jq --exit-status '
    .[0].State.Status == "running" and
    .[0].State.Restarting == false and
    .[0].RestartCount == 0
  ' >/dev/null || {
    printf 'ORACLE_SIGNATURE=api-process-not-alive\n' >&2
    return 1
  }
}

assert_api_not_restarted() {
  [[ "$(api_started_at)" == "$1" ]] || {
    printf 'ORACLE_SIGNATURE=api-restart-required-for-recovery\n' >&2
    return 1
  }
}

# Non-health paths must keep the FND-02 business error envelope.
assert_business_envelope_still_owns_other_paths() {
  local response
  response="$(api_response /health/does-not-exist)"
  [[ "$(response_status_line "$response")" == 'HTTP/1.1 404 '* ]] || {
    printf 'ORACLE_SIGNATURE=business-not-found-status-lost\n' >&2
    return 1
  }
  [[ "$response" == *'"code":"endpoint_not_found"'* ]] || {
    printf 'ORACLE_SIGNATURE=business-error-envelope-lost\n' >&2
    return 1
  }
}

psql_query() {
  "${compose[@]}" exec -T postgres psql -U minimal_bank -d minimal_bank -At -c "$1"
}

read_public_tables() {
  psql_query "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name;"
}

read_history() {
  psql_query 'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId";'
}

assert_no_project_residue() {
  local containers volumes networks
  containers="$(docker ps -aq --filter "label=com.docker.compose.project=$project_name")"
  volumes="$(docker volume ls -q --filter "label=com.docker.compose.project=$project_name")"
  networks="$(docker network ls -q --filter "label=com.docker.compose.project=$project_name")"
  [[ -z "$containers" && -z "$volumes" && -z "$networks" ]] || {
    printf 'ORACLE_SIGNATURE=project-residue-remains\n' >&2
    return 1
  }
}

cleanup() {
  "${compose[@]}" down --volumes --remove-orphans
  assert_no_project_residue
}

trap cleanup EXIT

assert_compose_health_semantics() {
  local rendered
  rendered="$("${compose[@]}" config --format json)"

  jq --exit-status '
    (.services.api.healthcheck.test | join(" ")) as $probe |
    ($probe | contains("/health/ready")) and
    ($probe | contains("pg_isready") | not) and
    (.services.api.healthcheck.interval != null) and
    (.services.api.healthcheck.retries != null) and
    (.services.postgres.healthcheck.test | join(" ") | contains("pg_isready"))
  ' <<<"$rendered" >/dev/null || {
    printf 'ORACLE_SIGNATURE=compose-health-semantics-mismatch\n' >&2
    return 1
  }
  printf 'COMPOSE_HEALTH_SEMANTICS: PASS\n'
}

"${compose[@]}" config --quiet
assert_compose_health_semantics

# --------------------------------------------------------------------------------------------
# Run A: canonical shipped ordering, then a real PostgreSQL stop and start.
# --------------------------------------------------------------------------------------------
"${compose[@]}" up --build --detach --remove-orphans
wait_for_state migrator exited
wait_for_state api running
wait_for_api_listener

assert_live
assert_ready
assert_business_envelope_still_owns_other_paths
wait_for_container_health healthy
[[ "$(read_history)" == *"$expected_migration"* ]] || {
  printf 'ORACLE_SIGNATURE=expected-migration-absent\n' >&2
  exit 1
}
printf 'RUN_A: STEADY_STATE_LIVE_AND_READY\n'

api_started_at_before_outage="$(api_started_at)"
history_before_outage="$(read_history)"
tables_before_outage="$(read_public_tables)"

"${compose[@]}" stop postgres
assert_api_process_alive
assert_live
assert_not_ready
wait_for_container_health unhealthy
assert_api_process_alive
printf 'RUN_A: POSTGRES_STOPPED_LIVE_UP_READY_DOWN\n'

"${compose[@]}" start postgres
wait_for_container_health healthy
assert_ready
assert_live
assert_api_not_restarted "$api_started_at_before_outage"
[[ "$(read_history)" == "$history_before_outage" ]] || {
  printf 'ORACLE_SIGNATURE=health-probing-changed-migration-history\n' >&2
  exit 1
}
[[ "$(read_public_tables)" == "$tables_before_outage" ]] || {
  printf 'ORACLE_SIGNATURE=health-probing-changed-schema\n' >&2
  exit 1
}
printf 'RUN_A: POSTGRES_RECOVERED_READY_WITHOUT_API_RESTART\n'

run_a_logs="$("${compose[@]}" logs --no-color --timestamps api)"
assert_no_log_disclosure "$run_a_logs"
[[ "$run_a_logs" == *'Readiness rejected with database_unreachable.'* ]] || {
  printf 'ORACLE_SIGNATURE=readiness-failure-not-technically-logged\n' >&2
  exit 1
}
printf 'RUN_A: NO_SECRET_OR_STACK_DISCLOSURE_AND_FAILURE_TECHNICALLY_LOGGED\n'

"${compose[@]}" down --volumes --remove-orphans
assert_no_project_residue
printf 'RUN_A: PASS\n'

# --------------------------------------------------------------------------------------------
# Run B: migration-incomplete runtime. The Migrator gate is bypassed only to reach the state;
# the shipped compose ordering itself is unchanged.
# --------------------------------------------------------------------------------------------
"${compose[@]}" up --detach --remove-orphans postgres
"${compose[@]}" up --detach --no-deps api
wait_for_state api running
wait_for_api_listener

assert_live
assert_not_ready
wait_for_container_health unhealthy
[[ -z "$(read_public_tables)" ]] || {
  printf 'ORACLE_SIGNATURE=api-created-schema-without-migrator\n' >&2
  exit 1
}
printf 'RUN_B: MIGRATION_INCOMPLETE_LIVE_UP_READY_DOWN\n'

api_started_at_before_migration="$(api_started_at)"

"${compose[@]}" up --detach migrator
wait_for_state migrator exited
docker inspect "$(container_id migrator)" | jq --exit-status '.[0].State.ExitCode == 0' >/dev/null

wait_for_container_health healthy
assert_ready
assert_live
assert_api_not_restarted "$api_started_at_before_migration"
[[ "$(read_history)" == *"$expected_migration"* ]] || {
  printf 'ORACLE_SIGNATURE=expected-migration-absent\n' >&2
  exit 1
}
printf 'RUN_B: READY_ONLY_AFTER_MIGRATION\n'

run_b_logs="$("${compose[@]}" logs --no-color --timestamps api)"
assert_no_log_disclosure "$run_b_logs"
[[ "$run_b_logs" == *'Readiness rejected with migrations_pending.'* ]] || {
  printf 'ORACLE_SIGNATURE=pending-migration-rejection-not-technically-logged\n' >&2
  exit 1
}
printf 'RUN_B: PASS\n'

printf 'FND06_HEALTH_COMPOSE_VERIFICATION: PASS\n'
