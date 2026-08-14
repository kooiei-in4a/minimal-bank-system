#!/usr/bin/env bash
set -Eeuo pipefail

# Critical FND-06 mutation pilot. Each mutation is made only in the working tree, is killed by
# the named oracle, then is byte-for-byte restored before the next pilot begins. Nothing here is
# a product change or a commit.

readonly repository_root="$(git rev-parse --show-toplevel)"
readonly source_file="$repository_root/src/MinimalBankSystem.Api/Runtime/HealthContract.cs"
readonly test_project="$repository_root/tests/MinimalBankSystem.IntegrationTests/MinimalBankSystem.IntegrationTests.csproj"
readonly sentinel="${FND06_SECRET_SENTINEL:-FND06_TEST_SENTINEL_NOT_A_CREDENTIAL}"
readonly jwt_sentinel="${sentinel}_JWT"
readonly jwt_secret_sentinel="$(printf '%s' "$jwt_sentinel" | base64 | tr -d '\n')"
export MBS_JWT_SIGNING_KEY="${MBS_JWT_SIGNING_KEY:-$jwt_secret_sentinel}"

if command -v dotnet >/dev/null; then
  readonly dotnet_host="$(command -v dotnet)"
elif command -v dotnet.exe >/dev/null; then
  readonly dotnet_host="$(command -v dotnet.exe)"
elif [[ -x '/c/Program Files/dotnet/dotnet.exe' ]]; then
  readonly dotnet_host='/c/Program Files/dotnet/dotnet.exe'
else
  printf 'FND-06 mutation prerequisite missing: dotnet host\n' >&2
  exit 69
fi

if [[ "$dotnet_host" == *.exe ]] && command -v wslpath >/dev/null; then
  readonly dotnet_test_project="$(wslpath -w "$test_project")"
elif [[ "$dotnet_host" == *.exe ]] && command -v cygpath >/dev/null; then
  readonly dotnet_test_project="$(cygpath -w "$test_project")"
else
  readonly dotnet_test_project="$test_project"
fi

backup_file=''

restore_source() {
  if [[ -n "$backup_file" && -f "$backup_file" ]]; then
    cp "$backup_file" "$source_file"
    cmp --silent "$backup_file" "$source_file"
    rm -f "$backup_file"
    backup_file=''
  fi
}

trap restore_source EXIT

run_oracle_green() {
  local test_filter="$1"
  "$dotnet_host" test "$dotnet_test_project" --no-restore --filter "$test_filter" --verbosity minimal
}

expect_oracle_red() {
  local test_filter="$1" expected_test="$2" expected_signature="$3" output exit_code
  set +e
  output="$("$dotnet_host" test "$dotnet_test_project" --no-restore --filter "$test_filter" --verbosity minimal 2>&1)"
  exit_code=$?
  set -e

  (( exit_code != 0 )) || {
    printf 'ORACLE_SIGNATURE=mutation-not-killed:%s\n' "$expected_test" >&2
    return 1
  }
  [[ "$output" == *"$expected_test"* ]] || {
    printf 'ORACLE_SIGNATURE=unexpected-mutation-failure:%s\n' "$expected_test" >&2
    printf '%s\n' "$output" >&2
    return 1
  }
  [[ "$output" == *"$expected_signature"* ]] || {
    printf 'ORACLE_SIGNATURE=unexpected-mutation-failure:missing-semantic-signature:%s\n' "$expected_signature" >&2
    printf '%s\n' "$output" >&2
    return 1
  }
}

replace_once() {
  local before="$1" after="$2" count
  count="$(grep --fixed-strings -- "$before" "$source_file" | wc -l | tr -d '[:space:]')"
  [[ "$count" == 1 ]] || {
    printf 'Expected exactly one mutation target, found %s: %s\n' "$count" "$before" >&2
    return 1
  }
  sed -i "s|$before|$after|" "$source_file"
  grep --fixed-strings --quiet "$after" "$source_file"
}

run_mutation() {
  local name="$1" before="$2" after="$3" test_filter="$4" test_name="$5" signature="$6"
  printf '%s: BASELINE_GREEN\n' "$name"
  run_oracle_green "$test_filter"

  backup_file="$(mktemp)"
  cp "$source_file" "$backup_file"
  replace_once "$before" "$after"

  printf '%s: MUTATION_APPLIED\n' "$name"
  expect_oracle_red "$test_filter" "$test_name" "$signature"
  printf '%s: MUTATION_RED\n' "$name"
  printf '%s: EXPECTED_REASON_CONFIRMED=%s\n' "$name" "$signature"

  restore_source
  run_oracle_green "$test_filter"
  printf '%s: RESTORE_GREEN\n' "$name"
}

# MUT-01: ready must become unavailable while PostgreSQL is stopped.
run_mutation \
  MUT01 \
  'return Reject(DatabaseUnreachable);' \
  'return HealthCheckResult.Healthy();' \
  'FullyQualifiedName~HealthTransitionTests.LivenessSurvivesRealPostgreSqlStopAndReadinessRecoversWithoutApiRestart' \
  'HealthTransitionTests.LivenessSurvivesRealPostgreSqlStopAndReadinessRecoversWithoutApiRestart' \
  'ORACLE_SIGNATURE=FND06_MUT01_READY_MUST_FAIL_WHEN_POSTGRES_STOPPED'

# MUT-02: liveness must never execute any readiness-tagged registration.
run_mutation \
  MUT02 \
  'CreateOptions(_ => false)' \
  'CreateOptions(registration => registration.Tags.Contains(ReadinessTag))' \
  'FullyQualifiedName~HealthContractTests.LivenessNeverExecutesAReadinessTaggedCheck' \
  'HealthContractTests.LivenessNeverExecutesAReadinessTaggedCheck' \
  'ORACLE_SIGNATURE=FND06_MUT02_LIVENESS_MUST_NOT_EXECUTE_READINESS'

# MUT-03: a reachable but migration-incomplete PostgreSQL database must not be reported ready.
run_mutation \
  MUT03 \
  '? Reject(MigrationsPending)' \
  '? HealthCheckResult.Healthy()' \
  'FullyQualifiedName~HealthReadinessTests.ReadinessRejectsAReachableUnmigratedDatabaseWithoutCreatingSchema' \
  'HealthReadinessTests.ReadinessRejectsAReachableUnmigratedDatabaseWithoutCreatingSchema' \
  'ORACLE_SIGNATURE=FND06_MUT03_PENDING_MIGRATION_MUST_NOT_BE_READY'

[[ -z "$backup_file" ]] || {
  printf 'ORACLE_SIGNATURE=mutation-residue\n' >&2
  exit 1
}
printf 'FND06_CRITICAL_MUTATIONS: PASS\n'
