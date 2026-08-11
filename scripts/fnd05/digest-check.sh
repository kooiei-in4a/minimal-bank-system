#!/bin/bash
# C-04 / M-05 oracle: every base image reference must be digest-qualified and match the
# exact D-02 locked digest. A bare tag (no @sha256:...) fails this check even if the tag
# name looks correct, because tags are mutable and not treated as immutable evidence.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
. ./lib.sh

check_file_has_exact_digest() {
  local file="$1" expected="$2" label="$3"
  [[ -f "${file}" ]] || fnd05_fail "${label}: file '${file}' does not exist"

  if ! grep -qF -- "${expected}" "${file}"; then
    fnd05_fail "${label}: '${file}' does not reference the locked digest '${expected}'"
  fi

  # Reject a same-tag bare reference (no @sha256:) anywhere else in the file, which would
  # indicate a tag-only fallback path alongside the pinned one.
  local tag="${expected%%@*}"
  local bare_matches
  bare_matches="$(grep -Fn -- "${tag}" "${file}" | grep -vF -- "${expected}" || true)"
  if [[ -n "${bare_matches}" ]]; then
    fnd05_fail "${label}: '${file}' contains a tag-only reference to '${tag}' outside the pinned digest line:"$'\n'"${bare_matches}"
  fi
}

check_file_has_exact_digest "${FND05_REPO_ROOT}/src/MinimalBankSystem.Api/Dockerfile" \
  "${FND05_DIGEST_SDK}" "api build stage"
check_file_has_exact_digest "${FND05_REPO_ROOT}/src/MinimalBankSystem.Api/Dockerfile" \
  "${FND05_DIGEST_ASPNET}" "api runtime stage"
fnd05_pass "src/MinimalBankSystem.Api/Dockerfile pins SDK and ASP.NET runtime by digest"

check_file_has_exact_digest "${FND05_REPO_ROOT}/src/MinimalBankSystem.Migrator/Dockerfile" \
  "${FND05_DIGEST_SDK}" "migrator build stage"
check_file_has_exact_digest "${FND05_REPO_ROOT}/src/MinimalBankSystem.Migrator/Dockerfile" \
  "${FND05_DIGEST_ASPNET}" "migrator runtime stage"
fnd05_pass "src/MinimalBankSystem.Migrator/Dockerfile pins SDK and ASP.NET runtime by digest"

check_file_has_exact_digest "${FND05_COMPOSE_FILE}" "${FND05_DIGEST_POSTGRES}" "compose.yaml postgres service"
fnd05_pass "compose.yaml pins postgres by digest"

echo "C-04 DIGEST PINNING: PASS"
