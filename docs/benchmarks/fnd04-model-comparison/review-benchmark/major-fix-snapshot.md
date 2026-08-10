# FND-04 Final Synthesis — G-01 Major Fix Snapshot

Status: **LOCKED / READY FOR TARGETED MAJOR-FIX RE-REVIEW**

```yaml
BENCHMARK_ID: fnd04-final-synthesis-independent-review
RUN_ID: fnd04-final-review-20260810
GOLD_REVISION: fnd04-final-gold-v1
FIX_PROMPT_REVISION: fnd04-final-major-fix-v1
SNAPSHOT_REVISION: fnd04-final-major-fix-snapshot-v1
TARGET_ISSUE: 42
TARGET_PR: 140
BASE_SHA: 38c07e210fe4e8689f1d8aeabbb07b92610d1826
OLD_HEAD_SHA: 99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
NEW_HEAD_SHA: 3511688401533f60bb77c7dcc647c4c2c4aa84c6
PR_MERGE_REF_SHA: 2e69049bd8b38e57cd4fee2c42e17edaeaf23df1
LOCKED_AT: 2026-08-10T14:59:00+09:00
```

## 1. Target state

PR #140 was independently re-fetched after the targeted fix.

- state: OPEN / DRAFT / UNMERGED
- base branch: `main`
- base SHA: `38c07e210fe4e8689f1d8aeabbb07b92610d1826`
- head branch: `agent/issue-42-fnd-04-final-code`
- new Head: `3511688401533f60bb77c7dcc647c4c2c4aa84c6`
- merge ref: `2e69049bd8b38e57cd4fee2c42e17edaeaf23df1`

## 2. Exact fix delta

GitHub compare `99cee438...` -> `351168840...`:

```text
commits: 1
files:   1
+18 / -0
```

Only changed path:

```text
tests/MinimalBankSystem.IntegrationTests/Persistence/DesignTimeConnectionSafetyTests.cs
```

Commit:

```text
3511688401533f60bb77c7dcc647c4c2c4aa84c6
test: strengthen design-time connection safety regression
```

No production source, migration, benchmark candidate, Compose, health, or timeout architecture changed.

## 3. G-01 fix content

The old test accepted any nonzero exit plus absence of a fixed forbidden-destination string list.

The new Head preserves those checks but adds positive assertions that require the failure output to contain:

- `The ConnectionString property has not been initialized.`
- `database '' on server ''`
- `Npgsql`
- `Microsoft.EntityFrameworkCore.Migrations`

Therefore an unrelated tool/build failure no longer satisfies the intended failure signature, and an off-blocklist fabricated destination no longer satisfies the empty-destination assertion.

This is the fix direction required by Gold `G-01 / NR-01`.

## 4. Author mutation sensitivity evidence

Author-reported pre-commit verification:

```text
baseline                         PASS
M1 off-blocklist destination     targeted test FAILED -> sensitivity PASS
M2 factory-unreachable failure   targeted test FAILED -> sensitivity PASS
recovery baseline                PASS
mutation residue                 NONE
```

This author evidence is not yet the final independent clearance. Targeted re-review must independently verify the G-01 sensitivity.

## 5. Local verification metadata

Author report:

- tool restore: PASS
- restore: PASS
- build: PASS / 0 warnings / 0 errors
- targeted baseline: PASS
- non-PostgreSQL: Unit 4 + Integration 38 PASS
- real PostgreSQL: 23 PASS including timeout test
- pending-model: PASS
- `git diff --check`: PASS
- worktree clean

Duration:

```text
STARTED_AT_LOCAL:  2026-08-10 14:28
FINISHED_AT_LOCAL: 2026-08-10 14:58
DURATION_MINUTES:  30
```

Duration is explicit author metadata, not inferred from GitHub timestamps.

## 6. CI — independently verified

### Direct-head CI

```text
Run:       31360093004
Event:     push
Checkout:  3511688401533f60bb77c7dcc647c4c2c4aa84c6
Conclusion: SUCCESS
```

Observed log:

- build: 0 warnings / 0 errors
- pending-model: PASS
- non-PostgreSQL: Unit 4 + Integration 38 = 42 PASS
- real PostgreSQL: 23 PASS

### PR merge-ref CI

```text
Run:       31360094852
Event:     pull_request
Checkout:  2e69049bd8b38e57cd4fee2c42e17edaeaf23df1
Merge:     3511688401533f60bb77c7dcc647c4c2c4aa84c6 into 38c07e210fe4e8689f1d8aeabbb07b92610d1826
Conclusion: SUCCESS
```

Observed log:

- build: 0 warnings / 0 errors
- pending-model: PASS
- non-PostgreSQL: 42 PASS
- real PostgreSQL: 23 PASS

## 7. Coordinator pre-re-review assessment

```text
Target identity:                    PASS
Old -> new delta test-only:         PASS
Production code unchanged:          PASS
Gold fix direction present:         PASS
Direct-head CI:                     SUCCESS
PR merge-ref CI:                    SUCCESS
Author M1/M2 sensitivity:           REPORTED PASS
Independent G-01 clearance:         NOT YET COMPLETE
```

Decision: **READY FOR TARGETED MAJOR-FIX RE-REVIEW**.

This snapshot does not itself clear G-01. Re-review must independently establish that the corrected test turns red for the two false-assurance mutations and remains green on the production baseline.
