#!/bin/bash
# V-05 Lifecycle: D-04 canonical stop (retains data) and restart (down + up, re-evaluating the
# Migrator gate). Assumes clean-start.sh has already run against the current volume.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
. ./lib.sh

fnd05_ensure_secret

history_before="$(fnd05_migration_history)"
[[ -n "${history_before}" ]] || fnd05_fail "lifecycle check requires an existing migration history; run clean-start.sh first"

volume_name="$(compose config --format json | jq -r '.volumes["postgres-data"].name')"

fnd05_log "docker compose down --remove-orphans (stop, retain data)"
compose down --remove-orphans

[[ -z "$(fnd05_container_id postgres)" ]] || fnd05_fail "postgres container still exists after 'down --remove-orphans'"
docker volume inspect "${volume_name}" >/dev/null 2>&1 \
  || fnd05_fail "named volume '${volume_name}' was removed by a data-retaining stop"
fnd05_pass "stop retained the named volume '${volume_name}' and removed the containers"

fnd05_log "docker compose up --build --detach --remove-orphans (restart, re-evaluates the Migrator gate)"
compose up --build --detach --remove-orphans

migrator_state="$(fnd05_state_json migrator)"
[[ "$(jq -r '.ExitCode' <<<"${migrator_state}")" == "0" ]] \
  || fnd05_fail "migrator did not exit 0 on restart"
fnd05_pass "Migrator gate was re-evaluated on restart and exited 0"

api_state="$(fnd05_state_json api)"
[[ "$(jq -r '.Status' <<<"${api_state}")" == "running" ]] || fnd05_fail "api is not running after restart"
fnd05_pass "api is running after restart"

history_after="$(fnd05_migration_history)"
[[ "${history_before}" == "${history_after}" ]] \
  || fnd05_fail "migration history changed across a data-retaining restart: before='${history_before}' after='${history_after}'"
fnd05_pass "migration history preserved across restart: ${history_after}"

echo "V-05 LIFECYCLE (STOP / RESTART): PASS"
echo "EVIDENCE: volume=${volume_name}; migrator exit=0 on restart; history=${history_after}"
