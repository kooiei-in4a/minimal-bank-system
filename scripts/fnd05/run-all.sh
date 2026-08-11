#!/bin/bash
# Runs the full FND-05 Completion Check sequence (C-01..C-08 / V-01..V-08) in the order that
# keeps preconditions valid between stages, and leaves no running resources when it finishes.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
. ./lib.sh

fnd05_ensure_secret

stages=(
  validate.sh
  digest-check.sh
  clean-start.sh
  rerun.sh
  api-no-auto-migration.sh
  lifecycle.sh
  migration-failure.sh
  secret-sentinel.sh
  clean-reset.sh
)

trap 'fnd05_clean_reset || true' EXIT

for stage in "${stages[@]}"; do
  fnd05_log "=== running ${stage} ==="
  bash "./${stage}"
done

fnd05_log "=== all FND-05 completion check stages passed ==="
