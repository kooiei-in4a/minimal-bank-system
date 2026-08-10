# FND-04 Model Comparison

Target Issue: #42 `[FND-04] EF Core・明示的migration実行基盤を確立する`

Status: **FINAL SYNTHESIS REVIEW 5/5 COMPLETE / READY FOR JUDGE QUORUM**

このdirectoryはFND-04 benchmarkの実行条件、candidate snapshots、Implementation Evaluation、Selection / Adjudication、Final Synthesis snapshot、independent review、Judge adjudicationを管理するbenchmark control正本である。

## Current state

```text
H0 implementation snapshot       8/8 LOCKED
Formal Self-Review               8/8 LOCKED
H1 self-review fix snapshot      8/8 LOCKED
H1 exact-head CI                 8/8 SUCCESS
Implementation Evaluation        COMPLETE / LOCKED
Selection / Adjudication         COMPLETE / LOCKED
Final Synthesis implementation   COMPLETE / SNAPSHOT LOCKED
Role-diverse independent review  COMPLETE / 5 OF 5
Finding normalization            COMPLETE / PRE-JUDGE
Judge quorum                     READY / NOT STARTED
Formal Agent B                   NOT STARTED
```

## Locked implementation result

- H1 winner: `claude-opus-5-claude-code` — 99
- H0 winner: `gpt-5.6-sol-codex` — 98
- Maximum Self-Review Gain: `claude-sonnet-5-claude-code` — +3
- Merge-ready candidate at Implementation Evaluation: 7 / 8
- Non-merge-ready candidate: `deepseek-v4-flash-opencode`
- Blocking candidate finding: `C8-M01`

H0 / SR / H1 Durationは全candidateで一貫収集できなかったためN/A。Final Synthesisのみexplicit Agent recordとして29分を保持する。

## Final Synthesis locked target

```text
PR:            #140
Branch:        agent/issue-42-fnd-04-final-code
Base SHA:      38c07e210fe4e8689f1d8aeabbb07b92610d1826
Head SHA:      99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
Commits:       1
Changed files: 25
Diff:          +1149 / -1
Duration:      29 minutes
```

PR #140はOPEN / DRAFT / UNMERGED。

## Final Synthesis CI

Both CI identities are now independently resolved:

```text
PR merge-ref run:
  31350916189
  checkout d12de2ae07003a10d19d576808cf88ec7796da23
  SUCCESS

Direct-head push run:
  31350870902
  checkout 99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
  SUCCESS
```

Direct-head run evidence:

- build: 0 warnings / 0 errors
- pending-model: PASS
- non-PostgreSQL: Unit 4 + Integration 38 = 42 PASS
- real PostgreSQL: 23 PASS

The earlier coordinator note that direct-head CI was unresolved is superseded by this verified push run.

## Role-diverse independent review — COMPLETE

Reviewer pool revision: `fnd04-reviewer-pool-v2`

| Slot | Model + Harness | Role | Verdict | Merge-ready |
|---|---|---|---|---|
| R1 | GPT-5.6 Sol / Codex | runtime / failure-path | APPROVE_WITH_FINDINGS | YES |
| R2 | Claude Opus 5 / Claude Code | deep technical / test assurance | CHANGES_REQUIRED | **NO** |
| R3 | GPT-5.6 Luna / Codex | specification / scope | APPROVE_WITH_FINDINGS | YES |
| R4 | GPT-5.6 Sol / Browser | framework / official-source | APPROVE | YES |
| R5 | Cursor Auto / Cursor | fast practical broad scan | APPROVE_WITH_FINDINGS | YES |

Raw Markdown + JSON pair: **5 / 5 captured** under `review-benchmark/reviews/`.

## Key disputed finding

Only one reviewer reported a Blocker/Major:

- R2-F01 — Major / blocking
- normalized root cause: `NR-01`

Topic: `DesignTimeConnectionSafetyTests` may be false assurance for the prior C8-M01 defect class.

Important distinction:

- multiple reviewers independently consider the current production factory behavior fail-closed and correct;
- R2 demonstrates that the committed regression test itself can remain green when an off-blocklist fabricated destination is injected or when the `--no-build` command cannot reach the factory;
- R5 independently identified the same basic test-evidence weakness but rated it Minor;
- R4 did not raise a finding but acknowledged the blocklist-only assertion is weak in isolation.

Therefore `NR-01` is **not yet a confirmed Major**. It is a valid root-cause candidate with disputed Severity / blocking status and goes to Judge A/B.

## Other normalized findings

- `NR-02`: CommandTimeout(60) vs CTS(60s) timeout classification — R1/R5 Minor, disputed
- `NR-03`: temporary model-drift evidence — initial evidence limitation, but R1/R2 independently reproduced drift detection and clean recovery
- `NR-04`: PR CI wording — Nit; direct-head CI evidence now resolved
- `NR-05`: ordinary failure exit-code taxonomy not specifically pinned — R2 Minor
- `NR-06`: low-information constant assertions — R2 Nit

Canonical normalization:

- `review-benchmark/finding-normalization-prejudge.md`
- `review-benchmark/finding-normalization-prejudge.json`

## Judge quorum — NEXT

Canonical prompt:

- `prompts/final-synthesis-judge.md`
- Revision: `fnd04-final-judge-v1`

Judges:

- Judge A: GPT-5.6 Sol / Codex
- Judge B: Claude Opus 5 / Claude Code
- Conditional Judge C: GPT-5.6 Pro / Browser

Judge A/Bは互いの結果を見ずfresh contextで実行する。まずraw reviewsを読まずにIssue / ADR / exact source / tests / CIからPhase-A Referenceを固定し、その後normalized findingsを裁定する。

Judge CはA/Bが次のいずれかで不一致の場合のみ追加する。

- Reference verdict
- blocking root cause
- merge-ready judgement

## Gold / Reference

Raw reviewer capture前にGoldは公開していない。Final adjudicated Gold / ReferenceはJudge quorum後に固定する。

## Experiment flow

```text
8 candidates
  -> H0 / SR / H1
    -> Implementation Evaluation          COMPLETE
      -> Selection / Adjudication         COMPLETE
        -> Final Synthesis                COMPLETE
          -> 5 role-diverse reviews       COMPLETE
            -> finding normalization      COMPLETE / PRE-JUDGE
              -> Judge A + Judge B        NEXT
                -> Judge C if required
                  -> adjudicated Gold / Reference
                    -> targeted fix if blocking finding confirmed
                      -> Formal Agent B product merge review
```

PR #140 Ready化、merge、Issue #42 closeはまだ許可しない。