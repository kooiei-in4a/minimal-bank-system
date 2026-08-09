# FND-03 Complete Experiment Archive

Target Issue: #41 `[FND-03] 実PostgreSQL integration test基盤を確立する`

Status: **COMPLETE / ARCHIVED**

このページは、FND-03で実施した実装比較、独立レビュー、Major修正比較、Judge裁定、最終production outcomeを一つのcanonical archiveとして辿るためのentry pointである。各stageは目的とscore semanticsが異なるため、ランキングを相互に混ぜない。

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
PR #104 merge / post-merge CI SUCCESS
    ↓
Issue #41 CLOSED / COMPLETED
    ↓
28 annotated candidate snapshots verified
    ↓
27 canonical candidate PRs CLOSED / unmerged
    ↓
28 candidate working branches deleted
```

## Stage index

### Stage 1 — Initial implementation benchmark

- [`summary.md`](./summary.md)
- [`implementation-evaluation.md`](./implementation-evaluation.md)
- [`archive-manifest.json`](./archive-manifest.json)
- Common base: `95a8e50e6b68025e3386fdd0672bd73bcbaa60a0`
- 14 candidates: 13 scored、MiniMax M3 / Open Codeは `stopped / no-change`
- Initial rank 1: GPT-5.6 Sol / Codex — `96 / 100`
- PR #91→#94、#92→#95はbranch-name correctionによるsuperseded PRであり、候補数へ二重計上しない。

### Stage 2 — Curated Final Synthesis provisional evaluation

- [`final-synthesis/README.md`](./final-synthesis/README.md)
- [`final-synthesis/provisional-summary.md`](./final-synthesis/provisional-summary.md)
- [`final-synthesis/provisional-evaluation.md`](./final-synthesis/provisional-evaluation.md)
- Source PR: #105
- Historical score: `98 / 100`
- Status: **SUPERSEDED / HISTORICAL**

`98 / 100`はMajor発見前の当時の評価として保存する。現在のmerge-readiness verdictとして使用しない。

### Stage 3 — 17-model independent review benchmark

- [`review-benchmark/run.json`](./review-benchmark/run.json)
- [`review-benchmark/manifest.json`](./review-benchmark/manifest.json): raw capture integrity manifest
- [`review-benchmark/collector-results.json`](./review-benchmark/collector-results.json): post-hoc Collector score / blocking-Gold alignment
- [`review-benchmark/full-evaluation.md`](./review-benchmark/full-evaluation.md)
- [`review-benchmark/gold-review.md`](./review-benchmark/gold-review.md)
- [`review-benchmark/gold-review.json`](./review-benchmark/gold-review.json)
- Raw reviews: [`review-benchmark/reviews/`](./review-benchmark/reviews/)
- 17 Markdown / 17 JSONを保存。raw内容は変更していない。

### Stage 4 — Post-hoc Gold / Major discovery

完全blindな事前locked Goldではない。最初のReference lock後にTestcontainers 4.13.0一次sourceを追加突合し、Majorを明確化したpost-hoc adjudicationである。

- Final technical Gold: `REQUEST CHANGES / NOT MERGE READY`
- Blocker 0 / Major 1 / Minor 1
- G-01: disposed-state latch / same-instance retry no-op
- G-02: digest assertionのdaemon-side evidence不足

### Stage 5 — 14-model Major-fix implementation benchmark

- [`final-fix/README.md`](./final-fix/README.md)
- [`final-fix/run.json`](./final-fix/run.json)
- Common base: `91e3fca181558cd1523390347f4f2f80d6014d26`
- 14 / 14 exact Head CI: SUCCESS

### Stage 6 — 3-Judge Major-fix adjudication

- [`final-fix/final-evaluation.md`](./final-fix/final-evaluation.md)
- [`final-fix/judges/manifest.json`](./final-fix/judges/manifest.json)
- [`final-fix/judges/synthesis.md`](./final-fix/judges/synthesis.md)
- Judge count: 3
- Final rank 1: GPT-5.6 Sol / Codex / PR #108 — `94 / 100`
- Merge-ready: `1 / 14`
- D-02 partial-create / ID-unavailable riskを含め、raw Judge scoreを単純平均せず一次証拠で裁定した。

### Stage 7 — Final production outcome

- [`final-outcome.md`](./final-outcome.md)
- Final Fix Head: `31e957e88d93e0e81fdc97eac7ba65dbd7ca3039`
- Merge commit: `6c5534fdb72e76d6ef5c3268cdb8558d7f344e7a`
- PR #104: MERGED
- Agent B: APPROVE / Blocker 0 / Major 0 / Minor 0 / Nit 0
- Post-merge CI `31301204377`: SUCCESS
- Issue #41: CLOSED / COMPLETED

## Archive completion evidence

Candidate archiveは`docs/benchmarks/archive-conventions.md`の順序に従って完了した。

```text
candidate identity verification
→ annotated tag creation
→ remote tag dereference verification
→ benchmark completion record
→ candidate PR unmerged Close
→ candidate working branch deletion
→ final remote verification
```

Final state:

- Annotated benchmark tags: **28 / 28 verified**
  - initial implementation: 14
  - Major-fix: 14
- Canonical candidate PRs: **27 / 27 CLOSED / unmerged**
  - initial MiniMax no-change candidateはPRなし
- Superseded historical PR #91 / #92: CLOSED / unmerged
- Candidate working branches: **28 / 28 deleted**
- `agent/issue-41-fnd-03-final-code`: preserved
- Final verification: GitHub Actions run `31305415766` SUCCESS
- Re-verification / temporary operator branch cleanup: run `31305570995` SUCCESS
- Temporary archive operator removal: PR #126 / merge `26cc3238e77ba3682aab842f78ee010bbec61c2d`

各candidateのfull Head、PR、CI、score、tag、Selected、final dispositionは[`archive-manifest.json`](./archive-manifest.json)を参照する。

## Historical integrity

- 初期implementation benchmarkのscoreを後続benchmarkで上書きしない。
- provisional `98 / 100`を削除せず、SUPERSEDEDとして保存する。
- raw reviewer / Judge artifactの意味内容を変更しない。
- post-hoc Goldを完全blind pre-locked Goldとは表現しない。
- `GPT-5.6 Pro / Browser / Pro`などartifact自身のidentityを維持する。
- Final production implementationはModel candidateランキングへ追加しない。

FND-03のbenchmark archiveはこれで完了している。
