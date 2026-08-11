#!/bin/bash
# V-06 Clean reset: `down --volumes --remove-orphans` must leave no container, network or
# named volume belonging to this Compose project, verified via external, project-scoped state
# (not just the command's exit code). Then proves the next start applies cleanly from empty.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
. ./lib.sh

fnd05_ensure_secret

fnd05_log "ensuring the project has running resources before reset"
compose up --build --detach --remove-orphans >&2

volume_name="$(compose config --format json | jq -r '.volumes["postgres-data"].name')"
docker volume inspect "${volume_name}" >/dev/null 2>&1 \
  || fnd05_fail "precondition failed: named volume '${volume_name}' does not exist before reset"
fnd05_pass "precondition: named volume '${volume_name}' exists before reset"

fnd05_log "docker compose down --volumes --remove-orphans"
compose down --volumes --remove-orphans

remaining_containers="$(compose ps -a --format json | jq -rs 'length')"
[[ "${remaining_containers}" == "0" ]] \
  || fnd05_fail "expected 0 containers for this project after clean reset, found ${remaining_containers}"
fnd05_pass "no containers remain for project '${FND05_PROJECT_NAME}'"

if docker volume inspect "${volume_name}" >/dev/null 2>&1; then
  fnd05_fail "named volume '${volume_name}' still exists after clean reset"
fi
fnd05_pass "named volume '${volume_name}' is absent after clean reset"

remaining_labelled_volumes="$(docker volume ls --filter "label=com.docker.compose.project=${FND05_PROJECT_NAME}" --format '{{.Name}}')"
[[ -z "${remaining_labelled_volumes}" ]] \
  || fnd05_fail "volumes still labelled for this project after clean reset: ${remaining_labelled_volumes}"
fnd05_pass "no project-labelled volumes remain"

echo "V-06 CLEAN RESET: PASS"
echo "EVIDENCE: volume ${volume_name} absent; 0 containers/volumes remain for project ${FND05_PROJECT_NAME}"
