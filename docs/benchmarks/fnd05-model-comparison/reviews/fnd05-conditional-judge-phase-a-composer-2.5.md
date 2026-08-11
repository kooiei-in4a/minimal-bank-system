# FND-05 Conditional Judge — Phase A Independent Reference

## JUDGE_IDENTITY

```yaml
MODEL: Composer 2.5
HARNESS: Cursor
EFFORT: default
CONTEXT: Fresh Context
ROLE: Conditional Heavy Review Judge
PROMPT_REVISION: fnd05-conditional-judge-v2
```

## TARGET_VERIFICATION

```yaml
REPOSITORY: kooiei-in4a/minimal-bank-system
TARGET_ISSUE: 43
TARGET_PR: 153
BASE_SHA: ee8abbb15758c1a2cfb624791482b755be578da2
FINAL_HEAD_SHA: 59aa87f9c6c4c581a56257caef738318e8d09ec3
TARGET_BRANCH: agent/issue-43-fnd-05-final-code
TRIGGER_LOCK_COMMIT: 453b319b278b0df145cb3493d634dfc8478bdd68
RUN_REGISTRY_SHA256: 06bd4d8e7a80b0d7939edd7ff2db88aa7a62dc6fe03aa4c133195b427f212e54
ENTRY_CONDITIONS: PASS
```

Trigger lock `run.json` blob SHA256 was recomputed from exact Git object at `453b319b278b0df145cb3493d634dfc8478bdd68` and matched the expected value.

## AUTHORITIES_READ

- GitHub Issue #43 (scope, AC, verification requirements)
- `AGENTS.md`
- Accepted ADR-0001 (Docker Compose v2 execution baseline)
- Accepted ADR-0008 (audit/logging — referenced for authority chain)
- Accepted ADR-0009 (explicit migrator, no API auto-migration, fail deployment on migration failure)
- `docs/benchmarks/fnd05-model-comparison/reference/mandatory-mutations.md` (revision `fnd05-mutations-v2`)
- `docs/benchmarks/fnd05-model-comparison/reference/mutation-determinism-contract.md` (revision `fnd05-mutation-determinism-v1`)
- `docs/benchmarks/fnd05-model-comparison/reference/pre-run-decision-locks.md` (D-04, D-05, D-06 rows for M-02 / M-06)
- FINAL_HEAD artifacts:
  - `tests/fnd05/verify-mutations.sh`
  - `tests/fnd05/static-gate.sh`
  - `tests/fnd05/verify-compose.sh`
  - `compose.yaml`
- PR #153 direct-head CI (run `31505335667`): `fnd05-compose` SUCCESS, `fnd05-mutations` SUCCESS at FINAL_HEAD

## M02_REFERENCE

```yaml
M02_REFERENCE:
  VALID_KILL: NO
  CONTRACT_VIOLATION: YES
  SEVERITY_IF_INVALID: Major
  MERGE_BLOCKING: YES
  ROOT_CAUSE: >
    M-02 `failure_oracle` treats any Migrator exit code 0 as
    `migrator-nonzero-masked` without first establishing that an intended real
    Migrator failure was reached. The intended-failure log marker check is only
    reachable when exit code is already non-zero, so it is dead code for the
    masked-failure path that M-02 is designed to test. D-06 precondition
    "intended real Migrator failure reached" is therefore not machine-verified
    before counting a kill.
  REQUIRED_FIX: >
    Before asserting masking detection, machine-readably confirm intended real
    Migrator failure was reached (e.g., failure marker and/or masked-nonzero
    probe marker in Migrator logs). Only after that precondition passes, assert
    observed exit 0 against expected non-zero and/or API startability. The
    failure signature must change when the intended failure path is not reached
    while masking wrapper remains.
```

### M02 checklist

| Check | Result | Notes |
| --- | --- | --- |
| M02_BASELINE_GREEN | YES | `run_m02` calls `baseline()` before mutation; CI `fnd05-mutations` GREEN at FINAL_HEAD |
| M02_PRECONDITION_DEFINED | YES | D-06 / mandatory-mutations require intended real Migrator failure reached |
| M02_PRECONDITION_ACTUALLY_OBSERVED | NO | Oracle never asserts failure marker or `FND05_M02_MASKED_NONZERO` before kill signature |
| M02_INTENDED_REAL_MIGRATOR_FAILURE_REACHED | UNVERIFIED by oracle | Injection uses `POSTGRES_PORT: "1"` but oracle does not observe it |
| M02_MUTATION_APPLIED | YES | Override applies exit-0 wrapper and wrong port |
| M02_TARGET_ORACLE_EXECUTED | PARTIAL | `failure_oracle` runs via `expect_red`, but only checks exit 0 first |
| M02_EXPECTED_RED_OBSERVED | YES (CI) | Suite reports KILLED; signature `migrator-nonzero-masked` emitted |
| M02_EXPECTED_FAILURE_SIGNATURE | `migrator-nonzero-masked` | Matches D-06 exit-code mismatch class only superficially |
| M02_SIGNATURE_DISCRIMINATES_INTENDED_PATH | NO | Probe shows identical signature with and without intended failure |
| M02_INVALID_FAILURE_EXCLUDED | NO | Natural Migrator success (exit 0, no failure marker) yields same signature |
| M02_RESTORED_GREEN | YES | `clean_current` + override removal after `expect_red` |
| M02_RESIDUE_ZERO | YES | `clean_current` + trap `cleanup_all` |

## M02_EVIDENCE

Source inspection of FINAL_HEAD `run_m02()`:

- Applies `POSTGRES_PORT: "1"` plus bash wrapper converting non-zero Migrator exit to 0.
- Calls `expect_red migrator-nonzero-masked failure_oracle`.
- `failure_oracle` logic order:
  1. If `exit_code migrator == 0` → emit `ORACLE_SIGNATURE=migrator-nonzero-masked` and RED.
  2. Else if API state != `created` → `api-was-started`.
  3. Else if Migrator logs lack `Migration failed. The deployment must not continue.` → `intended-failure-marker-absent`.

With masking active, step 1 always fires; steps 2–3 are unreachable. Therefore intended-failure reachability is never verified for the kill path.

Independent probe (isolated worktree at FINAL_HEAD, WSL/Docker, not committed):

| Case | migrator_exit | oracle_sig | has_failure_marker | has_masked_marker |
| --- | --- | --- | --- | --- |
| mask_only (wrapper, no port break) | 0 | migrator-nonzero-masked | 0 | 0 |
| mask_and_fail (wrapper + POSTGRES_PORT=1) | 0 | migrator-nonzero-masked | 1 | 1 |

Both cases produce the same kill signature despite only the second reaching intended real Migrator failure.

Contract basis: `mandatory-mutations.md` §3 (intended migration failure required), §13 (`PRECONDITION_RESULT != PASS` is not a valid kill; mandatory mutation invalid kill is merge-blocking Major); `mutation-determinism-contract.md` §3, §9; `pre-run-decision-locks.md` D-06 M-02 row.

## M02_PROBES

```text
PROBE_ID: M02-DISC-01
PRECONDITION: isolated git worktree at 59aa87f9c6c4c581a56257caef738318e8d09ec3, Docker available
CONTROLLED_CHANGE: (A) exit-0 masking wrapper only; (B) same wrapper + POSTGRES_PORT=1
OBSERVED: both cases exit 0; both emit ORACLE_SIGNATURE=migrator-nonzero-masked
EXPECTED_IF_DISCRIMINATING: case A should not emit masking kill signature (no real failure to mask)
CLEANUP: compose down --volumes; worktree retained for session only
RESIDUE: 0
```

## M06_REFERENCE

```yaml
M06_REFERENCE:
  VALID_KILL: NO
  CONTRACT_VIOLATION: YES
  SEVERITY_IF_INVALID: Major
  MERGE_BLOCKING: YES
  ROOT_CAUSE: >
    `run_m06` only verifies that a Compose override removed `postgres_data` from
    rendered config (inline jq self-check). It never invokes the shipped product
    oracle (`static-gate.sh` volume-policy jq assertion) or any lifecycle test.
    No baseline GREEN, expected product-oracle RED, restore GREEN, or residue
  checks are performed. The kill is mutation-self-confirmation, not oracle detection.
  REQUIRED_FIX: >
    Run baseline GREEN (static gate and/or lifecycle oracle), apply named-volume
    violation mutation, invoke shipped volume-policy / lifecycle oracle and observe
    expected RED with discriminating signature, then restore GREEN and confirm
    residue 0.
```

### M06 checklist

| Check | Result | Notes |
| --- | --- | --- |
| M06_BASELINE_GREEN | NO | `run_m06` does not run baseline or static gate before mutation |
| M06_PRECONDITION_DEFINED | YES | D-06: named PostgreSQL volume exists in resolved config |
| M06_PRECONDITION_ACTUALLY_OBSERVED | PARTIAL | Implicit via production `compose.yaml`; not asserted in `run_m06` |
| M06_MUTATION_APPLIED | YES (self-checked) | Inline jq confirms `postgres_data` absent after override |
| M06_TARGET_ORACLE_EXECUTED | NO | `static-gate.sh` never called; no lifecycle oracle |
| M06_EXPECTED_RED_OBSERVED | NO | No product/test oracle RED |
| M06_EXPECTED_FAILURE_SIGNATURE | N/A (not observed) | D-06 expects resolved-config / runtime volume oracle RED |
| M06_SIGNATURE_MATCHED | NO | Only inline mutation-application check |
| M06_INVALID_FAILURE_EXCLUDED | NO | Self-confirming config render is not invalid-failure exclusion |
| M06_RESTORED_GREEN | NO | No restore step |
| M06_RESIDUE_ZERO | NO | No compose project cleanup in `run_m06` |

Mutation detection separation:

- **Mutation applied (self-check):** YES — inline jq in `run_m06`.
- **Product/test oracle detected mutation:** NO — `static-gate.sh` includes `postgres_data` named-volume assertion but is not invoked by M-06.

## M06_EVIDENCE

Source inspection of FINAL_HEAD `run_m06()`:

- Creates override replacing named volume with anonymous bind `/var/lib/postgresql`.
- Renders config; inline jq checks `postgres_data` absent → prints `M-06: KILLED`.
- No `expect_red`, no `static-gate.sh`, no `compose up`, no `clean_current`, no `assert_residue_zero`.

Contrast with M-05 in same file: baseline static gate GREEN → mutate → `expect_red` via `static-gate.sh` → restore GREEN → residue 0.

`static-gate.sh` ships the product volume-policy oracle:

```jq
any(.services.postgres.volumes[]; .type == "volume" and .source == "postgres_data" and .target == "/var/lib/postgresql")
```

Independent probe (isolated worktree, not committed):

- Inline verifier (same logic as `run_m06`): reports KILLED (self-check only).
- `static-gate.sh` against mutated `compose.yaml`: exit 1 (RED) — product oracle would detect violation but is not wired into M-06.

Contract basis: `mandatory-mutations.md` §7, §13; `mutation-determinism-contract.md` §3, §9; `pre-run-decision-locks.md` D-06 M-06 row; Issue #43 AC "PostgreSQL dataはnamed volumeを使用する".

## M06_PROBES

```text
PROBE_ID: M06-ORACLE-01
PRECONDITION: detached worktree at FINAL_HEAD; MBS_DATABASE_PASSWORD set
CONTROLLED_CHANGE: sed replace named volume with anonymous mount in compose.yaml
OBSERVED: inline verifier KILLED; static-gate.sh exit 1 (RED)
EXPECTED: product oracle RED if invoked; M-06 harness does not invoke it
CLEANUP: worktree removed
RESIDUE: 0
```

## PHASE_A_MERGE_READY

```yaml
PHASE_A_MERGE_READY: NO
```

Both disputed mandatory mutations fail independent validity criteria. M-02 does not discriminate intended failure path from natural success under masking. M-06 does not execute product oracle RED. Per `mandatory-mutations.md` §13, invalid mandatory mutation kills are merge-blocking Major.

## UNVERIFIED

- Full end-to-end re-run of `verify-mutations.sh` locally (CI SUCCESS at FINAL_HEAD used as baseline/mutation-suite evidence).
- Runtime volume identity inspection (`docker volume inspect`) under M-06 mutation (config-level evidence sufficient for Phase A).

## H1_READ

NO

## H2_READ

NO
