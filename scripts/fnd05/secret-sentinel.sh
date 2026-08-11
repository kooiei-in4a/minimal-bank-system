#!/bin/bash
# V-08 Secret non-disclosure: brings the stack up with a distinctive sentinel password and
# confirms it is genuinely used end-to-end (Migrator succeeds, API runs) while never appearing
# in the repository, rendered Compose config, container logs, `docker inspect` Env/Cmd/Args, or
# `docker top` process arguments (D-03 prohibited surfaces).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
. ./lib.sh

SENTINEL="FND05_SECRET_SENTINEL_$(openssl rand -hex 16)"
export POSTGRES_PASSWORD="${SENTINEL}"

fnd05_clean_reset
fnd05_log "docker compose up --build --detach --remove-orphans (sentinel password)"
compose up --build --detach --remove-orphans

migrator_state="$(fnd05_state_json migrator)"
[[ "$(jq -r '.ExitCode' <<<"${migrator_state}")" == "0" ]] \
  || fnd05_fail "precondition failed: migrator did not succeed with the sentinel password, so the secret path was not genuinely exercised"
api_state="$(fnd05_state_json api)"
[[ "$(jq -r '.Status' <<<"${api_state}")" == "running" ]] \
  || fnd05_fail "precondition failed: api is not running with the sentinel password"
fnd05_pass "sentinel password was genuinely used end-to-end (migrator succeeded, api running)"

assert_absent() {
  local label="$1" haystack="$2"
  if grep -qF -- "${SENTINEL}" <<<"${haystack}"; then
    fnd05_fail "secret sentinel leaked into ${label}"
  fi
  fnd05_pass "secret sentinel absent from ${label}"
}

if command -v git >/dev/null 2>&1; then
  repo_matches="$(cd "${FND05_REPO_ROOT}" && git grep -F -- "${SENTINEL}" || true)"
  [[ -z "${repo_matches}" ]] || fnd05_fail "secret sentinel leaked into tracked repository content: ${repo_matches}"
  fnd05_pass "secret sentinel absent from tracked repository content"
fi

assert_absent "rendered Compose config (quiet)" "$(compose config --quiet 2>&1 || true)"
assert_absent "rendered Compose config (json)" "$(compose config --format json)"
assert_absent "container logs" "$(compose logs --no-color 2>&1)"

for role in postgres migrator api; do
  id="$(fnd05_container_id "${role}")"
  assert_absent "'${role}' docker inspect Config.Env/Cmd/Entrypoint/Args" \
    "$(docker inspect "${id}" --format '{{json .Config.Env}} {{json .Config.Cmd}} {{json .Config.Entrypoint}} {{json .Args}}')"
done

for role in migrator api; do
  id="$(fnd05_container_id "${role}")"
  assert_absent "'${role}' docker top process arguments" "$(docker top "${id}" 2>&1 || true)"
done

echo "V-08 SECRET NON-DISCLOSURE: PASS"
echo "EVIDENCE: sentinel exercised end-to-end (migrator exit=0, api running); absent from repo/config/logs/inspect/top"

fnd05_clean_reset
