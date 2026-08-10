# FND-04 Model Comparison

Target Issue: #42 `[FND-04] EF Core・明示的migration実行基盤を確立する`

Status: **IMPLEMENTATION EVALUATION LOCKED / READY FOR FINAL SYNTHESIS**

このdirectoryはFND-04 benchmarkの実行条件、locked snapshots、evaluation結果を管理するbenchmark control正本である。

現在までに、H0 implementation 8/8、Formal Self-Review 8/8、H1 Self-Review Fix 8/8、Implementation Evaluationを完了・LOCKした。candidate branchはH1 LOCK後に変更していない。

## Current result

```text
H0 implementation snapshot       8/8 LOCKED
Formal Self-Review               8/8 LOCKED
H1 self-review fix snapshot      8/8 LOCKED
H1 exact-head CI                 8/8 SUCCESS
Implementation Evaluation        COMPLETE / LOCKED
Final Synthesis                  READY / NOT STARTED
```

Implementation Evaluation canonical result:

- H1 winner: `claude-opus-5-claude-code` — 99
- H0 winner: `gpt-5.6-sol-codex` — 98
- Maximum Self-Review Gain: `claude-sonnet-5-claude-code` — +3
- Merge-ready: 7 / 8
- Non-merge-ready: `deepseek-v4-flash-opencode`
- Blocking finding: `C8-M01`

DurationはH0 / SR / H1を全candidate一貫して収集できなかったためN/A。Speed Score / Quality-Time Index / Practical Score speed componentは計算しない。

## Experiment shape

```text
8 candidates
  H0 implementation snapshot
    -> Formal Self-Review (fresh context, review-only)
      -> H1 self-review fix snapshot
        -> implementation evaluation        [COMPLETE / LOCKED]
          -> curated Final Synthesis         [NEXT]
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
H1 execution wrapper:           fnd04-h1-exec-time-v1
Implementation scoring:         fnd04-implementation-v1
Evaluator probes:               fnd04-evaluator-probes-v1
Assumption ledger:              fnd04-assumptions-v1
Implementation result:          fnd04-implementation-evaluation-v1
```

## Benchmark gates

- [x] Issue #42 Issue Ready = PASS
- [x] common base full SHA fixed: `38c07e210fe4e8689f1d8aeabbb07b92610d1826`
- [x] all 8 candidate branches created from common base
- [x] package/version contract fixed
- [x] `reference/assumption-ledger.md` locked
- [x] H0 candidate prompt revision fixed
- [x] Formal Self-Review / H1 prompt revisions fixed
- [x] scoring rubric fixed
- [x] evaluator-only probe plan fixed
- [x] H0 8/8 locked
- [x] Formal Self-Review 8/8 locked
- [x] H1 8/8 locked
- [x] H1 exact-head CI 8/8 success
- [x] Implementation Evaluation complete / locked
- [ ] Final Synthesis started

## Files

- `run.json`: machine-readable benchmark identity / phase state / candidate snapshots / evaluation summary
- `scoring.md`: H0/H1共通implementation scoring rubric
- `reference/assumption-ledger.md`: external-library and project assumptions locked before candidate outputs
- `reference/evaluator-probes.md`: candidate共通のadversarial verification plan
- `prompts/implementation-h0.md`: H0 implementation prompt
- `prompts/formal-self-review.md`: fresh-context review-only prompt
- `prompts/self-review-fix-h1.md`: SR Finding disposition / H1 fix prompt
- `results/implementation-evaluation.md`: canonical human-readable Implementation Evaluation
- `results/implementation-evaluation.json`: machine-readable Implementation Evaluation result

## Implementation Evaluation lock boundary

Implementation EvaluationはH1 lock commit `93d46a3822a8fddc342781cf5cd981cbac268cdd`を入力snapshotとして実施した。

Evaluation完了後もcandidate branch / PRは変更しない。Evaluation resultの固定はbenchmark control branchのみで行う。

次工程はcurated Final Synthesisであり、このREADME更新自体はFinal Synthesis implementation、candidate fix、merge、Issue closeを許可するものではない。
