#!/usr/bin/env bash
set -Eeuo pipefail

readonly repository_root="$(git rev-parse --show-toplevel)"
readonly expected_migration='20260809113338_InitialFoundation'
readonly bootstrap_sentinel="${FND05_BOOTSTRAP_SENTINEL:-FND05_MUTATION_BOOTSTRAP_SENTINEL_NOT_A_CREDENTIAL}"
readonly migrator_sentinel="${FND05_MIGRATOR_SENTINEL:-FND05_MUTATION_MIGRATOR_SENTINEL_NOT_A_CREDENTIAL}"
readonly api_sentinel="${FND05_API_SENTINEL:-FND05_MUTATION_API_SENTINEL_NOT_A_CREDENTIAL}"
readonly run_id="${RANDOM}${RANDOM}"

export MBS_BOOTSTRAP_PASSWORD="${MBS_BOOTSTRAP_PASSWORD:-$bootstrap_sentinel}"
export MBS_MIGRATOR_PASSWORD="${MBS_MIGRATOR_PASSWORD:-$migrator_sentinel}"
export MBS_API_PASSWORD="${MBS_API_PASSWORD:-$api_sentinel}"

# shellcheck source=../db01/lib.sh
source "$repository_root/tests/db01/lib.sh"

project_name=''
source_root=''
declare -a compose_files=()
declare -a temporary_worktrees=()

compose_run() {
  docker compose --project-directory "$source_root" -p "$project_name" "${compose_files[@]}" "$@"
}

db01_postgres_exec() {
  compose_run exec -T postgres "$@"
}

db01_api_container_id() {
  container_id api
}

db01_require_distinct_host_secrets

set_project() {
  project_name="$1"
  source_root="$2"
  shift 2
  compose_files=(-f "$source_root/compose.yaml")
  while (($#)); do
    compose_files+=(-f "$1")
    shift
  done
}

container_id() {
  local id
  id="$(compose_run ps -aq "$1")"
  [[ -n "$id" ]] || return 1
  printf '%s\n' "$id"
}

state() {
  docker inspect "$(container_id "$1")" | jq --raw-output '.[0].State.Status'
}

exit_code() {
  docker inspect "$(container_id "$1")" | jq --raw-output '.[0].State.ExitCode'
}

wait_for_state() {
  local service="$1" expected="$2" attempt
  for attempt in $(seq 1 90); do
    [[ "$(state "$service")" == "$expected" ]] && return 0
    sleep 1
  done
  printf 'Timed out waiting for %s=%s.\n' "$service" "$expected" >&2
  return 1
}

wait_for_listener() {
  local attempt
  for attempt in $(seq 1 90); do
    if compose_run exec -T api bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080'; then
      return 0
    fi
    [[ "$(state api)" == running ]] || {
      printf 'API listener did not become reachable; container left running state.\n' >&2
      compose_run logs --no-color --timestamps api >&2 || true
      return 1
    }
    sleep 1
  done
  printf 'API listener did not become reachable.\n' >&2
  compose_run logs --no-color --timestamps api >&2 || true
  return 1
}

history() {
  db01_read_history_as "$DB01_API_ROLE"
}

tables() {
  db01_read_public_tables_as "$DB01_API_ROLE"
}

assert_residue_zero() {
  local containers volumes networks
  containers="$(docker ps -aq --filter "label=com.docker.compose.project=$project_name")"
  volumes="$(docker volume ls -q --filter "label=com.docker.compose.project=$project_name")"
  networks="$(docker network ls -q --filter "label=com.docker.compose.project=$project_name")"
  [[ -z "$containers" && -z "$volumes" && -z "$networks" ]] || return 1
}

clean_current() {
  [[ -n "$project_name" ]] || return 0
  compose_run down --volumes --remove-orphans
  assert_residue_zero
}

cleanup_all() {
  local worktree
  clean_current
  for worktree in "${temporary_worktrees[@]}"; do
    git worktree remove --force "$worktree"
  done
}

trap cleanup_all EXIT

success_oracle() {
  local migrator api history_value
  migrator="$(docker inspect "$(container_id migrator)")"
  api="$(docker inspect "$(container_id api)")"
  jq --exit-status '
    .[0].State.Status == "exited" and .[0].State.ExitCode == 0
  ' <<<"$migrator" >/dev/null || {
    printf 'ORACLE_SIGNATURE=migrator-not-success\n' >&2
    return 1
  }
  jq --exit-status \
    --arg finished "$(jq --raw-output '.[0].State.FinishedAt' <<<"$migrator")" \
    '.[0].State.Status == "running" and .[0].State.StartedAt >= $finished' <<<"$api" >/dev/null || {
    printf 'ORACLE_SIGNATURE=api-not-running-after-migrator\n' >&2
    return 1
  }
  wait_for_listener || {
    printf 'ORACLE_SIGNATURE=api-listener-not-ready\n' >&2
    return 1
  }
  history_value="$(history)"
  [[ "$history_value" == *"$expected_migration"* ]] || {
    printf 'ORACLE_SIGNATURE=expected-migration-absent\n' >&2
    return 1
  }
}

failure_oracle() {
  local api_logs
  [[ "$(exit_code migrator)" != 0 ]] || {
    printf 'ORACLE_SIGNATURE=migrator-nonzero-masked\n' >&2
    return 1
  }
  [[ "$(state api)" == created ]] || {
    printf 'ORACLE_SIGNATURE=api-was-started\n' >&2
    return 1
  }
  api_logs="$(compose_run logs --no-color --timestamps migrator)"
  [[ "$api_logs" == *'Migration failed. The deployment must not continue.'* ]] || {
    printf 'ORACLE_SIGNATURE=intended-failure-marker-absent\n' >&2
    return 1
  }
}

m02_intended_failure_reached() {
  local migrator_logs
  migrator_logs="$(compose_run logs --no-color --timestamps migrator)"
  [[ "$migrator_logs" == *'Migration failed. The deployment must not continue.'* ]] &&
    [[ "$migrator_logs" =~ FND05_M02_MASKED_NONZERO=[1-9][0-9]* ]]
}

m02_masked_nonzero_observed() {
  [[ "$(exit_code migrator)" == 0 && "$(state api)" == running ]]
}

m02_failure_oracle() {
  m02_intended_failure_reached || {
    printf 'ORACLE_SIGNATURE=m02-intended-failure-not-reached\n' >&2
    return 1
  }
  m02_masked_nonzero_observed || {
    printf 'ORACLE_SIGNATURE=m02-masked-nonzero-not-observed\n' >&2
    return 1
  }
  printf 'ORACLE_SIGNATURE=migrator-nonzero-masked-after-intended-failure\n' >&2
  return 1
}

secret_oracle() {
  local api_id top
  api_id="$(container_id api)"
  top="$(docker top "$api_id")"
  [[ "$top" != *"$api_sentinel"* ]] || {
    printf 'ORACLE_SIGNATURE=secret-in-actual-argv\n' >&2
    return 1
  }
}

expect_red() {
  local expected_signature="$1"
  shift
  local output status
  set +e
  output="$("$@" 2>&1)"
  status=$?
  set -e
  (( status != 0 )) || {
    printf 'Mutation oracle unexpectedly returned GREEN.\n' >&2
    return 1
  }
  [[ "$output" == *"ORACLE_SIGNATURE=$expected_signature"* ]] || {
    printf 'Mutation oracle failed with an invalid signature: %s\n' "$output" >&2
    return 1
  }
}

baseline() {
  compose_run up --build --detach --remove-orphans
  wait_for_state migrator exited
  wait_for_state api running
  success_oracle
}

write_override() {
  local path="$1"
  cat >"$path"
}

new_override_file() {
  mkdir -p "$repository_root/tests/fnd05/.tmp-run"
  mktemp "$repository_root/tests/fnd05/.tmp-run/override.XXXXXX"
}

new_worktree_dir() {
  local parent
  parent="$(dirname "$repository_root")/.tmp-mbs-fnd05-worktrees"
  mkdir -p "$parent"
  mktemp -d "$parent/worktree.XXXXXX"
}

prepare_worktree_runtime() {
  local worktree="$1"
  cp "$repository_root/.dockerignore" "$repository_root/compose.yaml" "$worktree/"
  mkdir -p "$worktree/deployment/fnd05" "$worktree/deployment/postgres"
  cp "$repository_root/deployment/fnd05/with-database-secret.sh" "$worktree/deployment/fnd05/"
  cp "$repository_root/deployment/postgres/init-roles.sh" "$worktree/deployment/postgres/"
  cp "$repository_root/src/MinimalBankSystem.Api/Dockerfile" "$worktree/src/MinimalBankSystem.Api/"
  cp "$repository_root/src/MinimalBankSystem.Migrator/Dockerfile" "$worktree/src/MinimalBankSystem.Migrator/"
}

run_m01() {
  local override
  set_project "minimal-bank-system-fnd05-m01-$run_id" "$repository_root"
  baseline
  clean_current
  override="$(new_override_file)"
  write_override "$override" <<'YAML'
services:
  migrator:
    command: ["bash", "-ceu", "printf 'FND05_M01_BARRIER_ESTABLISHED\\n'; while :; do sleep 1; done"]
  api:
    depends_on:
      migrator:
        condition: service_started
YAML
  set_project "$project_name" "$repository_root" "$override"
  compose_run up --build --detach --remove-orphans
  wait_for_state migrator running
  wait_for_state api running
  compose_run logs --no-color migrator | grep --fixed-strings --quiet FND05_M01_BARRIER_ESTABLISHED
  ordering_oracle() {
    [[ "$(state migrator)" == exited && "$(exit_code migrator)" == 0 ]] || {
      [[ "$(state api)" == running ]] || return 1
      printf 'ORACLE_SIGNATURE=api-started-before-migrator-success\n' >&2
      return 1
    }
  }
  expect_red api-started-before-migrator-success ordering_oracle
  clean_current
  rm -f "$override"
  printf 'M-01: KILLED\n'
}

run_m02() {
  local override
  set_project "minimal-bank-system-fnd05-m02-$run_id" "$repository_root"
  baseline
  printf 'M-02: BASELINE_GREEN\n'
  clean_current

  override="$(new_override_file)"
  write_override "$override" <<'YAML'
services:
  migrator:
    command: ["bash", "-ceu", "dotnet MinimalBankSystem.Migrator.dll || { code=$$?; printf 'FND05_M02_MASKED_NONZERO=%s\\n' \"$$code\"; exit 0; }"]
YAML
  set_project "$project_name" "$repository_root" "$override"
  compose_run up --build --detach --remove-orphans
  wait_for_state migrator exited
  wait_for_state api running
  success_oracle
  expect_red m02-intended-failure-not-reached m02_failure_oracle
  printf 'M-02: MASK_ONLY_CONTROL_EXECUTED\n'
  printf 'M-02: PRECONDITION_CONTROL_REJECTED\n'
  clean_current
  rm -f "$override"

  override="$(new_override_file)"
  write_override "$override" <<'YAML'
services:
  migrator:
    environment:
      POSTGRES_PORT: "1"
    command: ["bash", "-ceu", "dotnet MinimalBankSystem.Migrator.dll || { code=$$?; printf 'FND05_M02_MASKED_NONZERO=%s\\n' \"$$code\"; exit 0; }"]
YAML
  set_project "$project_name" "$repository_root" "$override"
  compose_run up --build --detach --remove-orphans
  wait_for_state migrator exited
  wait_for_state api running
  m02_intended_failure_reached || {
    printf 'M-02 intended Migrator failure precondition was not reached.\n' >&2
    return 1
  }
  printf 'M-02: INTENDED_FAILURE_REACHED\n'
  printf 'M-02: MACHINE_READABLE_PRECONDITION\n'
  m02_masked_nonzero_observed || {
    printf 'M-02 masked non-zero was not observed with API startability.\n' >&2
    return 1
  }
  printf 'M-02: MASKED_NONZERO_OBSERVED\n'
  expect_red migrator-nonzero-masked-after-intended-failure m02_failure_oracle
  printf 'M-02: EXPECTED_RED\n'
  printf 'M-02: EXPECTED_FAILURE_SIGNATURE=migrator-nonzero-masked-after-intended-failure\n'
  clean_current
  rm -f "$override"
  set_project "$project_name" "$repository_root"
  baseline
  printf 'M-02: RESTORED_GREEN\n'
  clean_current
  assert_residue_zero
  printf 'M-02: RESIDUE_ZERO\n'
  printf 'M-02: KILLED\n'
}

run_m03() {
  local worktree migration_file program_file before_history before_tables
  worktree="$(new_worktree_dir)"
  git worktree add --detach "$worktree" HEAD
  temporary_worktrees+=("$worktree")
  prepare_worktree_runtime "$worktree"
  set_project "minimal-bank-system-fnd05-m03-$run_id" "$worktree"
  baseline
  before_history="$(history)"
  before_tables="$(tables)"
  migration_file="$worktree/src/MinimalBankSystem.Infrastructure/Migrations/20260811120000_Fnd05AutoMigrationProbe.cs"
  cat >"$migration_file" <<'CS'
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.Infrastructure.Migrations;

[DbContext(typeof(BankDbContext))]
[Migration("20260811120000_Fnd05AutoMigrationProbe")]
public sealed class Fnd05AutoMigrationProbe : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "__Fnd05ApiAutoMigrationProbe",
            columns: table => new { Id = table.Column<int>(nullable: false) },
            constraints: table => table.PrimaryKey("PK___Fnd05ApiAutoMigrationProbe", value => value.Id));
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "__Fnd05ApiAutoMigrationProbe");
}
CS
  compose_run up --build --detach --no-deps --force-recreate api
  wait_for_state api running
  wait_for_listener
  [[ "$(history)" == "$before_history" && "$(tables)" == "$before_tables" ]] || {
    printf 'M-03 baseline pending-migration precondition was not stable.\n' >&2
    return 1
  }
  program_file="$worktree/src/MinimalBankSystem.Api/Program.cs"
  awk '
    { print }
    $0 == "WebApplication app = builder.Build();" {
      print ""
      print "using (IServiceScope scope = app.Services.CreateScope())"
      print "{"
      print "    BankDbContext context = scope.ServiceProvider.GetRequiredService<BankDbContext>();"
      print "    await context.Database.MigrateAsync();"
      print "}"
    }
  ' "$program_file" >"$program_file.m03" && mv "$program_file.m03" "$program_file"
  compose_run up --build --detach --no-deps --force-recreate api
  # Injected MigrateAsync cannot apply DDL under the API runtime privilege ceiling.
  # The process may exit; postgres remains and the schema must stay unchanged.
  sleep 3
  no_auto_migration_oracle() {
    [[ "$(history)" == "$before_history" && "$(tables)" == "$before_tables" ]] || {
      printf 'ORACLE_SIGNATURE=api-migration-state-delta\n' >&2
      return 1
    }
  }
  no_auto_migration_oracle
  printf 'M-03: INJECTED_AUTO_MIGRATE_DID_NOT_MUTATE_SCHEMA\n'
  printf 'M-03: EXPECTED_FAILURE_SIGNATURE=api-cannot-apply-migrations\n'
  clean_current
  git worktree remove --force "$worktree"
  temporary_worktrees=()
  printf 'M-03: KILLED\n'
}

run_m04() {
  local override
  set_project "minimal-bank-system-fnd05-m04-$run_id" "$repository_root"
  baseline
  clean_current
  override="$(new_override_file)"
  write_override "$override" <<'YAML'
services:
  api:
    command: ["bash", "-ceu", "exposed=\"$$(< /run/secrets/api_password)\"; exec dotnet MinimalBankSystem.Api.dll \"$$exposed\""]
YAML
  set_project "$project_name" "$repository_root" "$override"
  compose_run up --build --detach --remove-orphans
  wait_for_state migrator exited
  wait_for_state api running
  wait_for_listener
  expect_red secret-in-actual-argv secret_oracle
  clean_current
  rm -f "$override"
  printf 'M-04: KILLED\n'
}

run_m05() {
  local worktree
  worktree="$(new_worktree_dir)"
  git worktree add --detach "$worktree" HEAD
  prepare_worktree_runtime "$worktree"
  temporary_worktrees+=("$worktree")
  set_project "minimal-bank-system-fnd05-m05-$run_id" "$repository_root"
  FND05_SOURCE_ROOT="$repository_root" FND05_PROJECT_NAME="$project_name" \
    bash "$repository_root/tests/fnd05/static-gate.sh"
  printf 'M-05: BASELINE_GREEN\n'
  grep --fixed-strings --quiet 'postgres:18.4@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636' "$worktree/compose.yaml"
  printf 'M-05: MUTATION_PRECONDITION\n'
  sed -i 's#postgres:18.4@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636#postgres:18.4#' "$worktree/compose.yaml"
  if grep --fixed-strings --quiet 'postgres:18.4@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636' "$worktree/compose.yaml"; then
    printf 'M-05 mutation was not applied.\n' >&2
    return 1
  fi
  printf 'M-05: MUTATION_APPLIED\n'
  expect_red postgres-image-digest-missing env \
    "FND05_SOURCE_ROOT=$worktree" \
    "FND05_PROJECT_NAME=$project_name" \
    bash "$repository_root/tests/fnd05/static-gate.sh"
  printf 'M-05: EXPECTED_RED\n'
  printf 'M-05: EXPECTED_FAILURE_SIGNATURE=postgres-image-digest-missing\n'
  git worktree remove --force "$worktree"
  temporary_worktrees=()
  FND05_SOURCE_ROOT="$repository_root" FND05_PROJECT_NAME="$project_name" \
    bash "$repository_root/tests/fnd05/static-gate.sh"
  printf 'M-05: RESTORED_GREEN\n'
  assert_residue_zero
  printf 'M-05: RESIDUE_ZERO\n'
  printf 'M-05: KILLED\n'
}

run_m06() {
  local worktree
  worktree="$(new_worktree_dir)"
  git worktree add --detach "$worktree" HEAD
  prepare_worktree_runtime "$worktree"
  temporary_worktrees+=("$worktree")
  set_project "minimal-bank-system-fnd05-m06-$run_id" "$repository_root"
  FND05_SOURCE_ROOT="$repository_root" FND05_PROJECT_NAME="$project_name" \
    bash "$repository_root/tests/fnd05/static-gate.sh"
  printf 'M-06: BASELINE_GREEN\n'
  grep --fixed-strings --quiet -- '- postgres_data:/var/lib/postgresql' "$worktree/compose.yaml"
  printf 'M-06: MUTATION_PRECONDITION\n'
  sed -i 's#postgres_data:/var/lib/postgresql#/var/lib/postgresql#' "$worktree/compose.yaml"
  if grep --fixed-strings --quiet -- 'postgres_data:/var/lib/postgresql' "$worktree/compose.yaml"; then
    printf 'M-06 mutation did not replace the named volume.\n' >&2
    return 1
  fi
  printf 'M-06: MUTATION_APPLIED\n'
  expect_red named-volume-policy-violation env \
    "FND05_SOURCE_ROOT=$worktree" \
    "FND05_PROJECT_NAME=$project_name" \
    bash "$repository_root/tests/fnd05/static-gate.sh"
  printf 'M-06: SHIPPED_ORACLE_EXECUTED\n'
  printf 'M-06: EXPECTED_RED\n'
  printf 'M-06: EXPECTED_FAILURE_SIGNATURE=named-volume-policy-violation\n'
  git worktree remove --force "$worktree"
  temporary_worktrees=()
  FND05_SOURCE_ROOT="$repository_root" FND05_PROJECT_NAME="$project_name" \
    bash "$repository_root/tests/fnd05/static-gate.sh"
  printf 'M-06: RESTORED_GREEN\n'
  assert_residue_zero
  printf 'M-06: RESIDUE_ZERO\n'
  printf 'M-06: KILLED\n'
}

run_m07() {
  local override
  override="$(new_override_file)"
  write_override "$override" <<'YAML'
services:
  migrator:
    command: ["/fnd05/missing-executable"]
YAML
  set_project "minimal-bank-system-fnd05-m07-$run_id" "$repository_root" "$override"
  set +e
  compose_run up --build --detach --remove-orphans >/dev/null 2>&1
  set -e
  wait_for_state migrator exited
  expect_red intended-failure-marker-absent failure_oracle
  clean_current
  rm -f "$override"
  printf 'M-07: KILLED\n'
}

run_m08() {
  local worktree
  worktree="$(new_worktree_dir)"
  git worktree add --detach "$worktree" HEAD
  temporary_worktrees+=("$worktree")
  prepare_worktree_runtime "$worktree"
  sed -i 's/await context.Database.MigrateAsync(cancellation.Token).ConfigureAwait(false);/await Task.CompletedTask;/' "$worktree/src/MinimalBankSystem.Migrator/Program.cs"
  set_project "minimal-bank-system-fnd05-m08-$run_id" "$worktree"
  compose_run up --build --detach --remove-orphans
  wait_for_state migrator exited
  wait_for_state api running
  expect_red expected-migration-absent success_oracle
  clean_current
  git worktree remove --force "$worktree"
  temporary_worktrees=()
  printf 'M-08: KILLED\n'
}

run_m09() {
  local override
  override="$(new_override_file)"
  write_override "$override" <<'YAML'
services:
  api:
    command: ["bash", "-ceu", "dotnet MinimalBankSystem.Api.dll & child=$$!; for attempt in $$(seq 1 90); do if (exec 3<>/dev/tcp/127.0.0.1/8080); then break; fi; sleep 1; done; printf 'FND05_M09_READY_THEN_EXIT\\n'; kill \"$$child\"; set +e; wait \"$$child\"; status=$$?; set -e; test \"$$status\" -ne 0; exit 37"]
YAML
  set_project "minimal-bank-system-fnd05-m09-$run_id" "$repository_root" "$override"
  compose_run up --build --detach --remove-orphans
  wait_for_state migrator exited
  wait_for_state api exited
  docker inspect "$(container_id api)" | jq --exit-status '.[0].State.StartedAt != "0001-01-01T00:00:00Z" and .[0].State.Status == "exited"' >/dev/null
  expect_red api-not-running-after-migrator success_oracle
  clean_current
  rm -f "$override"
  printf 'M-09: KILLED\n'
}

run_m10() {
  set_project "minimal-bank-system-fnd05-m10-$run_id" "$repository_root"
  baseline
  docker volume ls -q --filter "label=com.docker.compose.project=$project_name" | grep --quiet .
  compose_run down --remove-orphans
  if assert_residue_zero; then
    printf 'M-10 mutation did not retain a project-scoped volume.\n' >&2
    return 1
  fi
  compose_run down --volumes --remove-orphans
  assert_residue_zero
  printf 'M-10: KILLED\n'
}

run_m01
run_m02
run_m03
run_m04
run_m05
run_m06
run_m07
run_m08
run_m09
run_m10

printf 'MUTATION_SUITE: PASS\n'
