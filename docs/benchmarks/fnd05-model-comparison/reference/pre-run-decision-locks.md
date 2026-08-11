# FND-05 Pre-Run Decision Locks — D-01〜D-08

Revision: `fnd05-decisions-v1`

Status: **LOCKED FOR PRE-RUN / IMPLEMENTATION STILL PROHIBITED**

Target repository: `kooiei-in4a/minimal-bank-system`

These decisions lock common benchmark conditions before candidate execution. Product authority remains approved specification → Accepted ADR → Issue #43 → AGENTS.md. These locks do not authorize Issue Ready PASS or implementation.

## Evidence baseline

### Local primary evidence — 2026-08-11 JST

Canonical artifact:

`docs/benchmarks/fnd05-model-comparison/evidence/local-pre-lock-evidence-20260811.md`

Observed:

- OS: Ubuntu 24.04.4 LTS under WSL2, `linux/amd64`
- Docker Desktop 4.85.0 / Engine 29.6.2
- Docker Compose v5.3.1
- Buildx v0.35.0-desktop.2
- Bash 5.2.21
- jq 1.7
- `service_healthy`: PASS
- `service_completed_successfully`: PASS
- top-level `secrets.environment`: PASS
- `docker compose config --quiet`: PASS
- `docker compose ps --format json`: PASS
- project/service/volume Compose labels: machine-readable PASS
- probe residue: 0
- repository diff: none

### CI primary evidence

GitHub-hosted Ubuntu 24.04 image `20260720.247.2` includes:

- Docker Compose 2.38.2
- Docker Client / Server 28.0.4
- Buildx 0.35.0
- Bash 5.2.21
- jq 1.7

PR #145 reviewed Head `6c626451fc7d8059e468d19afbfc3c80b666acb9` passed Build and Test run `31443292973`.

---

## D-01 — Minimum Compose version / features

```yaml
status: LOCKED
minimum_compose_version: "2.38.2"
required_features:
  - depends_on.condition.service_healthy
  - depends_on.condition.service_completed_successfully
  - top_level_secrets_environment_source
  - docker_compose_config_quiet
  - docker_compose_config_format_json
  - docker_compose_ps_format_json
  - compose_project_labels
local_observed:
  compose: "5.3.1"
ci_observed:
  compose: "2.38.2"
```

Do not use Compose features newer than 2.38.2 as required candidate-common behavior unless D-01 is explicitly re-locked before candidate execution.

---

## D-02 — Exact image identities

The tag names are descriptive only. The digest-qualified references below are the immutable identities.

```yaml
status: LOCKED
platform: "linux/amd64"
postgres:
  tag: "postgres:18.4"
  reference: "postgres:18.4@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636"
  index_digest: "sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636"
dotnet_sdk:
  tag: "mcr.microsoft.com/dotnet/sdk:10.0-noble"
  reference: "mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0"
  index_digest: "sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0"
dotnet_aspnet:
  tag: "mcr.microsoft.com/dotnet/aspnet:10.0-noble"
  reference: "mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b"
  index_digest: "sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b"
```

The PostgreSQL tag produced a different older web-observed digest during preparation. The current direct-registry `docker buildx imagetools inspect postgres:18.4` measurement above is the lock source. Candidate and Final Synthesis use the digest-qualified reference and do not re-resolve the mutable tag as authority.

If any locked digest becomes unavailable, stop and re-lock D-02; do not silently substitute another digest.

---

## D-03 — Secret source / reader design

```yaml
status: LOCKED
source:
  type: "host environment -> Compose top-level secret"
  secret_source_key: "environment"
grant:
  rule: "explicit per-service grant only"
payload:
  canonical_secret: "database password"
postgres_reader:
  mechanism: "POSTGRES_PASSWORD_FILE mounted secret file"
api_migrator_reader:
  mechanism: "entrypoint/wrapper reads mounted secret file, constructs ConnectionStrings__Database from secret + non-secret connection parameters, exports it, then execs dotnet"
missing_secret:
  behavior: "fail closed before application/migrator work"
prohibited:
  - committed real credential
  - secret literal in compose file
  - secret value in command-line arguments
  - secret value in logs
  - secret value in rendered Compose config
```

The exact service names and file paths remain implementation choices unless another lock requires them.

A candidate may use an equivalent wrapper/reader implementation only if it preserves the same externally supplied secret, explicit grant, argv/log/render non-disclosure, and fail-closed properties.

---

## D-04 — Lifecycle commands / semantics

Canonical project identity:

```text
minimal-bank-system-fnd05
```

Canonical commands:

```bash
# validate
docker compose -p minimal-bank-system-fnd05 config --quiet

# render machine-readable config evidence
docker compose -p minimal-bank-system-fnd05 config --format json

# start / start-after-stop
docker compose -p minimal-bank-system-fnd05 up --build --detach --remove-orphans

# stop while retaining database data
docker compose -p minimal-bank-system-fnd05 down --remove-orphans

# restart with migration gate re-evaluation
docker compose -p minimal-bank-system-fnd05 down --remove-orphans
docker compose -p minimal-bank-system-fnd05 up --build --detach --remove-orphans

# clean reset
docker compose -p minimal-bank-system-fnd05 down --volumes --remove-orphans
```

Semantics:

- `down --remove-orphans` is the canonical stop because the next `up` recreates the execution path and re-evaluates the Migrator gate.
- `docker compose restart` is not the canonical benchmark restart because it is not used as evidence that the migration gate was re-evaluated.
- normal stop retains the named PostgreSQL volume.
- clean reset removes the project containers/networks and named volume, followed by D-05 absence verification.
- clean start means clean reset → start → D-05 verification.
- exit code 0 from a lifecycle command is not sufficient evidence by itself.

---

## D-05 — External state capture

Primary machine-readable evidence:

```text
docker compose -p minimal-bank-system-fnd05 ps -a --format json
docker inspect <container-id>
docker volume inspect <volume>
docker compose -p minimal-bank-system-fnd05 config --format json
docker compose -p minimal-bank-system-fnd05 logs --no-color --timestamps
```

Role-specific service names are resolved from the candidate implementation and recorded in the evidence; they are not pre-locked names.

Required observations:

```yaml
migrator:
  - container/process identity
  - State.Status
  - State.ExitCode
  - State.StartedAt
  - State.FinishedAt
api:
  - absent/created/running/exited distinction
  - State.Status
  - State.StartedAt
  - State.FinishedAt
ordering_success:
  - migrator ExitCode == 0
  - migration history contains expected migration
  - API StartedAt is not before Migrator FinishedAt
  - API is running
ordering_failure:
  - migrator ExitCode != 0
  - API has never started
  - started-then-exited is failure, not "never started"
project_identity:
  - com.docker.compose.project
  - com.docker.compose.service
volume_identity:
  - com.docker.compose.project
  - com.docker.compose.volume
```

Migration history is queried from the PostgreSQL runtime using the actual resolved DB service and:

```sql
SELECT "MigrationId"
FROM public."__EFMigrationsHistory"
ORDER BY "MigrationId";
```

`docker compose events --json` / Docker event JSON may be captured as corroborating ordering evidence, but does not replace the required final state and migration-history evidence.

---

## D-06 — Failure injection / mutation determinism

Status: `LOCKED`

Common isolation:

```yaml
project_isolation: "unique Compose project name per probe"
source_isolation: "detached temporary git worktree when source mutation is needed"
execution: "one mutation at a time"
production_branch_commit: false
cleanup: "revert/remove temporary override/worktree/project resources"
residue_required: 0
```

Candidate-visible contract includes mutation ID, protected property, deterministic precondition property, barrier/fixture class, injection-point class, and expected/invalid failure-signature classes. Exact evaluator patch/source edit remains evaluator-only and is not committed before candidate execution.

Locked mechanism classes:

| Mutation | Deterministic precondition / fixture | Injection class | Expected failure signature |
| --- | --- | --- | --- |
| M-01 | PostgreSQL usable; Migrator production path reached; controlled barrier holds Migrator incomplete | evaluator-only Compose override / wrapper weakens API start permission | API start observed while Migrator has not successfully completed |
| M-02 | intended real Migrator failure reached | evaluator-only wrapper/override masks non-zero as zero | exit-code contract mismatch and/or API becomes startable after intended failure |
| M-03 | isolated real PostgreSQL has a known pending migration state; baseline API startup leaves state unchanged | detached-worktree API startup auto-migration mutation | mutated API startup changes migration history/schema |
| M-04 | sentinel secret supplied through locked D-03 path | evaluator-only override expands sentinel into prohibited argv surface | sentinel found in prohibited process-argument observation |
| M-05 | locked digest-qualified config validates before mutation | temporary config/source mutation removes digest | static/resolved-image oracle detects tag-only identity |
| M-06 | named PostgreSQL volume exists in resolved config | temporary override replaces it with anonymous/bind contract violation | resolved config/runtime volume oracle detects non-named storage |
| M-07 | baseline negative path reaches intended component/reason | override breaks execution before intended path | expected path/reason marker missing; oracle rejects false non-zero |
| M-08 | expected migration state absent before run; real Migrator path reachable | detached-worktree runtime mutation returns exit 0 without applying migration | unchanged oracle sees exit 0 + missing expected migration state |
| M-09 | baseline success path proves API running | evaluator-only command/entrypoint override exits immediately after start | API is observed started then exited, not running |
| M-10 | targeted same-project container/network/volume/orphan exists before reset | temporary cleanup responsibility weakening | targeted same-project resource remains after clean reset |

For all mutations:

- no natural race or fixed-sleep coincidence is accepted as the precondition;
- unrelated build/YAML/CLI/image failures are invalid kills;
- baseline GREEN, expected RED, restore GREEN, and residue 0 are required.

---

## D-07 — Cross-platform execution contract

```yaml
status: LOCKED
required_container_platform: "linux/amd64"
required_ci:
  os: "GitHub-hosted Ubuntu 24.04 x64"
  minimum_compose: "2.38.2"
required_local_reference:
  os: "Ubuntu 24.04.4 LTS under WSL2"
  container_engine: "Docker Desktop Linux engine"
shell:
  required: "Bash >= 5.2"
json_tool:
  required: "jq >= 1.7"
paths: "POSIX/Linux paths"
line_endings:
  shell_assets: "LF"
unsupported_as_required_targets:
  - Windows containers
  - native PowerShell-only execution
  - macOS
  - linux/arm64
```

Local `core.autocrlf=true` is explicitly not treated as a script execution contract. Any shell asset must remain executable under Linux with LF line endings independent of the developer Git preference.

---

## D-08 — Final Synthesis identity

```yaml
status: LOCKED
model: "GPT-5.6 Terra"
harness: "Codex"
effort: "xHigh"
fresh_context_required: true
silent_substitution: false
```

Rationale:

- different model identity from all three implementation candidates;
- different model identity from the Sol and Opus Heavy reviewers;
- Codex provides the repository execution harness used elsewhere in the benchmark;
- preserves separation between candidate generation, Final Synthesis authorship, and final Heavy review.

Immediately before Final Synthesis, availability of this exact identity must be re-confirmed. If unavailable, stop and explicitly re-lock D-08 before execution; do not silently substitute a model.

---

## Lock result

```yaml
D_01: LOCKED
D_02: LOCKED
D_03: LOCKED
D_04: LOCKED
D_05: LOCKED
D_06: LOCKED
D_07: LOCKED
D_08: LOCKED
ISSUE_READY_PASS: false
KOO_START_AUTHORIZED: false
IMPLEMENTATION_PERMITTED: false
CANDIDATE_EXECUTION: NOT_STARTED
```
