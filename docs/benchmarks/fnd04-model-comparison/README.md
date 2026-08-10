# FND-04 Model Comparison

Target Issue: #42 `[FND-04] EF Core・明示的migration実行基盤を確立する`

Status: **FORMAL AGENT B TECHNICAL GATE PASS / MERGE PRECONDITIONS**

このdirectoryはFND-04 benchmarkのH0/SR/H1、Implementation Evaluation、Selection / Adjudication、Final Synthesis、独立review、Judge、Gold、Major fix、clearance、Formal Agent Bを管理するbenchmark control正本である。

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
Formal Agent B                   COMPLETE / APPROVE / B0 M0
GitHub APPROVE event              NOT OBTAINED / SELF-APPROVAL PROHIBITED
```

## Current product target

```text
PR:            #140
Branch:        agent/issue-42-fnd-04-final-code
Base SHA:      38c07e210fe4e8689f1d8aeabbb07b92610d1826
Current Head:  3511688401533f60bb77c7dcc647c4c2c4aa84c6
PR merge ref:  2e69049bd8b38e57cd4fee2c42e17edaeaf23df1
```

PR #140 remains OPEN / DRAFT / UNMERGED at Formal Agent B review time.

## Review / adjudication history

```text
5 role-diverse reviewers          COMPLETE
Judge A / B                       CHANGES_REQUIRED / NR-01 / NO
Gold old Head                     G-01 Major / blocking
Targeted test-only fix            COMPLETE
T1 re-review                       G01_FIXED / B0 M0
T2 re-review                       G01_FIXED / B0 M0
G-01 clearance                    PASS
```

Canonical clearance:

- `review-benchmark/major-fix-clearance.md`
- `review-benchmark/major-fix-clearance.json`
- Revision `fnd04-final-major-fix-clearance-v1`

## Current Head CI

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

Both passed build 0 warnings / 0 errors, pending-model, non-PostgreSQL 42 and real PostgreSQL 23.

## Formal Agent B — COMPLETE

Canonical prompt:

- `prompts/final-synthesis-formal-agent-b.md`
- Revision `fnd04-formal-agent-b-v1`

Execution:

```text
Claude Opus 5 / Claude Code / xHigh
Formal verdict: APPROVE
Merge-ready: YES
Blocker: 0
Major: 0
Minor: 2 nonblocking
Nit: 3 nonblocking
```

GitHub formal review record:

- Review ID: `4894487758`
- URL: `https://github.com/kooiei-in4a/minimal-bank-system/pull/140#pullrequestreview-4894487758`
- Reviewed commit: `3511688401533f60bb77c7dcc647c4c2c4aa84c6`
- GitHub event: `COMMENTED`

The reviewer attempted `APPROVE`, but GitHub rejected it with `422 Review Can not approve your own pull request` because `kooiei-in4a` is both the authenticated account and PR author. The single COMMENT review explicitly records that the formal technical verdict is APPROVE.

Canonical result:

- `review-benchmark/formal-agent-b-result.md`
- `review-benchmark/formal-agent-b-result.json`

## Gate interpretation

Technical product merge gate: **PASS**.

The absence of GitHub `APPROVED` state is a platform / identity constraint, not a technical review failure. However, this control record does not assume that repository branch rules accept a COMMENT in place of an APPROVED review.

Before merge:

1. determine whether repository rules require a non-author GitHub approval;
2. if required, obtain one `APPROVED` review from another authorized account;
3. mark PR #140 Ready;
4. verify final mergeability / required checks;
5. merge;
6. verify main contains the merged result and CI/evidence remains valid;
7. close Issue #42 only after merge and Close evidence are complete.

## Known nonblocking Formal Agent B findings

- MIN-01: two Migrator negative tests pin non-success but not the specific failure reason;
- MIN-02: documented idempotent SQL CLI path is independently verified but is not itself run as a CI CLI command;
- three Nits concerning design-time configuration asymmetry, command-line secret defense-in-depth and exact error-message coupling in G-01 test.

None is Blocker/Major.

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
                    -> Formal Agent B      COMPLETE / APPROVE / B0 M0
                      -> rule check / Ready / merge / Issue close
```
