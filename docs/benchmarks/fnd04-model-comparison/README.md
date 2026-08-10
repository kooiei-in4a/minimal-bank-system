# FND-04 Model Comparison

Target Issue: #42 `[FND-04] EF Core・明示的migration実行基盤を確立する`

Status: **G-01 CLEARED / FORMAL AGENT B READY**

このdirectoryはFND-04 benchmarkのH0/SR/H1、Implementation Evaluation、Selection / Adjudication、Final Synthesis、独立review、Judge、Gold、Major fix、clearanceを管理するbenchmark control正本である。

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
Judge C                          NOT REQUIRED
Gold / Reference                 LOCKED / G-01 MAJOR ON OLD HEAD
G-01 targeted fix                COMPLETE / NEW HEAD LOCKED
Targeted Major-fix re-review     COMPLETE / 2 OF 2 G01_FIXED
G-01 clearance                   PASS / LOCKED
Formal Agent B                   READY / NOT STARTED
```

## Final Synthesis current target

```text
PR:            #140
Branch:        agent/issue-42-fnd-04-final-code
Base SHA:      38c07e210fe4e8689f1d8aeabbb07b92610d1826
Current Head:  3511688401533f60bb77c7dcc647c4c2c4aa84c6
PR merge ref:  2e69049bd8b38e57cd4fee2c42e17edaeaf23df1
```

PR #140 remains OPEN / DRAFT / UNMERGED.

## Gold history

Old Head:

```text
99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
```

Judge A/B quorum:

```text
CHANGES_REQUIRED
Blocking root cause: G-01 / NR-01
Merge-ready: NO
```

Canonical Gold:

- `review-benchmark/gold-review.md`
- `review-benchmark/gold-review.json`
- Revision: `fnd04-final-gold-v1`

## G-01 targeted fix

Old -> new:

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

The new regression positively pins:

- uninitialized ConnectionString
- empty destination
- Npgsql execution path
- EF Migrations execution path

Duration: 30 minutes (`14:28` -> `14:58` JST, explicit author metadata).

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
Merge 3511688401533f60bb77c7dcc647c4c2c4aa84c6 into 38c07e210fe4e8689f1d8aeabbb07b92610d1826
SUCCESS
```

Both passed:

- build 0 warnings / 0 errors
- pending-model
- non-PostgreSQL 42
- real PostgreSQL 23

## G-01 targeted re-review — COMPLETE

| Slot | Model / Harness | Verdict | New B | New M |
|---|---|---|---:|---:|
| T1 | GPT-5.6 Sol / Codex / xHigh | G01_FIXED | 0 | 0 |
| T2 | Cursor / Auto | G01_FIXED | 0 | 0 |

Both independently verified baseline PASS, M1 FAIL, M2 FAIL, recovery PASS and mutation residue NONE.

Canonical clearance:

- `review-benchmark/major-fix-clearance.md`
- `review-benchmark/major-fix-clearance.json`
- Revision: `fnd04-final-major-fix-clearance-v1`

**G-01 status: CLEARED.**

Known Gold nonblocking findings remain nonblocking and were not mixed into the Major fix.

## Next — Formal Agent B product merge gate

Canonical prompt:

- `prompts/final-synthesis-formal-agent-b.md`
- Revision: `fnd04-formal-agent-b-v1`

Recommended execution:

```text
Claude Opus 5 / Claude Code / xHigh
```

Formal Agent B independently reviews the complete Issue #42 contract against exact current Head and records one formal GitHub review on PR #140.

If Blocker / Major = 0, it may record `APPROVE`; if Blocker / Major exists, `REQUEST_CHANGES`.

Formal Agent B itself must not Ready the PR, merge, or close Issue #42.

## Experiment flow

```text
8 candidates
  -> H0 / SR / H1                         COMPLETE
    -> Implementation Evaluation          COMPLETE
      -> Selection / Adjudication         COMPLETE
        -> Final Synthesis                COMPLETE
          -> role-diverse review 5/5      COMPLETE
            -> Judge A/B                  COMPLETE
              -> Gold / Reference         COMPLETE
                -> G-01 targeted fix      COMPLETE
                  -> G-01 re-review 2/2   COMPLETE / CLEARED
                    -> Formal Agent B      NEXT
                      -> Ready / merge / Issue close only after approval
```
