#!/bin/bash
# Shared helpers for the FND-05 Compose verification scripts (Issue #43).
# Bash >= 5.2 / jq >= 1.7 per D-07. Sourced by the other scripts in this directory.
set -euo pipefail

FND05_PROJECT_NAME="minimal-bank-system-fnd05"
FND05_REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FND05_COMPOSE_FILE="${FND05_REPO_ROOT}/compose.yaml"
FND05_FAILURE_OVERRIDE_FILE="${FND05_REPO_ROOT}/docker/compose.override.migration-failure.yaml"

# Locked D-02 image identities (must match compose.yaml / Dockerfiles exactly).
FND05_DIGEST_POSTGRES="postgres:18.4@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636"
FND05_DIGEST_SDK="mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0"
FND05_DIGEST_ASPNET="mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b"

fnd05_log()  { printf '[fnd05] %s\n' "$*" >&2; }
fnd05_pass() { printf '[fnd05][PASS] %s\n' "$*" >&2; }
fnd05_fail() { printf '[fnd05][FAIL] %s\n' "$*" >&2; exit 1; }

# All compose invocations pin the explicit project name (D-04) and this repository's
# compose.yaml, independent of the caller's working directory.
compose() {
  docker compose -p "${FND05_PROJECT_NAME}" -f "${FND05_COMPOSE_FILE}" "$@"
}

compose_with_failure_override() {
  docker compose -p "${FND05_PROJECT_NAME}" \
    -f "${FND05_COMPOSE_FILE}" \
    -f "${FND05_FAILURE_OVERRIDE_FILE}" \
    "$@"
}

# Generates an ephemeral secret for automated runs when the operator has not exported one.
# The value lives only in this process's environment; it is never written to disk.
fnd05_ensure_secret() {
  if [[ -z "${POSTGRES_PASSWORD:-}" ]]; then
    POSTGRES_PASSWORD="$(openssl rand -hex 32)"
    export POSTGRES_PASSWORD
    fnd05_log "POSTGRES_PASSWORD was not set; generated an ephemeral value for this run only."
  fi
}

fnd05_container_id() {
  compose ps -a -q "$1"
}

# Prints the container's `docker inspect` .State object, or {} if the container does not exist
# (used to distinguish "never created" from "created but not started").
fnd05_state_json() {
  local id
  id="$(fnd05_container_id "$1")"
  if [[ -z "${id}" ]]; then
    echo '{}'
    return
  fi
  docker inspect "${id}" --format '{{json .State}}'
}

fnd05_iso_to_epoch_ns() {
  date -u -d "$1" +%s%N
}

fnd05_psql() {
  compose exec -T postgres psql -v ON_ERROR_STOP=1 -X -q -U minimal_bank_system -d minimal_bank_system -Atc "$1"
}

fnd05_migration_history() {
  fnd05_psql 'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId";'
}

fnd05_public_tables() {
  fnd05_psql "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name;"
}

fnd05_clean_reset() {
  compose down --volumes --remove-orphans >&2
}
