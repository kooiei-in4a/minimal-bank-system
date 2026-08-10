# FND-04 Model Comparison

Target Issue: #42 `[FND-04] EF Core・明示的migration実行基盤を確立する`

Status: **FINAL SYNTHESIS LOCKED / READY FOR ROLE-DIVERSE INDEPENDENT REVIEW**

このdirectoryはFND-04 benchmarkの実行条件、candidate snapshots、Implementation Evaluation、Selection / Adjudication、Final Synthesis snapshot、independent review runを管理するbenchmark control正本である。

candidate branchはH1 LOCK後に変更していない。Final Synthesisはcandidate rankingとは別のcurated implementationとして扱う。

## Current state

```text
H0 implementation snapshot       8/8 LOCKED
Formal Self-Review               8/8 LOCKED
H1 self-review fix snapshot      8/8 LOCKED
H1 exact-head CI                 8/8 SUCCESS
Implementation Evaluation        COMPLETE / LOCKED
Selection / Adjudication         COMPLETE / LOCKED
Final Synthesis prompt           LOCKED
Final Synthesis implementation   COMPLETE / SNAPSHOT LOCKED
Role-diverse independent review  READY / 0 OF 5 STARTED
Judge quorum                     NOT STARTED
Formal Agent B                   NOT STARTED
```

## Locked implementation result

- H1 winner: `claude-opus-5-claude-code` — 99
- H0 winner: `gpt-5.6-sol-codex` — 98
- Maximum Self-Review Gain: `claude-sonnet-5-claude-code` — +3
- Merge-ready candidate at Implementation Evaluation: 7 / 8
- Non-merge-ready candidate: `deepseek-v4-flash-opencode`
- Blocking candidate finding: `C8-M01`

H0 / SR / H1 Durationは全candidateで一貫収集できなかったためN/A。Speed Score / Quality-Time Index / Practical Score speed componentは計算しない。

## Locked Selection / Adjudication

Primary:

- C5 `claude-opus-5-claude-code`
- H1 Head: `3a788cc31b3f65177d60dd3995842231dd505187`
- architecture / production-path verification base

Additional adoption:

- C1: failed Migrator outputのcredential / password non-disclosure regression
- C8-M01: missing `ConnectionStrings__Database`時のconnection-required design-time fail-closed regression

Explicit non-selection:

- C6 `TimeProvider` seamは初期Final Synthesisへ追加しない
- C8 fabricated `127.0.0.1 / design_time` destinationは禁止
- C2 / C3 / C4 / C7からC5を置換する要素は採用しない

Canonical files:

- `results/selection-adjudication.md`
- `results/selection-adjudication.json`

## Final Synthesis locked target

```text
PR:            #140
Branch:        agent/issue-42-fnd-04-final-code
Base SHA:      38c07e210fe4e8689f1d8aeabbb07b92610d1826
Head SHA:      99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
Commits:       1
Changed files: 25
Diff:          +1149 / -1
Duration:      29 minutes (explicit Agent record)
```

PR #140はsnapshot lock時点でOPEN / DRAFT / UNMERGED。

Coordinator pre-review gateでは、selection application、C8-M01 regression、scope boundary、real PostgreSQL verification構造をGitHub一次証拠から再確認し、review工程を止めるBlocker / Majorは検出していない。

これは**merge-ready判定ではない**。

Canonical Final Synthesis snapshot:

- `results/final-synthesis-snapshot.md`
- `results/final-synthesis-snapshot.json`
- Revision: `fnd04-final-synthesis-snapshot-v1`

## Final Synthesis CI identity

Known run:

```text
Build and Test #427
Run ID: 31350916189
Conclusion: SUCCESS
```

成功step:

- restore
- local tool restore
- build
- pending-model check
- non-PostgreSQL tests
- real PostgreSQL tests

Observed CI result:

- build: warnings 0 / errors 0
- non-PostgreSQL: Unit 4 / 4 + Integration 38 / 38
- real PostgreSQL: 23 / 23

Identity nuance:

- runはPR Head `99cee438...`に関連付く
- actual checkout logはGitHub pull-request merge ref `d12de2ae07003a10d19d576808cf88ec7796da23`
- merge refはexact Head `99cee...`をexact Base `38c07...`へmergeした状態
- available connectorではseparate direct-head push-runを独立解決できていない

したがってこのrunを「PR merge-state CI SUCCESS」と記録し、direct branch-Head checkout CIと混同しない。

## Role-diverse independent review

Run identity:

```text
BENCHMARK_ID: fnd04-final-synthesis-independent-review
RUN_ID:       fnd04-final-review-20260810
POOL:         fnd04-reviewer-pool-v2
PROMPT:       fnd04-final-review-v1
RAW CAPTURE:  0 / 5
```

### Reviewer pool revision 2

Reviewer実行開始前、raw capture `0 / 6`の時点でpoolを改訂した。旧poolのClaude Sonnet 5 / Claude CodeとGPT-5.6 Luna / Open Codeを外し、Grok 4.5固定も廃止した。

現在のpool:

| Slot | Expected Model + Harness | Primary role |
| --- | --- | --- |
| R1 | GPT-5.6 Sol / Codex | runtime / failure-path |
| R2 | Claude Opus 5 / Claude Code | deep technical / test assurance |
| R3 | GPT-5.6 Luna / Codex | specification / scope |
| R4 | ChatGPT Opus 5.6 Sol / Browser | framework / official-source cross-check |
| R5 | Cursor Auto / Cursor | fast independent review / practical broad scan |

Policy:

- Claude Opus 5は高コストでも、deep technical / test assuranceへピンポイント投入する。
- Claude Sonnet 5はreview poolでは使用しない。
- Open Codeはこのreview phaseでは使用しない。
- GPT-5.6 LunaはCodexでspecification / scopeを担当する。
- Cursorは特定モデル固定ではなく標準Auto modeを実務review枠として使う。
- 6枠を維持するためだけの冗長reviewerは追加せず、5 reviewerでraw capture completeとする。

Revision record:

- `review-benchmark/reviewer-pool-revision-2.md`
- authoritative review run control: `review-benchmark/run.json` schema 1.1

Top-level `run.json` schema 1.9に残る初期reviewer list / 6-of-6記述は、reviewer poolについてのみこのRevision 2でsupersedeされる。candidate / implementation / selection / Final Synthesis snapshot情報は変更しない。

Exact product-visible model identity / effortは各execution直前に再確認する。silent substitutionは禁止。

Cursor Autoは特定modelのreview結果として扱わない。実際のrouted modelがproduct上で表示される場合のみ追加記録し、表示されない場合は推測しない。

Common reviewer prompt:

- `prompts/final-synthesis-independent-review.md`
- Revision: `fnd04-final-review-v1`

Review run control:

- `review-benchmark/README.md`
- `review-benchmark/run.json`
- `review-benchmark/reviewer-pool-revision-2.md`

### Independence boundary

reviewerは次を見ない。

- candidate ranking / score
- Implementation Evaluation
- Selection / Adjudication
- 他reviewer結果
- Gold / Judge結果

raw reviewer outputはチャット等からCollectorがMarkdown + JSON pairとして保存し、semantic editingしない。

Gold / Reference Reviewの内容はraw reviewer capture前にreviewer-visible directoryへ公開しない。Coordinator snapshotはGoldではない。

Controlled Mutantはcurrent real-target runでは開始していない。

## Judge quorum

- Judge A: GPT-5.6 Sol / Codex
- Judge B: Claude Opus 5 / Claude Code
- Conditional Judge C: GPT-5.6 Pro / Browser

Judge Cはfirst two Judgesがreference verdict、blocking root cause、merge-ready判断で不一致の場合のみ使用する。

## Experiment shape

```text
8 candidates
  H0 implementation snapshot
    -> Formal Self-Review
      -> H1 self-review fix snapshot
        -> implementation evaluation        [COMPLETE / LOCKED]
          -> selection / adjudication        [COMPLETE / LOCKED]
            -> curated Final Synthesis       [COMPLETE / LOCKED]
              -> role-diverse independent review [NEXT / 0 OF 5]
                -> 2 Judges (+ 1 conditional tie-breaker)
                  -> Formal Agent B review
```

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
Final Synthesis prompt:         fnd04-final-synthesis-v1
Final Synthesis snapshot:       fnd04-final-synthesis-snapshot-v1
Final review prompt:            fnd04-final-review-v1
Reviewer pool:                  fnd04-reviewer-pool-v2
```

## Key files

- `run.json`: overall machine-readable benchmark phase state（reviewer pool v1部分はrevision 2でsuperseded）
- `scoring.md`: H0/H1 scoring rubric
- `reference/assumption-ledger.md`: pre-locked external assumptions
- `reference/evaluator-probes.md`: evaluator-only probes
- `prompts/final-synthesis.md`: Final Synthesis implementation prompt
- `prompts/final-synthesis-independent-review.md`: role-diverse independent review prompt
- `results/implementation-evaluation.md/json`
- `results/selection-adjudication.md/json`
- `results/final-synthesis-snapshot.md/json`
- `review-benchmark/README.md`
- `review-benchmark/run.json`: authoritative current reviewer pool
- `review-benchmark/reviewer-pool-revision-2.md`

## Gate boundary

Final Synthesis snapshotまでcomplete / locked。

**次工程はR1-R5の独立レビューraw capture。**

この状態はPR #140 Ready化、merge、Issue #42 closeを許可しない。role-diverse review → Judge → Formal Agent Bを経て初めてproduct merge gateを評価する。
