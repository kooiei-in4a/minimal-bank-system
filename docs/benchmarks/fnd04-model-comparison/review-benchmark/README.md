# FND-04 Final Synthesis Independent Review Benchmark

Status: **G-01 MAJOR FIX SNAPSHOT LOCKED / TARGETED RE-REVIEW NEXT**

```yaml
BENCHMARK_ID: fnd04-final-synthesis-independent-review
RUN_ID: fnd04-final-review-20260810
TARGET_PR: 140
BASE_SHA: 38c07e210fe4e8689f1d8aeabbb07b92610d1826
OLD_HEAD_SHA: 99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
NEW_HEAD_SHA: 3511688401533f60bb77c7dcc647c4c2c4aa84c6
GOLD_REVISION: fnd04-final-gold-v1
MAJOR_FIX_SNAPSHOT: fnd04-final-major-fix-snapshot-v1
RE_REVIEW_PROMPT: fnd04-final-major-fix-rereview-v1
```

## Completed adjudication

Role-diverse raw review 5/5、Judge A/B、Gold / Referenceは完了済み。

Gold verdict for old Head:

```text
CHANGES_REQUIRED
Merge-ready: NO
Confirmed Major: G-01 / NR-01
```

G-01はproduction defectではなく、`DesignTimeConnectionSafetyTests`がguard対象のoff-blocklist fabricated destinationやfactory未到達failureでもgreenになるfalse assuranceだった。

## G-01 targeted fix — IMPLEMENTED / SNAPSHOT LOCKED

New Head:

```text
3511688401533f60bb77c7dcc647c4c2c4aa84c6
```

GitHub compare old -> new:

```text
1 commit
1 modified file
+18 / -0
```

Only changed path:

```text
tests/MinimalBankSystem.IntegrationTests/Persistence/DesignTimeConnectionSafetyTests.cs
```

The fix adds positive evidence requiring:

- uninitialized ConnectionString failure
- empty database / server destination
- Npgsql path
- EF Migrations path

The fixed destination blocklist remains supplementary.

Canonical fix snapshot:

- `major-fix-snapshot.md`
- `major-fix-snapshot.json`
- Revision: `fnd04-final-major-fix-snapshot-v1`

## Mutation sensitivity — author evidence

```text
baseline                         PASS
M1 off-blocklist destination     test FAILED as expected
M2 factory-unreachable failure   test FAILED as expected
recovery                         PASS
mutation residue                 NONE
```

Targeted re-review must reproduce this independently before G-01 is cleared.

## CI — independently verified

### Direct Head

```text
Run 31360093004
checkout 3511688401533f60bb77c7dcc647c4c2c4aa84c6
SUCCESS
```

- build 0 warnings / 0 errors
- pending-model PASS
- non-PostgreSQL 42 PASS
- real PostgreSQL 23 PASS

### PR merge ref

```text
Run 31360094852
checkout 2e69049bd8b38e57cd4fee2c42e17edaeaf23df1
Merge 3511688401533f60bb77c7dcc647c4c2c4aa84c6
  into 38c07e210fe4e8689f1d8aeabbb07b92610d1826
SUCCESS
```

- build 0 warnings / 0 errors
- pending-model PASS
- non-PostgreSQL 42 PASS
- real PostgreSQL 23 PASS

## Targeted Major-fix re-review — NEXT

Canonical prompt:

```text
../prompts/final-synthesis-major-fix-re-review.md
Revision: fnd04-final-major-fix-rereview-v1
```

Reviewer set:

| Slot | Model / Harness | Purpose |
|---|---|---|
| T1 | GPT-5.6 Sol / Codex / xHigh | deep independent mutation-sensitivity verification |
| T2 | Cursor Auto / Cursor | practical independent targeted verification |

Completion condition:

```text
T1 = G01_FIXED
T2 = G01_FIXED
new Blocker = 0
new Major = 0
```

If both clear G-01, proceed to Formal Agent B product merge gate. The original 5-review benchmark is not rerun.

## Current flow

```text
5/5 role-diverse review             COMPLETE
Judge A/B                           COMPLETE
Gold / Reference                    LOCKED
G-01 targeted fix                   COMPLETE / SNAPSHOT LOCKED
Targeted re-review                  READY / 0 OF 2
Formal Agent B                      BLOCKED UNTIL G-01 CLEARANCE
```

PR #140 remains Draft / unmerged. Ready, merge, and Issue #42 close remain prohibited.
