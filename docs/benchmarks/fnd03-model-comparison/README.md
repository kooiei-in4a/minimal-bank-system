# FND-03 Complete Experiment Archive

Target Issue: #41 `[FND-03] 実PostgreSQL integration test基盤を確立する`

Status: **COMPLETE / ARCHIVED**

このページは、FND-03で実施した7段階の実験・レビュー・実装結果を、候補ランキングと実際のproduction outcomeを混同せずに辿るためのcanonical entry pointである。

## Canonical timeline

```text
Initial implementation benchmark
    ↓
Final Synthesis PR #104 / Head 91e3fca
    ↓
17-model independent review benchmark
    ↓
post-hoc adjudication: REQUEST CHANGES / Major 1
    ↓
Testcontainers 4.13.0 cleanup Major confirmed
    ↓
14-model independent Major-fix benchmark
    ↓
3 Judge adjudication
    ↓
PR #108 architecture selected
    ↓
Final implementation Head 31e957e
    ↓
Agent B APPROVE / Blocker 0 / Major 0
    ↓
PR #104 merge
    ↓
post-merge CI SUCCESS
    ↓
Issue #41 CLOSED / COMPLETED
```

## Stage index

### Stage 1 — Initial implementation benchmark

初期実装候補のcanonical resultであり、後続のFinal SynthesisやMajor-fix scoreで上書きしていない。

- [`summary.md`](./summary.md)
- [`implementation-evaluation.md`](./implementation-evaluation.md)
- [`archive-manifest.json`](./archive-manifest.json)
- Common base: `95a8e50e6b68025e3386fdd0672bd73bcbaa60a0`
- 14 candidates: 13 scored、MiniMax M3 / Open Codeは `stopped / no-change`

### Stage 2 — Curated Final Synthesis provisional evaluation

PR #105の当時の評価を保存する。`98 / 100`は歴史的な実験結果であり、現在のmerge-readiness verdictではない。

- [`final-synthesis/README.md`](./final-synthesis/README.md)
- [`final-synthesis/provisional-summary.md`](./final-synthesis/provisional-summary.md)
- [`final-synthesis/provisional-evaluation.md`](./final-synthesis/provisional-evaluation.md)
- Source PR: [#105](https://github.com/kooiei-in4a/minimal-bank-system/pull/105)
- Status: **SUPERSEDED / HISTORICAL**

### Stage 3 — 17-model independent review benchmark

Final Synthesis Head `91e3fca...`を17組のModel + Agent/Harnessが独立レビューしたraw artifactと集計結果を保存する。

- [`review-benchmark/README.md`](./review-benchmark/README.md)
- [`review-benchmark/run.json`](./review-benchmark/run.json)
- [`review-benchmark/manifest.json`](./review-benchmark/manifest.json)
- [`review-benchmark/full-evaluation.md`](./review-benchmark/full-evaluation.md)
- [`review-benchmark/gold-review.md`](./review-benchmark/gold-review.md)
- [`review-benchmark/gold-review.json`](./review-benchmark/gold-review.json)
- Raw reviews: [`review-benchmark/reviews/`](./review-benchmark/reviews/)
- Source PR: [#106](https://github.com/kooiei-in4a/minimal-bank-system/pull/106)

### Stage 4 — Post-hoc Gold / Major discovery

完全blindな事前locked Goldではない。最初のReference lock後にTestcontainers 4.13.0一次sourceを追加突合して、Majorを明確化したpost-hoc adjudicationである。

- Final technical Gold: `REQUEST CHANGES / NOT MERGE READY`
- Blocker: 0 / Major: 1 / Minor: 1
- Major root cause: `G-01`
- [`gold-review.md`](./review-benchmark/gold-review.md)
- [`full-evaluation.md`](./review-benchmark/full-evaluation.md)

### Stage 5 — 14-model Major-fix implementation benchmark

- [`final-fix/README.md`](./final-fix/README.md)
- [`final-fix/run.json`](./final-fix/run.json)
- Common base: `91e3fca181558cd1523390347f4f2f80d6014d26`
- 14 / 14 exact Head CI: SUCCESS

### Stage 6 — 3-Judge Major-fix adjudication

- [`final-fix/final-evaluation.md`](./final-fix/final-evaluation.md)
- Raw Judge manifest: [`final-fix/judges/manifest.json`](./final-fix/judges/manifest.json)
- Raw Judge synthesis: [`final-fix/judges/synthesis.md`](./final-fix/judges/synthesis.md)
- Judge raw parts: [`final-fix/judges/`](./final-fix/judges/)
- Final rank 1: GPT-5.6 Sol / Codex, PR #108, `94 / 100`
- Merge-ready: `1 / 14`

### Stage 7 — Final production outcome

これはbenchmark rankingではなく、実際に採用・mergeされたproduction implementationの記録である。

- [`final-outcome.md`](./final-outcome.md)
- Final Fix Head: `31e957e88d93e0e81fdc97eac7ba65dbd7ca3039`
- Merge commit: `6c5534fdb72e76d6ef5c3268cdb8558d7f344e7a`
- [PR #104](https://github.com/kooiei-in4a/minimal-bank-system/pull/104): MERGED
- [Issue #41](https://github.com/kooiei-in4a/minimal-bank-system/issues/41): CLOSED / COMPLETED

## Historical integrity

- 初期implementation benchmarkのscoreはcanonical resultとして維持する。
- provisional `98 / 100`は削除せず、`SUPERSEDED / HISTORICAL`として保存する。
- 17 reviewer raw Markdown / JSONは内容を変更していない。
- 2件のraw JSON schema deviationはmanifestのcapture statusで保持する。
- post-hoc Goldを完全blind locked-Gold benchmarkとは表現しない。
- 3 Judgeのartifact identity、特に `GPT-5.6 Pro / Browser / Pro` を変更しない。
- Final production implementationは候補ランキングへ追加しない。

## Archive operation boundary

このarchiveはdocumentation / benchmark archiveのみを対象とする。candidate tag作成、candidate PRのclose、branch削除、Issue更新はarchive PR merge後の別工程で行う。
