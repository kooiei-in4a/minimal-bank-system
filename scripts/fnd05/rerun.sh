#!/bin/bash
# V-04 Existing-volume rerun: re-running the Migrator against a database that is already at
# the latest migration must stay success (exit 0) and must not duplicate migration history.
# Assumes clean-start.sh has already run against the current volume.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
. ./lib.sh

fnd05_ensure_secret

before="$(fnd05_migration_history)"
[[ -n "${before}" ]] || fnd05_fail "rerun check requires an existing migration history; run clean-start.sh first"

fnd05_log "docker compose run --rm --no-deps migrator (rerun on existing volume)"
run_exit=0
compose run --rm --no-deps migrator || run_exit=$?
[[ "${run_exit}" -eq 0 ]] || fnd05_fail "migrator rerun on an already-migrated database exited ${run_exit}, expected 0"
fnd05_pass "migrator rerun exited 0"

after="$(fnd05_migration_history)"
[[ "${before}" == "${after}" ]] \
  || fnd05_fail "migration history changed after rerun: before='${before}' after='${after}'"
fnd05_pass "migration history unchanged after rerun: ${after}"

echo "V-04 EXISTING-VOLUME RERUN: PASS"
echo "EVIDENCE: history before=${before} after=${after}"
