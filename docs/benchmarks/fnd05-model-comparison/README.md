# FND-05 Model Comparison — Pre-Run Preparation

Target Issue: #43 `[FND-05] Docker Compose実行基盤を確立する`

Status: **PREPARATION DRAFT / IMPLEMENTATION PROHIBITED**

このdirectoryは、FND-05の実装開始前に、ADR・Issue・実装設計・テスト設計・project rule・review責任を固定する。

Benchmark文書は製品仕様、Accepted ADR、Issue #43を上書きしない。矛盾する場合は上位正本を優先し、実装を停止する。

## Final process shape

```text
ADR / Issue / Implementation Design / Test Design lock
  ↓
3 independent implementations
  - GPT-5.6 Luna / Codex
  - Claude Sonnet 5 / Claude Code
  - Grok 4.5 / Cursor
  ↓
common evaluation / element-level Selection
  ↓
curated Final Synthesis
  ↓
static project-rule checks
  ↓
2 lightweight reviews
  - Composer 2.5 / Cursor: project quality / rules
  - GPT-5.6 Luna / Codex: ADR / Issue / AC conformance
  ↓
light findings fix / CI / Final Head lock
  ↓
2 heavy final reviews — 原則各1回
  - GPT-5.6 Sol / Codex: architecture / contract
  - Claude Opus 5 / Claude Code: failure / lifecycle / false assurance
  ↓
B0 / M0
  ├─ YES → merge gate
  └─ NO  → targeted fix / blast-radius-based re-review
```

OpenCodeは使用しない。

## Self-review policy

独立したFormal Self-Review phaseは実施しない。

Agent Aの基本的なdiff確認・検証は、`prompts/implementation.md`へ事前定義したCompletion Checksとして組み込む。

- 「自由にセルフレビューせよ」と指示しない。
- review観点、prohibited pattern、required evidence、mutationを実装開始前に固定する。
- H0 → Formal SR → H1という別実行は作らない。

## Implementation candidates — 3

| Slot | Model + Harness | Effort target | Purpose |
| --- | --- | --- | --- |
| C1 | GPT-5.6 Luna / Codex | xHigh相当、実行前にexact label確認 | ADR・Issueへ忠実な基準実装 |
| C2 | Claude Sonnet 5 / Claude Code | xHigh相当、実行前にexact label確認 | 別系列の設計・実装解釈 |
| C3 | Grok 4.5 / Cursor | high。high fastは使用しない | 別Harness・Compose運用経路の異質性 |

実行直前にproduct-visible model labelとeffortを再確認し、`run.json`へ固定する。変更・利用不能なlabelを黙って代替しない。

## Light reviews — 2

| Slot | Model + Harness | Checks |
| --- | --- | --- |
| L1 | Composer 2.5 / Cursor | code quality、project rule、配置、Compose構造、明白な設定・secret・scope問題 |
| L2 | GPT-5.6 Luna / Codex | ADR → Issue → AC → implementation → test → evidenceのtraceability |

Light Reviewはmerge可否の最終判断ではない。Heavy reviewerへ明白な問題を持ち込まないための前処理である。

## Heavy final reviews — 2

| Slot | Model + Harness | Checks | Full-review budget |
| --- | --- | --- | ---: |
| H1 | GPT-5.6 Sol / Codex | architecture、ADR、責務境界、Issue本質 | 1 |
| H2 | Claude Opus 5 / Claude Code | failure、lifecycle、ordering、secret、false assurance | 1 |

Heavy promptには、確認する項目と**原則確認しない項目**を同じ強さで明記する。style、軽微な命名、formatter、README typo、単純な配置・version照合等はLight Gateの責任とする。

## Conditional Judge

Judgeは通常工程へ置かない。次の場合だけ発動する。

- Sol / OpusでBlocker・Major有無が割れる
- root causeまたはrequired fix方向が割れる
- merge readinessが割れる
- 両者が同じ未検証assumptionへ依存している

Judgeのexact identityはtrigger時にfresh context・非author・非reviewer優先で固定する。

## Pre-run gates

- [ ] FND-04 final retrospective PR #144がreview済み
- [ ] Issue #43のdependency #42 COMPLETE / MERGEDを再確認
- [ ] Issue #43のGate statusを現在状態へ更新
- [ ] `reference/assumption-ledger.md`の`TO_LOCK`が0
- [ ] `reference/implementation-and-test-design-contract.md` locked
- [ ] `reference/project-rule-catalog.md` locked
- [ ] `reference/review-perspective-matrix.md` locked
- [ ] `reference/mandatory-mutations.md` locked
- [ ] `scoring.md` locked
- [ ] implementation / evaluation / selection / final synthesis prompt revisions locked
- [ ] light / heavy / fix / re-review prompt revisions locked
- [ ] exact common base full SHA fixed
- [ ] 3 candidate branches created from exact common base
- [ ] 3 / 3 branch Head identity verified
- [ ] 3 Draft PR事前作成
- [ ] exact model / harness / effort fixed
- [ ] candidate output 0件を確認
- [ ] Issue #43 Issue Ready = PASS
- [ ] Kooがcandidate execution開始を許可

全項目を満たすまでimplementationを開始しない。

## Files

### State / scoring

- `run.json`: machine-readable pre-run state
- `pre-run-checklist.md`: human-readable completion checklist
- `scoring.md`: 3 candidate共通の評価基準

### Reference

- `reference/assumption-ledger.md`: Docker / project assumptions and open decisions
- `reference/implementation-and-test-design-contract.md`: ADR起点の実装・test設計
- `reference/project-rule-catalog.md`: MUST / MUST NOT / placement rule
- `reference/review-perspective-matrix.md`: review責任分担と非対象
- `reference/mandatory-mutations.md`: Final Synthesisで必ず検証するmutation

### Implementation / synthesis

- `prompts/implementation.md`: 3 candidate共通実装prompt
- `prompts/implementation-evaluation.md`: 3 candidate比較
- `prompts/selection-adjudication.md`: element-level selection
- `prompts/final-synthesis.md`: current mainからのcurated実装

### Light gate

- `prompts/light-review-project-quality.md`: Composer用
- `prompts/light-review-contract-conformance.md`: Luna用
- `prompts/light-findings-fix.md`: Light finding disposition / fix

### Heavy gate / conditional path

- `prompts/heavy-review-sol.md`: Sol architecture final gate
- `prompts/heavy-review-opus.md`: Opus adversarial final gate
- `prompts/conditional-judge.md`: Heavy disagreement時のみ
- `prompts/targeted-fix.md`: locked Blocker / Majorの最小修正
- `prompts/targeted-re-review.md`: finding-owned re-review

### Gate

- `prompts/issue-ready-review.md`: candidate開始前のfresh gate review

## External behavior assumptions

Docker公式文書により、Composeはshort syntaxの`depends_on`だけではdependencyのready状態を待たない。`service_healthy`と`service_completed_successfully`は実装手段として使用できるが、FND-05ではCompose定義の存在だけを検証証拠にせず、container state、exit code、timestamp、migration historyを外部観測する。

- https://docs.docker.com/compose/how-tos/startup-order/
- https://docs.docker.com/reference/compose-file/services/
- https://docs.docker.com/reference/cli/docker/compose/ps/
- https://docs.docker.com/reference/cli/docker/compose/config/
- https://docs.docker.com/compose/how-tos/use-secrets/

## Start boundary

このpreparationがreview・mergeされ、そのmerge commitをcommon baseとして固定した後に、3 candidate branchとDraft PRを事前作成する。

branch / PR作成後もcandidate executionは開始しない。Issue #43 Issue Ready PASSとKooの明示的開始指示をもって開始する。
