# FND-04 Final Synthesis Independent Review Benchmark

Status: **RAW CAPTURE COMPLETE / READY FOR JUDGE QUORUM**

```yaml
BENCHMARK_ID: fnd04-final-synthesis-independent-review
RUN_ID: fnd04-final-review-20260810
POOL_REVISION: fnd04-reviewer-pool-v2
PROMPT_REVISION: fnd04-final-review-v1
TARGET_PR: 140
BASE_SHA: 38c07e210fe4e8689f1d8aeabbb07b92610d1826
HEAD_SHA: 99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
RAW_CAPTURE: 5 / 5
```

このdirectoryはFND-04 Final Synthesisのrole-diverse independent review raw artifacts、finding normalization、Judge adjudicationを管理する。

## Target identity

- PR #140 / Head `99cee4386ea049ad84e9c087c6fdf1e25cc20f3e`
- Base `38c07e210fe4e8689f1d8aeabbb07b92610d1826`
- PR merge-ref CI run `31350916189`: SUCCESS / checkout `d12de2ae07003a10d19d576808cf88ec7796da23`
- direct-head push CI run `31350870902`: SUCCESS / checkout exact Head `99cee4386ea049ad84e9c087c6fdf1e25cc20f3e`

Coordinator independently verified the direct-head run after raw reviewer results were received. It passed build with 0 warnings / 0 errors, pending-model, non-PostgreSQL tests (42), and real PostgreSQL tests (23).

## Reviewer pool — Revision 2 / COMPLETE

| Slot | Model + Harness | Primary role | Verdict | Merge-ready |
|---|---|---|---|---|
| R1 | GPT-5.6 Sol / Codex | runtime / failure-path | APPROVE_WITH_FINDINGS | YES |
| R2 | Claude Opus 5 / Claude Code | deep technical / test assurance | CHANGES_REQUIRED | **NO** |
| R3 | GPT-5.6 Luna / Codex | specification / scope | APPROVE_WITH_FINDINGS | YES |
| R4 | GPT-5.6 Sol / Browser | framework / official-source | APPROVE | YES |
| R5 | Cursor Auto / Cursor | fast independent review | APPROVE_WITH_FINDINGS | YES |

Raw capture is **5 / 5 complete**. Each reviewer has a Markdown + JSON pair under `reviews/`.

R3 raw structured output contains an abbreviated `target_verification.observed_base_sha`; this raw field is retained as received. Its top-level target Base SHA is the fixed full value and its target-verification verdict was PASS. Collector records this as an integrity note rather than silently correcting the raw result.

## Raw artifacts

```text
reviews/gpt-5.6-sol-codex.md
reviews/gpt-5.6-sol-codex.json

reviews/claude-opus-5-claude-code.md
reviews/claude-opus-5-claude-code.json

reviews/gpt-5.6-luna-codex-final-review.md
reviews/gpt-5.6-luna-codex-final-review.json

reviews/chatgpt-browser-framework-review.md
reviews/chatgpt-browser-framework-review.json

reviews/cursor-auto.md
reviews/cursor-auto.json
```

Raw review semantics are not adjudicated by editing the raw files. Collector normalization is stored separately.

## Review result summary

```text
APPROVE:                 1
APPROVE_WITH_FINDINGS:   3
CHANGES_REQUIRED:        1

merge-ready YES:         4 / 5
merge-ready NO:          1 / 5

Blocker reported:        0 reviewers
Major reported:          1 reviewer (R2)
```

The one blocking candidate is R2-F01, normalized as `NR-01`.

## Pre-Judge finding normalization

Canonical Collector artifacts:

- `finding-normalization-prejudge.md`
- `finding-normalization-prejudge.json`

Normalized candidates:

| ID | Topic | Pre-Judge status |
|---|---|---|
| NR-01 | C8-M01 regression test / false assurance | **valid candidate / Severity disputed / Judge required** |
| NR-02 | 60s CommandTimeout vs CTS exit classification | disputed Minor candidate |
| NR-03 | temporary model-drift negative evidence | independently reproduced by R1/R2; likely evidence limit only |
| NR-04 | PR CI identity wording | Nit; direct-head evidence now independently verified |
| NR-05 | failure exit-code taxonomy coverage | unique Minor candidate |
| NR-06 | low-information assertions | Nit candidate |

### NR-01 key distinction

Multiple reviewers agree current production behavior is fail-closed. The dispute is whether the committed `DesignTimeConnectionSafetyTests` provides sufficient regression assurance.

- R2: Major / blocking; mutation probes show the test stays green with an off-blocklist fabricated destination and when the EF factory cannot be reached.
- R5: same basic weakness, Severity Minor.
- R4: no finding, but states the blocklist-only assertion is weak when considered alone.

Do not fix PR #140 solely by majority/minority vote. Severity and blocking status require Judge adjudication from primary evidence.

## Judge quorum — NEXT

Canonical Judge prompt:

- `../prompts/final-synthesis-judge.md`
- Revision: `fnd04-final-judge-v1`

Expected independent Judges:

- Judge A: GPT-5.6 Sol / Codex
- Judge B: Claude Opus 5 / Claude Code
- Conditional Judge C: GPT-5.6 Pro / Browser

Judge C is used only when A/B disagree on Reference verdict, blocking root cause, or merge-ready judgement.

Judge A/B must use fresh context and first establish an independent Phase-A Reference from Issue / ADR / exact source / tests / CI before reading raw reviewer findings. Reviewer identity or model reputation must not influence adjudication.

## Gold / Reference status

Raw capture is now complete. Final adjudicated Gold / Reference is **not yet locked**. It will be fixed after Judge quorum so the disputed blocking root cause is not decided by the Collector alone.

## Product merge gate

PR #140 remains Draft / unmerged. Role-diverse reviewer majority is not a merge gate. Current flow:

```text
5/5 raw review capture       COMPLETE
  -> finding normalization   COMPLETE / PRE-JUDGE
    -> Judge A + Judge B     NEXT
      -> Judge C if required
        -> adjudicated Gold / Reference
          -> targeted fix if blocking finding confirmed
            -> Formal Agent B product merge review
```

No Ready, merge, or Issue #42 close action is authorized by this benchmark state.