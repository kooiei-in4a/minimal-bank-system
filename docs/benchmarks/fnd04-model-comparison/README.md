# FND-04 Model Comparison

Target Issue: #42 `[FND-04] EF Core・明示的migration実行基盤を確立する`

Status: **G-01 MAJOR FIX COMPLETE / TARGETED RE-REVIEW READY**

このdirectoryはFND-04 benchmarkのH0/SR/H1、Implementation Evaluation、Selection / Adjudication、Final Synthesis、独立review、Judge、Gold、Major fixを管理するbenchmark control正本である。

## Current state

```text
H0 implementation snapshot       8/8 LOCKED
Formal Self-Review               8/8 LOCKED
H1 self-review fix snapshot      8/8 LOCKED
H1 exact-head CI                 8/8 SUCCESS
Implementation Evaluation        COMPLETE / LOCKED
Selection / Adjudication         COMPLETE / LOCKED
Final Synthesis initial Head      COMPLETE / SNAPSHOT LOCKED
Role-diverse independent review  COMPLETE / 5 OF 5
Judge A / B                      COMPLETE / QUORUM MATCH
Gold / Reference                 LOCKED / G-01 MAJOR
G-01 targeted fix                COMPLETE / SNAPSHOT LOCKED
Targeted Major-fix re-review     READY / 0 OF 2
Formal Agent B                   BLOCKED UNTIL G-01 CLEARANCE
```

## Gold result on old Head

Old Head:

```text
99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
```

Judge A/B quorum:

```text
CHANGES_REQUIRED
Blocking root cause: NR-01
Merge-ready: NO
```

Canonical Gold:

- `review-benchmark/gold-review.md`
- `review-benchmark/gold-review.json`
- Revision: `fnd04-final-gold-v1`

Confirmed Major `G-01 / NR-01`: `DesignTimeConnectionSafetyTests` could stay green for an off-blocklist fabricated destination or a factory-unreachable unrelated failure.

## Targeted Major fix

New Head:

```text
3511688401533f60bb77c7dcc647c4c2c4aa84c6
```

Old -> New:

```text
1 commit
1 file
+18 / -0
```

Only changed file:

```text
tests/MinimalBankSystem.IntegrationTests/Persistence/DesignTimeConnectionSafetyTests.cs
```

Production code change: **NONE**.

New positive assertions require the intended failure signature:

- uninitialized ConnectionString
- empty database / server destination
- Npgsql path
- EF Migrations path

Author mutation verification:

```text
baseline PASS
M1 off-blocklist destination -> targeted test FAIL
M2 factory unreachable       -> targeted test FAIL
recovery PASS
residue NONE
```

Duration: **30 minutes** (`14:28` -> `14:58` JST; explicit author metadata).

Canonical snapshot:

- `review-benchmark/major-fix-snapshot.md`
- `review-benchmark/major-fix-snapshot.json`
- Revision: `fnd04-final-major-fix-snapshot-v1`

## New Head CI

Direct-head:

```text
Run 31360093004
checkout 3511688401533f60bb77c7dcc647c4c2c4aa84c6
SUCCESS
```

PR merge-ref:

```text
Run 31360094852
checkout 2e69049bd8b38e57cd4fee2c42e17edaeaf23df1
Merge 3511688401533f60bb77c7dcc647c4c2c4aa84c6
  into 38c07e210fe4e8689f1d8aeabbb07b92610d1826
SUCCESS
```

Both runs independently verified:

- build 0 warnings / 0 errors
- pending-model PASS
- non-PostgreSQL 42 PASS
- real PostgreSQL 23 PASS

## Next gate — targeted G-01 re-review

Common prompt:

- `prompts/final-synthesis-major-fix-re-review.md`
- Revision: `fnd04-final-major-fix-rereview-v1`

Reviewers:

| Slot | Model / Harness | Scope |
|---|---|---|
| T1 | GPT-5.6 Sol / Codex / xHigh | independent G-01 mutation sensitivity |
| T2 | Cursor Auto / Cursor | independent practical targeted verification |

The old 5-review pool is **not rerun**. Targeted re-review checks only whether G-01 is fixed and whether the 18-line test-only patch introduced a new Blocker/Major.

Clearance condition:

```text
T1 G01_FIXED
T2 G01_FIXED
new Blocker 0
new Major 0
```

After clearance, proceed to Formal Agent B product merge gate.

PR #140 remains OPEN / DRAFT / UNMERGED. Ready, merge and Issue #42 close remain prohibited until Formal Agent B completes.
