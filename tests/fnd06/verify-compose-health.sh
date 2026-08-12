#!/usr/bin/env bash
set -Eeuo pipefail

readonly project_name="${FND06_PROJECT_NAME:-minimal-bank-system-fnd06-health}"
readonly compose=(docker compose -p "$project_name")
readonly sentinel="${FND06_SECRET_SENTINEL:-FND06_TEST_SENTINEL_NOT_A_CREDENTIAL}"

require_command() {
  command -v "$1" >/dev/null
}

for command_name in docker jq bash; do
  require_command "$command_name" || {
    printf 'FND-06 Compose health verification prerequisite missing: %s\n' "$command_name" >&2
    exit 69
  }
done

export MBS_DATABASE_PASSWORD="${MBS_DATABASE_PASSWORD:-$sentinel}"

cleanup() {
  "${compose[@]}" down --volumes --remove-orphans >/dev/null 2>&1 || true
}
trap cleanup EXIT

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
  local service_name="$1"
  local expected_state="$2"
  local attempt
  for attempt in $(seq 1 120); do
    if [[ "$(container_state "$service_name")" == "$expected_state" ]]; then
      return 0
    fi
    sleep 1
  done
  printf '%s did not reach %s.\n' "$service_name" "$expected_state" >&2
  return 1
}

wait_for_health() {
  local service_name="$1"
  local expected_health="$2"
  local attempt
  for attempt in $(seq 1 120); do
    if [[ "$(container_health "$service_name")" == "$expected_health" ]]; then
      return 0
    fi
    [[ "$(container_state "$service_name")" == 'running' ]] || return 1
    sleep 1
  done
  printf '%s did not become %s.\n' "$service_name" "$expected_health" >&2
  return 1
}

read_http_status() {
  local path="$1"
  "${compose[@]}" exec -T api bash -ceu '
    path="$1"
    exec 3<>/dev/tcp/127.0.0.1/8080
    printf "GET %s HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n" "$path" >&3
    IFS= read -r -u 3 status_line || true
    status_line="${status_line%$'\''\r'\''}"
    remainder="${status_line#* }"
    printf "%s\n" "${remainder%% *}"
  ' bash "$path"
}

read_http_exchange() {
  local path="$1"
  "${compose[@]}" exec -T api bash -ceu '
    path="$1"
    exec 3<>/dev/tcp/127.0.0.1/8080
    printf "GET %s HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n" "$path" >&3
    cat <&3
  ' bash "$path"
}

assert_probe_binary_in_image() {
  "${compose[@]}" exec -T api bash -ceu '
    test -x /usr/local/bin/http-get-status
    if command -v curl >/dev/null; then exit 40; fi
    if command -v wget >/dev/null; then exit 41; fi
    if command -v nc >/dev/null; then exit 42; fi
    /usr/local/bin/http-get-status 127.0.0.1 8080 /health/live
  '
}

assert_response_has_no_secrets() {
  local path="$1"
  local body
  body="$(read_http_exchange "$path")"

  [[ "$body" != *"$sentinel"* ]] || {
    printf 'Health response leaked the secret sentinel.\n' >&2
    return 1
  }
  [[ "$body" != *'internal_error'* ]] || {
    printf 'Health response used the business error envelope.\n' >&2
    return 1
  }
  [[ "$body" != *'Exception'* && "$body" != *'Stack'* && "$body" != *'Npgsql'* ]] || {
    printf 'Health response leaked exception detail.\n' >&2
    return 1
  }
  [[ "$body" != *'Password'* && "$body" != *'ConnectionStrings'* ]] || {
    printf 'Health response leaked credential material.\n' >&2
    return 1
  }
}

printf 'FND06_COMPOSE_HEALTH: starting\n'
"${compose[@]}" up --build --detach --remove-orphans
wait_for_state migrator exited
wait_for_state api running
wait_for_health api healthy

assert_probe_binary_in_image

live_status="$(read_http_status /health/live)"
ready_status="$(read_http_status /health/ready)"
[[ "$live_status" == '200' ]] || {
  printf 'Expected live 200, got %s\n' "$live_status" >&2
  exit 1
}
[[ "$ready_status" == '200' ]] || {
  printf 'Expected ready 200, got %s\n' "$ready_status" >&2
  exit 1
}
assert_response_has_no_secrets /health/live
assert_response_has_no_secrets /health/ready
printf 'FND06_COMPOSE_HEALTH: initial live/ready success\n'

"${compose[@]}" stop postgres
wait_for_state postgres exited
[[ "$(container_state api)" == 'running' ]] || {
  printf 'API process stopped when PostgreSQL stopped.\n' >&2
  exit 1
}

live_status="$(read_http_status /health/live)"
ready_status="$(read_http_status /health/ready)"
[[ "$live_status" == '200' ]] || {
  printf 'Expected live 200 while PostgreSQL stopped, got %s\n' "$live_status" >&2
  exit 1
}
[[ "$ready_status" != '200' ]] || {
  printf 'Expected ready failure while PostgreSQL stopped, got status=%s\n' "$ready_status" >&2
  exit 1
}
set +e
"${compose[@]}" exec -T api /usr/local/bin/http-get-status 127.0.0.1 8080 /health/ready
ready_probe_exit=$?
set -e
[[ "$ready_probe_exit" -ne 0 ]] || {
  printf 'Shipped ready probe incorrectly succeeded while PostgreSQL was stopped.\n' >&2
  exit 1
}
wait_for_health api unhealthy
assert_response_has_no_secrets /health/ready
printf 'FND06_COMPOSE_HEALTH: postgres stop live=success ready=failure\n'

"${compose[@]}" start postgres
wait_for_state postgres running
wait_for_health postgres healthy
wait_for_health api healthy

live_status="$(read_http_status /health/live)"
ready_status="$(read_http_status /health/ready)"
[[ "$live_status" == '200' ]] || {
  printf 'Expected live 200 after PostgreSQL recovery, got %s\n' "$live_status" >&2
  exit 1
}
[[ "$ready_status" == '200' ]] || {
  printf 'Expected ready 200 after PostgreSQL recovery without API restart, got %s\n' "$ready_status" >&2
  exit 1
}
assert_response_has_no_secrets /health/ready
printf 'FND06_COMPOSE_HEALTH: postgres recovery ready=success\n'

printf 'FND06_COMPOSE_HEALTH_VERIFICATION: PASS\n'
