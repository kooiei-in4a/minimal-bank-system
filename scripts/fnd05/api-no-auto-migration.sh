#!/bin/bash
# V-07 API no-auto-migration: restarting only the API process (not the Migrator) must never
# change migration history or the public schema. This uses `docker compose restart api` to
# isolate the API's own startup path; it is not used as D-04's canonical restart, which is
# `down` + `up` and re-evaluates the Migrator gate (see lifecycle.sh).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
. ./lib.sh

fnd05_ensure_secret

history_before="$(fnd05_migration_history)"
tables_before="$(fnd05_public_tables)"
[[ -n "${history_before}" ]] || fnd05_fail "no-auto-migration check requires an existing migration history; run clean-start.sh first"

api_state_before="$(fnd05_state_json api)"
[[ "$(jq -r '.Status' <<<"${api_state_before}")" == "running" ]] || fnd05_fail "api must be running before this check"

fnd05_log "docker compose restart api (isolated API-only restart)"
compose restart api

api_state_after="$(fnd05_state_json api)"
[[ "$(jq -r '.Status' <<<"${api_state_after}")" == "running" ]] || fnd05_fail "api is not running after restart"
[[ "$(jq -r '.StartedAt' <<<"${api_state_after}")" != "$(jq -r '.StartedAt' <<<"${api_state_before}")" ]] \
  || fnd05_fail "api StartedAt did not change; the restart may not have taken effect"
fnd05_pass "api process was actually restarted (StartedAt advanced)"

history_after="$(fnd05_migration_history)"
tables_after="$(fnd05_public_tables)"

[[ "${history_before}" == "${history_after}" ]] \
  || fnd05_fail "migration history changed after an API-only restart: before='${history_before}' after='${history_after}'"
[[ "${tables_before}" == "${tables_after}" ]] \
  || fnd05_fail "public schema changed after an API-only restart: before='${tables_before}' after='${tables_after}'"
fnd05_pass "migration history and public schema are unchanged after normal API startup"

echo "V-07 API NO-AUTO-MIGRATION: PASS"
echo "EVIDENCE: history=${history_after}; tables=${tables_after}"
