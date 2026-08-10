# FND-04 Model Comparison

Target Issue: #42 `[FND-04] EF Core・明示的migration実行基盤を確立する`

Status: **SELECTION / ADJUDICATION LOCKED / READY FOR FINAL SYNTHESIS**

このdirectoryはFND-04 benchmarkの実行条件、locked snapshots、evaluation、selection / adjudicationを管理するbenchmark control正本である。

candidate branchはH1 LOCK後に変更していない。Final Synthesisはcandidate rankingとは別のcurated implementationとして扱う。

## Current state

```text
H0 implementation snapshot       8/8 LOCKED
Formal Self-Review               8/8 LOCKED
H1 self-review fix snapshot      8/8 LOCKED
H1 exact-head CI                 8/8 SUCCESS
Implementation Evaluation        COMPLETE / LOCKED
Selection / Adjudication         COMPLETE / LOCKED
Final Synthesis                  READY / NOT STARTED
```

## Locked implementation result

- H1 winner: `claude-opus-5-claude-code` — 99
- H0 winner: `gpt-5.6-sol-codex` — 98
- Maximum Self-Review Gain: `claude-sonnet-5-claude-code` — +3
- Merge-ready candidate: 7 / 8
- Non-merge-ready: `deepseek-v4-flash-opencode`
- Blocking finding: `C8-M01`

DurationはH0 / SR / H1を全candidate一貫して収集できなかったためN/A。Speed Score / Quality-Time Index / Practical Score speed componentは計算しない。

## Locked Final Synthesis selection

Primary:

- C5 `claude-opus-5-claude-code`
- H1 Head: `3a788cc31b3f65177d60dd3995842231dd505187`
- Role: architecture / production-path verification base

Additional adoption:

- C1: failed Migrator outputへcredential / passwordが漏れないことを確認するregression test
- C8-M01: missing `ConnectionStrings__Database`時のconnection-required design-time operationをfail-closedにするmandatory regression

Explicit non-selection:

- C6 `TimeProvider` seamは初期Final Synthesisへ追加しない。C5のreal PostgreSQL lockによるproduction 60-second timeout証拠を優先する。
- C8のfabricated `Host=127.0.0.1;...Database=design_time` fallback patternは採用禁止。
- C2 / C3 / C4 / C7から、C5を置換する独自要素は採用しない。

Canonical selection files:

- `results/selection-adjudication.md`
- `results/selection-adjudication.json`

## Final Synthesis construction boundary

```text
Branch:        agent/issue-42-fnd-04-final-code
Base branch:   main
Expected base: 38c07e210fe4e8689f1d8aeabbb07b92610d1826
```

- candidate branch merge: prohibited
- candidate commit cherry-pick: prohibited
- benchmark candidate artifacts modification: prohibited
- current mainがExpected baseから動いていた場合は開始せず再確認する

Final SynthesisはC5を主軸に、selection / adjudicationで明示された追加要素だけをcurateする。候補機能の全部盛りは行わない。

## Required Final Synthesis evidence

- exact package / local tool pinning
- clean PostgreSQL `0 -> InitialFoundation`
- explicit Migrator rerun
- missing / unreachable / rejected credential failure
- real PostgreSQL blockingによるactual 60-second timeout
- failed Migrator output secret non-disclosure
- API startup no migration / no `EnsureCreated`
- API `BankDbContext` resolve時もschema mutationなし
- actual EF pending-model positive check
- evaluator-only temporary model drift negative probe + clean recovery
- idempotent migration SQL generation
- C8-M01 missing design-time connection fail-closed regression
- business schemaなし
- `git diff --check`
- exact Head CI success

## Duration experiment for Final Synthesis

candidate benchmarkのDuration=N/Aは変更しない。

Final Synthesisだけ、実装Agentが次を分単位で明示記録する。

```text
STARTED_AT_LOCAL: YYYY-MM-DD HH:MM
FINISHED_AT_LOCAL: YYYY-MM-DD HH:MM
DURATION_MINUTES: integer
```

GitHub timestampから処理時間を推定しない。この値をcandidateのSpeed rankingへ遡及適用しない。

## Experiment shape

```text
8 candidates
  H0 implementation snapshot
    -> Formal Self-Review
      -> H1 self-review fix snapshot
        -> implementation evaluation        [COMPLETE / LOCKED]
          -> selection / adjudication        [COMPLETE / LOCKED]
            -> curated Final Synthesis       [NEXT]
              -> role-diverse independent review
                -> 2 Judges (+ 1 conditional tie-breaker)
                  -> Formal Agent B review
```

## Reviewer pool — 6 roles

| Slot | Model + Harness | Primary role |
| --- | --- | --- |
| R1 | GPT-5.6 Sol / Codex | runtime / failure-path |
| R2 | Claude Opus 5 / Claude Code | deep technical / test assurance |
| R3 | Claude Sonnet 5 / Claude Code | specification / scope |
| R4 | ChatGPT Opus 5.6 Sol / Browser | framework / official-source cross-check |
| R5 | GPT-5.6 Luna / Open Code | tool-driven independent review |
| R6 | Grok 4.5 / Cursor | fast independent review |

Exact product-visible model identity and effortはreviewer execution直前に再確認する。

## Judge quorum

- Judge A: GPT-5.6 Sol / Codex
- Judge B: Claude Opus 5 / Claude Code
- Conditional Judge C: GPT-5.6 Pro / Browser

Judge Cはfirst two Judgesがreference verdict、blocking root cause、merge-ready判断で不一致の場合のみ使用する。

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
Selection / adjudication:       fnd04-selection-adjudication-v1
```

## Files

- `run.json`: machine-readable benchmark identity / phase state / candidate snapshots / locked decisions
- `scoring.md`: H0/H1 common scoring rubric
- `reference/assumption-ledger.md`: pre-locked external assumptions
- `reference/evaluator-probes.md`: evaluator-only adversarial probes
- `prompts/implementation-h0.md`: H0 implementation prompt
- `prompts/formal-self-review.md`: Formal Self-Review prompt
- `prompts/self-review-fix-h1.md`: H1 fix prompt
- `results/implementation-evaluation.md`: human-readable Implementation Evaluation
- `results/implementation-evaluation.json`: machine-readable Implementation Evaluation
- `results/selection-adjudication.md`: human-readable Final Synthesis selection / adjudication
- `results/selection-adjudication.json`: machine-readable selection / adjudication

## Gate boundary

Selection / Adjudicationまでcomplete / locked。

次工程はlocal AgentによるFinal Synthesis implementationである。このREADME更新自体はFinal Synthesis completion、Ready化、merge、Issue #42 closeを許可しない。Draft PR / exact Head CI取得後に独立レビュー工程へ進む。
