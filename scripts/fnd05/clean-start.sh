#!/bin/bash
# V-02 Clean start: clean volume -> PostgreSQL usable -> Migrator exit 0 -> expected migration
# history -> API running only after the Migrator finished (D-05 ordering_success rule).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
. ./lib.sh

fnd05_ensure_secret
fnd05_clean_reset

fnd05_log "docker compose up --build --detach --remove-orphans"
if ! compose up --build --detach --remove-orphans; then
  fnd05_fail "clean start: 'docker compose up' returned non-zero on a clean volume"
fi

postgres_state="$(fnd05_state_json postgres)"
postgres_status="$(jq -r '.Status' <<<"${postgres_state}")"
[[ "${postgres_status}" == "running" ]] || fnd05_fail "postgres is not running after up (status='${postgres_status}')"
postgres_health="$(compose ps -a --format json | jq -rs '.[] | select(.Service=="postgres") | .Health')"
[[ "${postgres_health}" == "healthy" ]] || fnd05_fail "postgres healthcheck did not report healthy (got '${postgres_health}')"
fnd05_pass "PostgreSQL running and healthy"

migrator_state="$(fnd05_state_json migrator)"
migrator_exit="$(jq -r '.ExitCode' <<<"${migrator_state}")"
migrator_status="$(jq -r '.Status' <<<"${migrator_state}")"
[[ "${migrator_status}" == "exited" && "${migrator_exit}" == "0" ]] \
  || fnd05_fail "migrator did not exit 0 (status='${migrator_status}' exit='${migrator_exit}')"
fnd05_pass "Migrator exited 0"

history="$(fnd05_migration_history)"
[[ "${history}" == *"_InitialFoundation" ]] \
  || fnd05_fail "migration history does not contain the expected InitialFoundation migration; got: '${history}'"
row_count="$(wc -l <<<"${history}" | tr -d ' ')"
[[ "${row_count}" == "1" ]] || fnd05_fail "expected exactly 1 migration history row, got ${row_count}: ${history}"
fnd05_pass "migration history contains exactly the expected InitialFoundation row: ${history}"

api_state="$(fnd05_state_json api)"
api_status="$(jq -r '.Status' <<<"${api_state}")"
[[ "${api_status}" == "running" ]] || fnd05_fail "api is not running after clean start (status='${api_status}')"

api_started_at="$(jq -r '.StartedAt' <<<"${api_state}")"
migrator_finished_at="$(jq -r '.FinishedAt' <<<"${migrator_state}")"
api_started_ns="$(fnd05_iso_to_epoch_ns "${api_started_at}")"
migrator_finished_ns="$(fnd05_iso_to_epoch_ns "${migrator_finished_at}")"
[[ "${api_started_ns}" -ge "${migrator_finished_ns}" ]] \
  || fnd05_fail "API StartedAt (${api_started_at}) is before Migrator FinishedAt (${migrator_finished_at})"
fnd05_pass "API StartedAt (${api_started_at}) is not before Migrator FinishedAt (${migrator_finished_at})"

echo "V-02 CLEAN START: PASS"
echo "EVIDENCE: migrator exit=${migrator_exit} finished=${migrator_finished_at}; api started=${api_started_at}; history=${history}"
