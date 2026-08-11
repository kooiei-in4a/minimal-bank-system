#!/bin/bash
# V-01 Static configuration validation.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
. ./lib.sh

fnd05_ensure_secret

fnd05_log "Docker Compose version: $(docker compose version --short)"

compose config --quiet
fnd05_pass "docker compose config --quiet"

rendered="$(compose config --format json)"

# The secret payload must never appear in rendered config.
if grep -qF -- "${POSTGRES_PASSWORD}" <<<"${rendered}"; then
  fnd05_fail "POSTGRES_PASSWORD value leaked into rendered Compose config"
fi
fnd05_pass "secret value absent from rendered config"

postgres_image="$(jq -r '.services.postgres.image' <<<"${rendered}")"
[[ "${postgres_image}" == "${FND05_DIGEST_POSTGRES}" ]] \
  || fnd05_fail "postgres image is '${postgres_image}', expected locked digest '${FND05_DIGEST_POSTGRES}'"
fnd05_pass "postgres image matches D-02 locked digest"

volume_type="$(jq -r '.services.postgres.volumes[0].type' <<<"${rendered}")"
volume_source="$(jq -r '.services.postgres.volumes[0].source' <<<"${rendered}")"
[[ "${volume_type}" == "volume" ]] || fnd05_fail "postgres data mount is type '${volume_type}', expected a named volume"
jq -e --arg name "${volume_source}" '.volumes | has($name)' <<<"${rendered}" >/dev/null \
  || fnd05_fail "volume '${volume_source}' is not declared as a top-level named volume"
fnd05_pass "PostgreSQL data uses named volume '${volume_source}'"

jq -e '.secrets.postgres_password.environment == "POSTGRES_PASSWORD"' <<<"${rendered}" >/dev/null \
  || fnd05_fail "top-level secret is not sourced from the host environment"
fnd05_pass "top-level secret sourced from host environment (D-03)"

for role in migrator api; do
  jq -e --arg role "${role}" '.services[$role].secrets | any(.source == "postgres_password")' <<<"${rendered}" >/dev/null \
    || fnd05_fail "service '${role}' is not granted the postgres_password secret"
done
fnd05_pass "migrator and api are granted the secret via explicit per-service grant"

jq -e '.services.migrator.depends_on.postgres.condition == "service_healthy"' <<<"${rendered}" >/dev/null \
  || fnd05_fail "migrator does not depend on postgres becoming healthy"
jq -e '.services.api.depends_on.migrator.condition == "service_completed_successfully"' <<<"${rendered}" >/dev/null \
  || fnd05_fail "api does not depend on migrator completing successfully"
fnd05_pass "ordering dependency conditions match D-01/D-05 (service_healthy / service_completed_successfully)"

fnd05_log "EVIDENCE: rendered config (secrets/images/dependencies only) --"
jq '{services: (.services | with_entries(.value |= {image, depends_on, secrets: (.secrets // null)}))}' <<<"${rendered}" >&2

echo "V-01 STATIC CONFIGURATION VALIDATION: PASS"
