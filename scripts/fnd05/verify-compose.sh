#!/usr/bin/env bash
set -Eeuo pipefail

readonly PROJECT="${FND05_PROJECT:-minimal-bank-system-fnd05}"
readonly FAILURE_PROJECT="${FND05_FAILURE_PROJECT:-${PROJECT}-failure}"
readonly EXPECTED_MIGRATION_SUFFIX='_InitialFoundation'
readonly FAILURE_OVERRIDE='scripts/fnd05/fixtures/migrator-failure.compose.yaml'

die() {
    printf 'FND05_VERIFY_FAILED: %s\n' "$*" >&2
    exit 1
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || die "required command is missing: $1"
}

compose() {
    docker compose -p "$PROJECT" "$@"
}

failure_compose() {
    docker compose -p "$FAILURE_PROJECT" -f compose.yaml -f "$FAILURE_OVERRIDE" "$@"
}

service_id() {
    local service="$1"
    compose ps -aq "$service" | head -n 1
}

failure_service_id() {
    local service="$1"
    failure_compose ps -aq "$service" | head -n 1
}

container_state_json() {
    docker inspect "$1" | jq -e '.[0] | {id: .Id, status: .State.Status, exit_code: .State.ExitCode, started_at: .State.StartedAt, finished_at: .State.FinishedAt, project: .Config.Labels["com.docker.compose.project"], service: .Config.Labels["com.docker.compose.service"]}'
}

wait_for_terminal() {
    local container_id="$1"
    local state

    for _ in {1..90}; do
        state="$(docker inspect -f '{{.State.Status}}' "$container_id")"
        case "$state" in
            exited|dead)
                return 0
                ;;
            created|running|restarting)
                sleep 1
                ;;
            *)
                die "unexpected container state '$state' for '$container_id'"
                ;;
        esac
    done

    die "container '$container_id' did not reach a terminal state"
}

wait_for_api_running() {
    local container_id=""
    local state

    for _ in {1..90}; do
        container_id="$(service_id api)"
        if [[ -n "$container_id" ]]; then
            state="$(docker inspect -f '{{.State.Status}}' "$container_id")"
            case "$state" in
                running)
                    printf '%s' "$container_id"
                    return 0
                    ;;
                exited|dead)
                    die 'API started and then exited; success requires a running API'
                    ;;
            esac
        fi
        sleep 1
    done

    die 'API did not reach running state'
}

assert_no_project_resources() {
    local project="$1"
    local compose_file_args=()
    if [[ "$project" == "$FAILURE_PROJECT" ]]; then
        compose_file_args=(-f compose.yaml -f "$FAILURE_OVERRIDE")
    fi

    local containers
    containers="$(docker compose -p "$project" "${compose_file_args[@]}" ps -aq 2>/dev/null || true)"
    [[ -z "$containers" ]] || die "containers remain for project '$project'"

    local volumes
    volumes="$(docker volume ls --filter "label=com.docker.compose.project=$project" --format '{{.Name}}')"
    [[ -z "$volumes" ]] || die "volumes remain for project '$project': $volumes"

    local networks
    networks="$(docker network ls --filter "label=com.docker.compose.project=$project" --format '{{.Name}}')"
    [[ -z "$networks" ]] || die "networks remain for project '$project': $networks"
}

assert_secret_not_observed() {
    local project_logs="$1"
    shift
    local container_id
    local args

    if grep -F -- "$SECRET_SENTINEL" <<<"$project_logs" >/dev/null; then
        die 'secret sentinel was found in Compose logs'
    fi

    for container_id in "$@"; do
        args="$(docker inspect "$container_id" | jq -r '.[0] | ([.Path] + .Args)[]')"
        if grep -F -- "$SECRET_SENTINEL" <<<"$args" >/dev/null; then
            die 'secret sentinel was found in a container command or argument'
        fi
    done
}

assert_volume_contract() {
    local volume_name
    volume_name="$(docker volume ls \
        --filter "label=com.docker.compose.project=$PROJECT" \
        --filter 'label=com.docker.compose.volume=postgres_data' \
        --format '{{.Name}}')"
    [[ -n "$volume_name" ]] || die 'the PostgreSQL named volume was not found'
    [[ "$(wc -l <<<"$volume_name")" -eq 1 ]] || die 'expected one PostgreSQL named volume'

    docker volume inspect "$volume_name" | jq -e --arg project "$PROJECT" '
        .[0].Labels["com.docker.compose.project"] == $project and
        .[0].Labels["com.docker.compose.volume"] == "postgres_data"
    ' >/dev/null || die 'PostgreSQL volume labels do not identify the Compose project and volume'
}

assert_rendered_contract() {
    local rendered="$1"

    if grep -F -- "$SECRET_SENTINEL" <<<"$rendered" >/dev/null; then
        die 'secret sentinel was found in rendered Compose configuration'
    fi

    jq -e '
        .services.postgres.image == "postgres:18.4@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636" and
        .services.postgres.platform == "linux/amd64" and
        .services.postgres.environment.POSTGRES_PASSWORD_FILE == "/run/secrets/database_password" and
        (.services.postgres.volumes | any(.type == "volume" and .source == "postgres_data" and .target == "/var/lib/postgresql")) and
        .services.postgres.healthcheck.test[0] == "CMD-SHELL" and
        .services.migrator.depends_on.postgres.condition == "service_healthy" and
        .services.api.depends_on.postgres.condition == "service_healthy" and
        .services.api.depends_on.migrator.condition == "service_completed_successfully" and
        .secrets.database_password.environment == "MBS_DATABASE_PASSWORD"
    ' <<<"$rendered" >/dev/null || die 'resolved Compose configuration violates the locked contract'
}

assert_migration_history() {
    local history
    history="$(compose exec -T postgres psql -U minimal_bank_system -d minimal_bank_system -Atc \
        'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId";')"
    grep -E -- "$EXPECTED_MIGRATION_SUFFIX$" <<<"$history" >/dev/null ||
        die 'expected InitialFoundation migration is absent from PostgreSQL history'
    printf '%s\n' "$history"
}

assert_success_state() {
    local migrator_id="$1"
    local api_id="$2"
    local migrator_exit
    local migrator_finished
    local api_started
    local state

    state="$(docker inspect "$migrator_id" | jq -e '.[0].State')"
    migrator_exit="$(jq -r '.ExitCode' <<<"$state")"
    [[ "$migrator_exit" == 0 ]] || die "Migrator exit code was $migrator_exit on the success path"
    [[ "$(jq -r '.Status' <<<"$state")" == exited ]] || die 'Migrator did not complete as a one-shot process'

    migrator_finished="$(jq -r '.FinishedAt' <<<"$state")"
    api_started="$(docker inspect "$api_id" | jq -r '.[0].State.StartedAt')"
    [[ "$(docker inspect "$api_id" | jq -r '.[0].State.Status')" == running ]] || die 'API is not running'

    if (( $(date --date="$api_started" +%s%N) < $(date --date="$migrator_finished" +%s%N) )); then
        die 'API started before Migrator finished'
    fi

    printf 'MIGRATOR_STATE: '
    container_state_json "$migrator_id"
    printf 'API_STATE: '
    container_state_json "$api_id"
}

require_command docker
require_command jq
require_command date
[[ -n "${MBS_DATABASE_PASSWORD:-}" ]] || die 'MBS_DATABASE_PASSWORD must be supplied by the host environment'
readonly SECRET_SENTINEL="$MBS_DATABASE_PASSWORD"

printf 'DOCKER_ENGINE: '
docker version --format '{{.Server.Version}}'
printf 'DOCKER_COMPOSE: '
docker compose version --short

compose config --quiet
rendered_config="$(compose config --format json)"
assert_rendered_contract "$rendered_config"
printf 'CONFIG_VALIDATION: PASS\n'

compose down --volumes --remove-orphans >/dev/null 2>&1 || true
assert_no_project_resources "$PROJECT"
failure_compose down --volumes --remove-orphans >/dev/null 2>&1 || true
assert_no_project_resources "$FAILURE_PROJECT"

compose up --build --detach --remove-orphans
migrator_id="$(service_id migrator)"
[[ -n "$migrator_id" ]] || die 'Migrator container was not created'
wait_for_terminal "$migrator_id"
api_id="$(wait_for_api_running)"
assert_success_state "$migrator_id" "$api_id"
history_after_clean="$(assert_migration_history)"
assert_volume_contract
success_logs="$(compose logs --no-color --timestamps 2>&1)"
assert_secret_not_observed "$success_logs" "$migrator_id" "$api_id"
printf 'CLEAN_START: PASS\nMIGRATOR_EXIT: 0\nMIGRATION_HISTORY: %s\nAPI_START_ORDERING: PASS\nSECRET_SENTINEL: ABSENT\n' "$history_after_clean"

compose down --remove-orphans
volume_after_stop="$(docker volume ls --filter "label=com.docker.compose.project=$PROJECT" --filter 'label=com.docker.compose.volume=postgres_data' --format '{{.Name}}')"
[[ -n "$volume_after_stop" ]] || die 'normal stop removed the PostgreSQL data volume'
compose up --build --detach --remove-orphans
migrator_id="$(service_id migrator)"
wait_for_terminal "$migrator_id"
api_id="$(wait_for_api_running)"
assert_success_state "$migrator_id" "$api_id"
history_after_restart="$(assert_migration_history)"
[[ "$history_after_restart" == "$history_after_clean" ]] || die 'restart changed migration history unexpectedly'
printf 'RESTART: PASS\nMIGRATION_HISTORY_AFTER_RESTART: %s\n' "$history_after_restart"

compose down --volumes --remove-orphans
assert_no_project_resources "$PROJECT"
printf 'CLEAN_RESET: PASS\n'

set +e
failure_compose up --build --detach --remove-orphans
failure_up_exit=$?
set -e
failure_migrator_id="$(failure_service_id migrator)"
[[ -n "$failure_migrator_id" ]] || die 'failure probe did not create the Migrator container'
wait_for_terminal "$failure_migrator_id"
failure_exit="$(docker inspect "$failure_migrator_id" | jq -r '.[0].State.ExitCode')"
[[ "$failure_exit" != 0 ]] || die 'failure probe unexpectedly returned Migrator exit 0'
failure_api_id="$(failure_service_id api)"
if [[ -n "$failure_api_id" ]]; then
    failure_started_at="$(docker inspect "$failure_api_id" | jq -r '.[0].State.StartedAt')"
    [[ "$failure_started_at" == 0001-* ]] || die 'failure probe API was started; started-then-exited is not never-started'
fi
failure_logs="$(failure_compose logs --no-color --timestamps 2>&1)"
grep -F -- 'Migration failed' <<<"$failure_logs" >/dev/null || die 'failure probe did not reach the Migrator failure path'
if [[ -n "$failure_api_id" ]]; then
    assert_secret_not_observed "$failure_logs" "$failure_migrator_id" "$failure_api_id"
else
    assert_secret_not_observed "$failure_logs" "$failure_migrator_id"
fi
printf 'MIGRATION_FAILURE: PASS\nFAILURE_COMPOSE_UP_EXIT: %s\nMIGRATOR_EXIT: %s\nAPI_NEVER_STARTED: PASS\nFAILURE_PATH_MARKER: PASS\n' "$failure_up_exit" "$failure_exit"

failure_compose down --volumes --remove-orphans
assert_no_project_resources "$FAILURE_PROJECT"
printf 'FAILURE_CLEAN_RESET: PASS\n'
