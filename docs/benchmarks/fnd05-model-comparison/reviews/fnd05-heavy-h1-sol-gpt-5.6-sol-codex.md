# FND-05 Sol Heavy Final Review

REVIEWER_IDENTITY:

```yaml
MODEL: GPT-5.6 Sol
HARNESS: Codex
EFFORT: xHigh
CONTEXT: Fresh Context
ROLE: architecture_and_contract_final_gate
SLOT: H1
PROMPT_REVISION: fnd05-heavy-sol-v2
```

TARGET_VERIFICATION:

```yaml
PR: 153
BASE_SHA: ee8abbb15758c1a2cfb624791482b755be578da2
FINAL_HEAD_SHA: 59aa87f9c6c4c581a56257caef738318e8d09ec3
TARGET_BRANCH: agent/issue-43-fnd-05-final-code
PR_STATE: OPEN
PR_DRAFT: true
PR_MERGED: false
HEAD_MATCH: YES
OUTPUT_BASE: 6cde564abe3e16581ecd264934ad02fef251ecef
LIGHT_FIX_DELTA:
  COMMITS: 1
  PRODUCT_RUNTIME_CHANGED: NO
  FILES:
    - tests/fnd05/static-gate.sh
    - tests/fnd05/verify-compose.sh
    - tests/fnd05/verify-mutations.sh
```

ENTRY_CONDITIONS:

```yaml
RESULT: PASS
RUN_JSON_AT_LIGHT_FIX_LOCK:
  SHA256: 34f066db8eebf66267c8bd3e32e2592688b3b0d4001d1b0fefe8c50ea9002001
  MATCH: YES
LIGHT_FIX:
  STATUS: locked
  TARGET_HEAD_SHA: 59aa87f9c6c4c581a56257caef738318e8d09ec3
  ARTIFACT_SHA256: 927deabfc98708219896815f856f8b61a7d60551aeb201640e67a2f3c73df17b
  ARTIFACT_SHA_MATCH: YES
STATIC_GATE: PASS
L1: LOCKED
L2: LOCKED
LIGHT_FINDINGS_DISPOSITION: COMPLETE
REQUIRED_MUTATION_BASELINE: AVAILABLE
DIRECT_HEAD_CI:
  BUILD_AND_TEST:
    RUN: 31505330867
    EVENT: push
    STATUS: completed
    CONCLUSION: success
    ACTUAL_CHECKOUT_SHA: 59aa87f9c6c4c581a56257caef738318e8d09ec3
    MATCH: YES
  FND05_COMPOSE:
    RUN: 31505330990
    EVENT: push
    STATUS: completed
    CONCLUSION: success
    ACTUAL_CHECKOUT_SHA: 59aa87f9c6c4c581a56257caef738318e8d09ec3
    MATCH: YES
```

VERDICT:

```yaml
APPROVE
```

BLOCKERS:

None.

MAJORS:

None.

ARCHITECTURE_ASSESSMENT:

The exact Final Head establishes the required observable architecture without responsibility inversion:

```text
PostgreSQL health/usable gate
  -> FND-04 one-shot Migrator execution
  -> successful process completion (exit 0)
  -> normal ASP.NET Core API start permission
```

`compose.yaml` gates the Migrator on PostgreSQL `service_healthy` and gates the API on Migrator `service_completed_successfully`. The production Migrator is the only runtime role that invokes `Database.MigrateAsync`; it returns non-zero on configuration, connection, migration, or bounded-timeout failure. The API has no migration call, no fallback provider, and no startup DDL. Therefore a Migrator non-zero result remains fail-closed and cannot be converted into API start permission by the production Compose path.

The runtime evidence independently observed Migrator exit/status, expected EF migration history, API state and start ordering, retained named-volume restart behavior, failure-path API never-start, clean-reset residue zero, missing-secret fail-closed behavior, and all M-01 through M-10 kills at the exact Final Head.

D-01 through D-08 conform:

- D-01: only the locked Compose features are required and used.
- D-02: PostgreSQL 18.4, .NET SDK 10, and ASP.NET runtime 10 use the locked digest-qualified `linux/amd64` identities.
- D-03: host environment to Compose secret to mounted file to explicitly granted service is preserved.
- D-04: validate, start, retaining stop/restart, and volume-removing clean reset are documented with the locked project identity and commands.
- D-05: tests use Compose JSON, inspect, volume inspection, logs, timestamps, labels, and migration history rather than command exit alone.
- D-06: source mutations are isolated in detached temporary worktrees where required, use project isolation, restore GREEN, and leave zero Docker residue.
- D-07: required execution is Bash/jq on Ubuntu 24.04 x64 with Linux `amd64` containers; the shell assets are consumed by Bash and the direct-head workflow uses the locked platform.
- D-08: Final Synthesis identity remains GPT-5.6 Terra / Codex / xHigh / Fresh Context; H1 is a distinct Sol reviewer and does not modify the product target.

ADR_CONFORMANCE:

- ADR-0001: the implementation retains one long-lived ASP.NET Core application service with PostgreSQL 18 under Docker Compose v2. The one-shot Migrator is the explicit operational role required by ADR-0009, not an additional long-lived application architecture.
- ADR-0008: PostgreSQL data uses a named volume; technical logs remain JSON console logs; the FND-05 path does not add a logging service or expose the database secret. Audit, backup/restore, and health behavior are not prematurely implemented.
- ADR-0009: EF Core migration source remains authoritative; the explicit one-shot Migrator runs before the API; migration timeout/failure is non-zero; and the normal API startup path performs no automatic schema evolution.

RESPONSIBILITY_BOUNDARIES:

- PostgreSQL owns database availability and durable named-volume state.
- The FND-04 Migrator owns pending migration discovery, bounded schema application, migration history, and success/failure exit status.
- Compose owns orchestration and converts only Migrator success into API start permission.
- The normal API owns application serving and persistence use, but not schema evolution.
- Failure is not swallowed: the Migrator emits a technical failure marker and returns non-zero; Compose withholds API start.

ISSUE_43_ESSENTIAL_BEHAVIOR:

The essential behaviors are satisfied: PostgreSQL and API share the Compose project; the explicit Migrator is connected; clean databases migrate before API start; migration failure prevents API start; migration omission is detected by runtime history evidence; API startup does not migrate; PostgreSQL uses a named volume; approved-major images are digest pinned; secrets are externally injected and absent from argv/repository/rendered configuration/log evidence; and start/stop/restart/clean-reset paths are reproducible.

SCOPE_BOUNDARY:

The Final Head does not add a health endpoint, business endpoint, business schema/data, backup/restore implementation, monitoring service, production deployment, scheduler, Kubernetes, HA, or zero-downtime machinery. FND-04 migration machinery, FND-05 Compose ordering, and future FND-06 health contracts remain separated.

DESIGN_LEVEL_SECURITY:

The database password flows from `MBS_DATABASE_PASSWORD` through the Compose top-level secret to `/run/secrets/database_password` only for PostgreSQL, Migrator, and API. PostgreSQL uses its file reader; the API and Migrator wrapper validate non-secret connection fields, require a readable non-empty secret file, construct `ConnectionStrings__Database` inside the container process environment, clear the temporary shell variable, and `exec` the target without placing the secret in argv. Direct-head evidence found no sentinel in rendered config, logs, container inspect, or process arguments, and the missing-secret probe failed before services became serving.

REJECTED_UNRESOLVED_LIGHT_RECHECK:

```yaml
REJECTED_OR_UNRESOLVED_BLOCKER_MAJOR_CANDIDATES: []
ESCALATED_BLOCKER_MAJOR_CANDIDATES: []
EVIDENCE_INCOMPLETE_FINDINGS: []
RESOLVED_LIGHT_FINDINGS_REOPENED: []
```

The resolved Light findings `L2-D06-M05` and `L2-D03-SECRET-MISSING` were consumed as locked verified evidence and were not re-reviewed as new findings. Independent architecture review found no remaining Blocker/Major root cause corresponding to them.

MERGE_READY:

```yaml
YES
```

This is an architecture/contract merge-readiness decision only. It is not merge authorization and no merge operation was performed.

LIGHT_GATE_ESCAPES:

```yaml
- ID: LGE-H1-01
  CONCERN: PR #153 body is stale and still states that product implementation has not started.
  CLASSIFICATION: process/documentation only
  ARCHITECTURE_DEFECT: NO
  BLOCKING_HEAVY_FINDING: NO
  RECOMMENDED_DISPOSITION: Refresh the PR body before merge with the exact Final Head, current implementation/evidence, unverified items, and known risks required by AGENTS.md.
```

No broader Light-quality audit was performed.

NON_BLOCKING_HEAVY_CONCERNS_MAX_3:

1. `LGE-H1-01`: refresh the stale PR #153 description before merge. This does not change the H1 architecture/contract verdict.

UNVERIFIED:

None within H1 architecture/contract scope. The reviewer did not substitute PR merge-ref CI or a different Head; exact push-run code, logs, API status, and checkout SHA were inspected.

REVIEW_BUDGET:

```yaml
full review used: 1 / 1
```

ARTIFACT_LOCK:

This Commit A is the producer commit. The exact Git blob SHA-256 and producer commit are recorded by the following `run.json`-only lock commit.
