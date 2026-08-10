# FND-04 Final Synthesis Snapshot

Status: **LOCKED / READY FOR ROLE-DIVERSE INDEPENDENT REVIEW**

```yaml
SNAPSHOT_REVISION: fnd04-final-synthesis-snapshot-v1
TARGET_ISSUE: 42
TARGET_PR: 140
BASE_SHA: 38c07e210fe4e8689f1d8aeabbb07b92610d1826
HEAD_SHA: 99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
PR_MERGE_REF_SHA: d12de2ae07003a10d19d576808cf88ec7796da23
SELECTION_REVISION: fnd04-selection-adjudication-v1
IMPLEMENTATION_PROMPT_REVISION: fnd04-final-synthesis-v1
LOCKED_AT: 2026-08-10T12:02:00+09:00
```

この文書は、Final Synthesis authorの自己申告だけでなく、GitHub上のPR metadata、実diff、production code、test code、workflow run / job / logをcoordinatorが再取得した上で固定したreview-input snapshotである。

このlockは**merge-ready判定ではない**。次工程のrole-diverse independent reviewへ投入するtarget identityを固定する。

## 1. Target identity

- Repository: `kooiei-in4a/minimal-bank-system`
- Issue: #42 `[FND-04] EF Core・明示的migration実行基盤を確立する`
- PR: #140 `[FND-04] EF Core・明示的migration実行基盤 — Final Synthesis`
- PR state at lock: OPEN / DRAFT / UNMERGED / mergeable
- Branch: `agent/issue-42-fnd-04-final-code`
- Base: `main`
- Base SHA: `38c07e210fe4e8689f1d8aeabbb07b92610d1826`
- Head SHA: `99cee4386ea049ad84e9c087c6fdf1e25cc20f3e`
- Commits: 1
- Changed files: 25
- Diff size: `+1149 / -1`

Changed files are limited to FND-04 product / test / CI / documentation scope. No `docs/benchmarks/` candidate artifact is changed by PR #140.

## 2. Duration metadata

Author-recorded execution metadata:

```text
STARTED_AT_LOCAL:  2026-08-10 11:25
FINISHED_AT_LOCAL: 2026-08-10 11:54
DURATION_MINUTES:  29
```

This is explicit Agent execution metadata. It is not inferred from GitHub timestamps and is not retroactively applied to H0/H1 candidate speed scoring.

## 3. Coordinator verification of locked selection

### C5 primary — VERIFIED PRESENT

The Final Synthesis implements the selected C5-style architecture:

- `BankDbContext`, Npgsql provider configuration, migrations, snapshot and design-time factory are owned by Infrastructure.
- API / Migrator / design-time use Npgsql and Infrastructure migrations assembly.
- `MinimalBankSystem.Migrator` is a dedicated one-shot executable.
- `InitialFoundation` has empty `Up` / `Down` operations.
- normal API startup does not call migration / `EnsureCreated` paths.
- real PostgreSQL integration tests exercise production Migrator process behavior.

### C1 secret non-disclosure — VERIFIED PRESENT

`MigrationBaselineTests.MigratorExitsNonZeroWhenCredentialsAreRejectedWithoutDisclosingThePassword` injects a sentinel password into a rejected credential connection and asserts:

- exit code is non-zero;
- sentinel is absent from stdout;
- sentinel is absent from stderr.

### C8-M01 mandatory regression — VERIFIED PRESENT

The Final Synthesis does not fabricate the rejected C8 destination (`127.0.0.1` / `design_time`).

`BankDbContextFactory` uses Npgsql model-only configuration when `ConnectionStrings__Database` is absent and does not create a fake destination.

`DesignTimeConnectionSafetyTests` launches repository-local `dotnet-ef database update --no-build` in a child process, removes `ConnectionStrings__Database` only from the child, requires a non-zero result, and asserts that fabricated/fake destination markers are absent from the output.

### C6 TimeProvider seam — NON-SELECTION PRESERVED

No C6-style production `TimeProvider` timeout seam was added to the Migrator. The Final Synthesis retains the direct real-PostgreSQL lock test that exercises the production 60-second budget.

## 4. Verification evidence inspected

### Package / tool / migration identity

- repository-local `dotnet-ef` manifest pins `10.0.10` with roll-forward disabled;
- EF Core / Design = `10.0.10`;
- Npgsql / Npgsql EF provider = `10.0.3`;
- migration assembly is Infrastructure;
- migration name is `InitialFoundation`;
- migration history uses `public.__EFMigrationsHistory`;
- baseline model contains no entity type and migration `Up` / `Down` operation lists are empty.

### Runtime / failure path

Production Migrator:

- resolves canonical `ConnectionStrings:Database` configuration;
- missing connection is an explicit failure;
- Npgsql command timeout is 60 seconds;
- whole migration cancellation budget is 60 seconds;
- success returns 0;
- general failure returns 1;
- timeout returns 2.

Real PostgreSQL tests include:

- clean apply;
- rerun / unchanged history;
- missing connection failure;
- unreachable server failure;
- rejected credentials + password non-disclosure;
- malformed migration history failure;
- real PostgreSQL blocking path causing production timeout;
- API startup no schema mutation;
- API real `BankDbContext` resolve with no schema mutation.

### Model / SQL evidence

Tests and CI include the actual EF pending-model mechanism. Model tests also exercise idempotent SQL generation and verify the empty baseline boundary.

The temporary model-drift negative probe was reported as local-only and was not committed. Its raw local execution is author evidence and remains an independent-review verification target rather than a GitHub-hosted artifact.

## 5. CI identity and result

Associated workflow run:

```yaml
RUN_ID: 31350916189
WORKFLOW: Build and Test
RUN_NUMBER: 427
STATUS: completed
CONCLUSION: success
JOB: build-test
```

Verified successful steps:

- Restore
- Restore local tools
- Build
- Verify no pending EF model changes
- Test (non-PostgreSQL)
- Test (real PostgreSQL)

CI log evidence:

- Build: warnings 0 / errors 0
- non-PostgreSQL: Unit 4 / 4 + Integration 38 / 38
- real PostgreSQL: 23 / 23
- pending-model command: `No changes have been made to the model since the last migration.`

### CI identity nuance

The run is associated with PR Head `99cee4386ea049ad84e9c087c6fdf1e25cc20f3e`, but the `pull_request` job checkout log shows GitHub's PR merge ref:

```text
PR merge ref SHA: d12de2ae07003a10d19d576808cf88ec7796da23
Merge 99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
  into 38c07e210fe4e8689f1d8aeabbb07b92610d1826
```

Therefore the canonical description for this run is **successful PR-merge-ref CI for the locked Head against the exact locked Base**, not a claim that the runner directly checked out the branch Head SHA itself.

A separate direct-head push-run was not independently resolved through the available connector during coordinator verification. This does not block entering independent review because the exact Base/Head pair is fixed and the PR merge result is the code state under merge evaluation, but reviewers must preserve this distinction when describing CI identity.

## 6. Scope check

Coordinator inspection found no Final Synthesis change to:

- benchmark candidate branches / artifacts;
- Docker Compose implementation;
- health contract implementation;
- business entity or business schema;
- FND-05 / FND-06 product scope.

## 7. Coordinator pre-review gate

```text
Target identity:                 PASS
Base / Head fixed:              PASS
Draft / unmerged boundary:      PASS
Selection application:          PASS
C8-M01 regression present:      PASS
Scope boundary:                 PASS
PR-merge-ref CI:                SUCCESS
Direct-head checkout CI:        NOT INDEPENDENTLY RESOLVED
Blocking defect found by gate:  NONE
```

**Decision: READY FOR ROLE-DIVERSE INDEPENDENT REVIEW.**

This is not an APPROVE / merge-ready verdict. Independent reviewers must re-derive findings from Issue #42, authority documents, exact diff, tests and CI rather than trusting this coordinator snapshot or the PR author's description.
