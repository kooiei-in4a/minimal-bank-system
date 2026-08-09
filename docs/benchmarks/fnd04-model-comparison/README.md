# FND-04 Model Comparison — Pre-Run Scaffold

Target Issue: #42 `[FND-04] EF Core・明示的migration実行基盤を確立する`

Status: **PREPARED / NOT STARTED**

このdirectoryはFND-04 benchmarkの実行条件をcandidate開始前に固定する。ここに結果やGoldを先書きしない。

## Experiment shape

```text
8 candidates
  H0 implementation snapshot
    -> Formal Self-Review (fresh context, review-only)
      -> H1 self-review fix snapshot
        -> implementation evaluation
          -> curated Final Synthesis
            -> role-diverse independent review
              -> 2 Judges (+ 1 conditional tie-breaker)
                -> Formal Agent B review
```

Majorが確定した場合のtargeted fix roundは原則最大4候補とし、14候補全再実行を標準にしない。

## Implementation candidate pool

### Active core — 6

1. GPT-5.6 Sol / Codex
2. GPT-5.6 Terra / Codex
3. GPT-5.6 Luna / Codex
4. GPT-5.6 Luna / Open Code
5. Claude Opus 5 / Claude Code
6. Claude Sonnet 5 / Claude Code

### Challengers — 2

7. Grok 4.5 / Cursor — speed / alternative execution profile
8. DeepSeek V4 Flash / Open Code — Open Code challenger / alternative failure-proof style

### Reserve

- Qwen3.7 Plus / Open Code
- Composer 2.5 / Cursor
- DeepSeek V4 Pro / Open Code

### Suspended for this run

- MiniMax M3 / Open Code
- MiMo-V2.5 / Open Code
- MiMo-V2.5-Pro / Open Code

Suspension is not permanent exclusion. See the parent methodology for re-entry rules.

## Reviewer pool — 6 roles

| Slot | Model + Harness | Primary role |
| --- | --- | --- |
| R1 | GPT-5.6 Sol / Codex | runtime / failure-path |
| R2 | Claude Opus 5 / Claude Code | deep technical / test assurance |
| R3 | Claude Sonnet 5 / Claude Code | specification / scope |
| R4 | ChatGPT Opus 5.6 Sol / Browser | framework / official-source cross-check |
| R5 | GPT-5.6 Luna / Open Code | tool-driven independent review |
| R6 | Grok 4.5 / Cursor | fast independent review |

Exact product-visible model identity and effort are re-verified immediately before reviewer execution and recorded in `run.json`; a changed/unavailable product label is not silently substituted.

## Judge quorum

- Judge A: GPT-5.6 Sol / Codex
- Judge B: Claude Opus 5 / Claude Code
- Conditional Judge C: GPT-5.6 Pro / Browser

Judge C is used only if the first two Judges disagree on reference verdict, blocking root cause, or merge-ready candidate.

## Review targets

- Real target: actual FND-04 Final Synthesis
- Controlled Mutant: optional reviewer-capability target with pre-locked Gold; mutation details remain collector-private until raw reviews are fixed

## Locked revisions

```text
H0 implementation prompt:       fnd04-h0-v1
Formal Self-Review prompt:      fnd04-sr-v1
H1 fix prompt:                  fnd04-h1-v1
Implementation scoring:         fnd04-implementation-v1
Evaluator probes:               fnd04-evaluator-probes-v1
Assumption ledger:              fnd04-assumptions-v1
```

## Pre-run gates

- [x] Issue #42 Issue Ready = PASS
- [ ] common base full SHA fixed
- [ ] all 8 candidate branches created from common base
- [x] package/version contract fixed
- [x] `reference/assumption-ledger.md` locked
- [x] H0 candidate prompt revision fixed
- [x] Formal Self-Review / H1 prompt revisions fixed
- [x] scoring rubric fixed
- [x] evaluator-only probe plan fixed
- [x] no candidate execution started

## Files

- `run.json`: machine-readable pre-run identity
- `scoring.md`: H0/H1共通implementation scoring rubric
- `reference/assumption-ledger.md`: external-library and project assumptions locked before candidate outputs
- `reference/evaluator-probes.md`: candidate共通のadversarial verification plan
- `prompts/implementation-h0.md`: H0 implementation prompt
- `prompts/formal-self-review.md`: fresh-context review-only prompt
- `prompts/self-review-fix-h1.md`: SR Finding disposition / H1 fix prompt

## Start boundary

このpreparationがmainへmergeされた後、そのmerge commitをbenchmark common baseとして固定する。

次に8 candidate branchをそのexact SHAから事前作成し、branch / Headが8 / 8一致することを確認する。

**そこまで完了してもcandidate model executionは開始しない。**

Benchmark execution開始はKooが実行プロンプトを各Harnessへ投入した時点とする。
