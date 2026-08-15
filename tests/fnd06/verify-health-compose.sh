#!/usr/bin/env bash
set -Eeuo pipefail

# FND-06 verification against the shipped Compose runtime. It sends HTTP directly through bash
# /dev/tcp so the probe method is the same one configured on the API container.

readonly project_name="${FND06_PROJECT_NAME:-minimal-bank-system-fnd06-${RANDOM}${RANDOM}}"
readonly sentinel="${FND06_SECRET_SENTINEL:-FND06_TEST_SENTINEL_NOT_A_CREDENTIAL}"
readonly bootstrap_sentinel="${sentinel}_BOOTSTRAP"
readonly migrator_sentinel="${sentinel}_MIGRATOR"
readonly api_sentinel="${sentinel}_API"
readonly jwt_sentinel="${sentinel}_JWT"
readonly jwt_secret_sentinel="$(printf '%s' "$jwt_sentinel" | base64 | tr -d '\n')"
readonly expected_foundation_migration='20260809113338_InitialFoundation'
readonly expected_identity_migration='20260813181449_AddOperatorIdentity'
readonly expected_audit_migration='20260814234733_AddAuditPersistence'
readonly compose=(docker compose -p "$project_name")

for command_name in docker jq bash; do
  command -v "$command_name" >/dev/null || {
    printf 'FND-06 prerequisite missing: %s\n' "$command_name" >&2
    exit 69
  }
done

export MBS_DATABASE_BOOTSTRAP_PASSWORD="${MBS_DATABASE_BOOTSTRAP_PASSWORD:-$bootstrap_sentinel}"
export MBS_DATABASE_MIGRATOR_PASSWORD="${MBS_DATABASE_MIGRATOR_PASSWORD:-$migrator_sentinel}"
export MBS_DATABASE_API_PASSWORD="${MBS_DATABASE_API_PASSWORD:-$api_sentinel}"
export MBS_JWT_SIGNING_KEY="${MBS_JWT_SIGNING_KEY:-$jwt_secret_sentinel}"

container_id() {
  local service="$1" id
  id="$("${compose[@]}" ps -aq "$service")"
  [[ -n "$id" ]] || {
    printf 'ORACLE_SIGNATURE=container-not-found:%s\n' "$service" >&2
    return 1
  }
  printf '%s\n' "$id"
}

container_state() {
  docker inspect "$(container_id "$1")" | jq --raw-output '.[0].State.Status'
}

container_health() {
  docker inspect "$(container_id api)" | jq --raw-output '.[0].State.Health.Status // "none"'
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
  printf 'ORACLE_SIGNATURE=state-timeout:%s:%s\n' "$service" "$expected" >&2
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
  printf 'ORACLE_SIGNATURE=api-listener-unavailable\n' >&2
  return 1
}

wait_for_container_health() {
  local expected="$1" attempt
  for attempt in $(seq 1 120); do
    [[ "$(container_health)" == "$expected" ]] && return 0
    sleep 1
  done
  printf 'ORACLE_SIGNATURE=container-health-timeout:expected=%s:actual=%s\n' \
    "$expected" "$(container_health)" >&2
  return 1
}

# HTTP/1.0 makes the response close-delimited, avoiding any need for a curl-like tool or chunk
# decoder. The arguments are fixed literals inside this script.
api_response() {
  local method="$1" path="$2"
  "${compose[@]}" exec -T api bash -c \
    "exec 3<>/dev/tcp/127.0.0.1/8080 && printf '$method $path HTTP/1.0\\r\\nHost: localhost\\r\\n\\r\\n' >&3 && cat <&3" |
    tr -d '\r'
}

response_status() {
  head -n 1 <<<"$1"
}

response_body() {
  sed -n '/^$/,$p' <<<"$1" | tail -n +2
}

assert_health() {
  local path="$1" expected_status="$2" expected_body="$3" response body forbidden
  response="$(api_response GET "$path")"
  [[ "$(response_status "$response")" == "HTTP/1.1 $expected_status "* ]] || {
    printf 'ORACLE_SIGNATURE=health-status:%s:%s\n' "$path" "$(response_status "$response")" >&2
    return 1
  }
  body="$(response_body "$response")"
  [[ "$body" == "$expected_body" ]] || {
    printf 'ORACLE_SIGNATURE=health-body:%s:%s\n' "$path" "$body" >&2
    return 1
  }
  [[ "$response" == *'Content-Type: text/plain'* ]] || {
    printf 'ORACLE_SIGNATURE=health-content-type:%s\n' "$path" >&2
    return 1
  }
  for forbidden in "$bootstrap_sentinel" "$migrator_sentinel" "$api_sentinel" \
    "$jwt_sentinel" "$jwt_secret_sentinel" \
    'Password=' 'Host=' 'Username=' 'ConnectionStrings' \
    'Exception' 'StackTrace' 'stack trace' 'database_unreachable' 'migrations_pending' \
    'dependency_failure' 'postgresql-readiness'; do
    [[ "$response" != *"$forbidden"* ]] || {
      printf 'ORACLE_SIGNATURE=health-response-disclosure:%s\n' "$forbidden" >&2
      return 1
    }
  done
}

assert_live() { assert_health /health/live 200 healthy; }
assert_ready() { assert_health /health/ready 200 healthy; }
assert_not_ready() { assert_health /health/ready 503 unhealthy; }

wait_for_ready() {
  local attempt
  for attempt in $(seq 1 90); do
    if assert_ready; then
      return 0
    fi
    sleep 1
  done
  printf 'ORACLE_SIGNATURE=ready-recovery-timeout\n' >&2
  return 1
}

assert_api_running_without_restart() {
  local expected_started_at="$1"
  [[ "$(container_state api)" == running ]] || {
    printf 'ORACLE_SIGNATURE=api-not-running\n' >&2
    return 1
  }
  [[ "$(api_started_at)" == "$expected_started_at" ]] || {
    printf 'ORACLE_SIGNATURE=api-restarted\n' >&2
    return 1
  }
}

read_history() {
  # Role-aware: reads as the Migrator role, which owns the EF migration-history table under the
  # WP2-DB-01 privilege boundary. The historical shared "minimal_bank" role no longer exists.
  "${compose[@]}" exec -T postgres psql -U minimal_bank_migrator -d minimal_bank -At \
    -c 'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId";'
}

read_public_tables() {
  "${compose[@]}" exec -T postgres psql -U minimal_bank_migrator -d minimal_bank -At \
    -c "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name;"
}

assert_no_log_disclosure() {
  local logs forbidden
  logs="$("${compose[@]}" logs --no-color --timestamps api)"
  for forbidden in "$bootstrap_sentinel" "$migrator_sentinel" "$api_sentinel" \
    "$jwt_sentinel" "$jwt_secret_sentinel" \
    'Password=' 'ConnectionStrings__' 'StackTrace' 'stack trace' '   at '; do
    [[ "$logs" != *"$forbidden"* ]] || {
      printf 'ORACLE_SIGNATURE=health-log-disclosure:%s\n' "$forbidden" >&2
      return 1
    }
  done
}

assert_compose_probe() {
  local rendered probe timeout_seconds
  rendered="$("${compose[@]}" config --format json)"
  probe="$(jq --raw-output '.services.api.healthcheck.test | join(" ")' <<<"$rendered")"
  timeout_seconds="$(jq --raw-output '.services.api.healthcheck.timeout' <<<"$rendered")"
  [[ "$probe" == *'/health/ready'* && "$probe" == *'bash'* && "$probe" != *'pg_isready'* ]] || {
    printf 'ORACLE_SIGNATURE=compose-health-semantics-mismatch\n' >&2
    return 1
  }
  [[ "$timeout_seconds" == '15s' ]] || {
    printf 'ORACLE_SIGNATURE=compose-probe-budget-mismatch\n' >&2
    return 1
  }
}

assert_get_only() {
  local response status
  response="$(api_response POST /health/live)"
  status="$(response_status "$response")"
  case "$status" in
    'HTTP/1.0 200 '*|'HTTP/1.1 200 '*)
      printf 'ORACLE_SIGNATURE=non-get-health-succeeded:%s\n' "$status" >&2
      return 1
      ;;
    *) ;;
  esac
}

assert_unknown_health_path_uses_existing_contract() {
  local response
  response="$(api_response GET /health/does-not-exist)"
  [[ "$(response_status "$response")" == 'HTTP/1.1 404 '* &&
     "$response" == *'"code":"endpoint_not_found"'* ]] || {
    printf 'ORACLE_SIGNATURE=fnd02-unknown-health-contract-lost\n' >&2
    return 1
  }
}

assert_no_project_residue() {
  local containers volumes networks
  containers="$(docker ps -aq --filter "label=com.docker.compose.project=$project_name")"
  volumes="$(docker volume ls -q --filter "label=com.docker.compose.project=$project_name")"
  networks="$(docker network ls -q --filter "label=com.docker.compose.project=$project_name")"
  [[ -z "$containers" && -z "$volumes" && -z "$networks" ]] || {
    printf 'ORACLE_SIGNATURE=compose-residue\n' >&2
    return 1
  }
}

cleanup() {
  "${compose[@]}" down --volumes --remove-orphans
  assert_no_project_residue
}

trap cleanup EXIT

"${compose[@]}" config --quiet
assert_compose_probe

# Run A: canonical startup order followed by an actual PostgreSQL service stop and start.
"${compose[@]}" up --build --detach --remove-orphans
wait_for_state migrator exited
wait_for_state api running
wait_for_api_listener
assert_live
assert_ready
assert_get_only
assert_unknown_health_path_uses_existing_contract
wait_for_container_health healthy

api_started_before_outage="$(api_started_at)"
history_before_outage="$(read_history)"
tables_before_outage="$(read_public_tables)"
[[ "$history_before_outage" == *"$expected_foundation_migration"* ]] || {
  printf 'ORACLE_SIGNATURE=missing-canonical-migration\n' >&2
  exit 1
}
[[ "$history_before_outage" == *"$expected_identity_migration"* ]] || {
  printf 'ORACLE_SIGNATURE=missing-operator-identity-migration\n' >&2
  exit 1
}
[[ "$history_before_outage" == *"$expected_audit_migration"* ]] || {
  printf 'ORACLE_SIGNATURE=missing-product-audit-migration\n' >&2
  exit 1
}

"${compose[@]}" stop postgres
assert_api_running_without_restart "$api_started_before_outage"
assert_live
assert_not_ready
wait_for_container_health unhealthy
assert_api_running_without_restart "$api_started_before_outage"
printf 'RUN_A: POSTGRES_STOPPED_LIVE_UP_READY_DOWN\n'

"${compose[@]}" start postgres
wait_for_ready
wait_for_container_health healthy
assert_api_running_without_restart "$api_started_before_outage"
[[ "$(read_history)" == "$history_before_outage" ]] || {
  printf 'ORACLE_SIGNATURE=health-probe-changed-migration-history\n' >&2
  exit 1
}
[[ "$(read_public_tables)" == "$tables_before_outage" ]] || {
  printf 'ORACLE_SIGNATURE=health-probe-changed-schema\n' >&2
  exit 1
}
assert_no_log_disclosure
printf 'RUN_A: POSTGRES_RECOVERED_READY_WITHOUT_API_RESTART\n'

"${compose[@]}" down --volumes --remove-orphans
assert_no_project_residue

# Run B: bypass only the migrator gate to construct a verification-only unmigrated runtime. The
# shipped lifecycle itself remains canonical; the API must not create its own schema.
"${compose[@]}" up --detach --remove-orphans postgres
"${compose[@]}" up --detach --no-deps api
wait_for_state api running
wait_for_api_listener
assert_live
assert_not_ready
wait_for_container_health unhealthy
[[ -z "$(read_public_tables)" ]] || {
  printf 'ORACLE_SIGNATURE=api-created-schema-before-migrator\n' >&2
  exit 1
}

api_started_before_migration="$(api_started_at)"
"${compose[@]}" up --detach migrator
wait_for_state migrator exited
docker inspect "$(container_id migrator)" | jq --exit-status '.[0].State.ExitCode == 0' >/dev/null
wait_for_ready
wait_for_container_health healthy
assert_api_running_without_restart "$api_started_before_migration"
assert_no_log_disclosure
printf 'RUN_B: MIGRATION_INCOMPLETE_REJECTED_THEN_RECOVERED\n'
printf 'FND06_HEALTH_COMPOSE_VERIFICATION: PASS\n'
