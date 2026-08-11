#!/bin/bash
# V-03 Migration failure: the migrator fails deterministically (unreachable DB_PORT via the
# test-only override in docker/compose.override.migration-failure.yaml) and the API must
# never start. "Started then exited" is not accepted as evidence of "never started" (D-05).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
. ./lib.sh

fnd05_ensure_secret
fnd05_clean_reset

fnd05_log "docker compose (+ migration-failure override) up --build --detach --remove-orphans"
up_exit=0
compose_with_failure_override up --build --detach --remove-orphans || up_exit=$?
[[ "${up_exit}" -ne 0 ]] || fnd05_fail "expected 'docker compose up' to report failure when the migrator fails, got exit 0"
fnd05_pass "docker compose up reported failure (exit ${up_exit})"

migrator_state="$(fnd05_state_json migrator)"
migrator_exit="$(jq -r '.ExitCode' <<<"${migrator_state}")"
migrator_status="$(jq -r '.Status' <<<"${migrator_state}")"
[[ "${migrator_status}" == "exited" ]] || fnd05_fail "migrator is not in 'exited' state (got '${migrator_status}')"
[[ "${migrator_exit}" != "0" ]] || fnd05_fail "migrator unexpectedly exited 0 under the induced failure"
fnd05_pass "Migrator exited non-zero (${migrator_exit}) as required for the failure path"

api_id="$(fnd05_container_id api)"
if [[ -z "${api_id}" ]]; then
  fnd05_pass "API container was never created"
else
  api_state="$(fnd05_state_json api)"
  api_status="$(jq -r '.Status' <<<"${api_state}")"
  api_started_at="$(jq -r '.StartedAt' <<<"${api_state}")"
  [[ "${api_status}" == "created" && "${api_started_at}" == "0001-01-01T00:00:00Z" ]] \
    || fnd05_fail "API must never start after a migrator failure, but observed status='${api_status}' StartedAt='${api_started_at}'"
  fnd05_pass "API container exists only in 'created' state and was never started (StartedAt is the zero value)"
fi

history_table_exists="$(fnd05_psql "SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL;")"
if [[ "${history_table_exists}" == "t" ]]; then
  history="$(fnd05_migration_history)"
  [[ -z "${history}" ]] || fnd05_fail "migration history is non-empty after an intended migration failure: ${history}"
  fnd05_pass "migration history table exists but has no rows after the induced failure"
else
  history="(table does not exist)"
  fnd05_pass "migration history table was never created after the induced failure"
fi

migrator_logs="$(compose_with_failure_override logs migrator --no-color 2>&1)"
if grep -qiE 'migration completed' <<<"${migrator_logs}"; then
  fnd05_fail "migrator logs report completion despite the induced connection failure"
fi
fnd05_pass "migrator logs do not report a false success"

echo "V-03 MIGRATION FAILURE / API NON-START: PASS"
echo "EVIDENCE: migrator exit=${migrator_exit} status=${migrator_status}; api never started; history=(empty)"

fnd05_clean_reset
