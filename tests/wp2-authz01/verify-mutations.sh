#!/usr/bin/env bash
set -Eeuo pipefail

# Critical Mutations AUTHZ-STATE-01/02/03. MUTATION_RED requires the same semantic failure
# signature the integration tests assert: a disabled Operator and a stale authorization-state
# version must be rejected with HTTP 401, and a JWT role claim must never authorize a policy.

readonly script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly repository_root="$(cd -- "$script_directory/../.." && pwd)"
readonly project_path="$repository_root/tests/MinimalBankSystem.IntegrationTests/MinimalBankSystem.IntegrationTests.csproj"
readonly filter="${WP2AUTHZ01_TEST_FILTER:-FullyQualifiedName~AuthzPostgreSqlTests}"
readonly mutation_dir="$script_directory/mutations"

applied_mutations=()

restore_all() {
  local patch
  for patch in "${applied_mutations[@]}"; do
    git -C "$repository_root" apply -R "$patch" 2>/dev/null || true
  done
}
trap restore_all EXIT

run_tests() {
  dotnet test "$project_path" --no-build --filter "$filter" --logger "console;verbosity=normal" 2>&1
}

assert_green() {
  run_tests >/dev/null 2>&1
  printf 'AUTHZ-STATE: BASELINE_GREEN\n'
}

expect_mutation_red() {
  local number="$1" semantic="$2"
  local output status
  set +e
  output="$(run_tests 2>&1)"
  status=$?
  set -e
  (( status != 0 )) || return 1
  [[ "$output" == *"AUTHZ-STATE-$number: expected"* &&
     "$output" == *"Semantic failure: $semantic"* ]] || return 1
  printf 'AUTHZ-STATE-%s: MUTATION_RED\n' "$number"
  printf 'AUTHZ-STATE-%s: SEMANTIC_FAILURE=%s\n' "$number" "$semantic"
}

mutation_cycle() {
  local number="$1" semantic="$2" description="$3"
  local patch="$mutation_dir/authz-state-$number.patch"

  git -C "$repository_root" apply "$patch"
  applied_mutations+=("$patch")
  printf 'AUTHZ-STATE-%s: MUTATION_APPLIED=%s\n' "$number" "$description"
  dotnet build "$repository_root/MinimalBankSystem.slnx" --verbosity quiet
  expect_mutation_red "$number" "$semantic"

  git -C "$repository_root" apply -R "$patch"
  applied_mutations=("${applied_mutations[@]/$patch}")
  dotnet build "$repository_root/MinimalBankSystem.slnx" --verbosity quiet
  assert_green
  printf 'AUTHZ-STATE-%s: RESTORE_GREEN\n' "$number"
}

dotnet build "$repository_root/MinimalBankSystem.slnx" --verbosity quiet
assert_green

mutation_cycle 01 'disabled operator must be rejected with 401' 'disabled-state-check-removed'
mutation_cycle 02 'stale authorization-state version must be rejected with 401' 'authorization-state-version-check-removed'
mutation_cycle 03 'a JWT role claim must never authorize a policy' 'jwt-role-claim-became-authoritative'

printf 'AUTHZ-STATE: KILLED\n'
printf 'WP2_AUTHZ01_CRITICAL_MUTATION: PASS\n'
