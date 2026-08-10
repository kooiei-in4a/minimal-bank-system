# FND-05 Model Comparison — Pre-Run Preparation

Target Issue: #43 `[FND-05] Docker Compose実行基盤を確立する`

Status: **PREPARATION FIX REQUIRED / IMPLEMENTATION PROHIBITED**

このdirectoryはFND-05実装開始前に、ADR・Issue・実装設計・test oracle・Project Rule・review責任を固定する。

Benchmark文書は製品仕様、Accepted ADR、Issue #43を上書きしない。

## Mutable-state source of truth

Mutableなrun identity / gate / decision / stage artifact stateの正本は`run.json`。

Markdown / executable promptは可読性のため値を再掲できるが、実行時は`run.json`のlocked value / evidence / artifact identityを照合する。

## Final process shape

```text
Product authority / D-01..D-08 lock
  ↓
Issue Ready PASS
  ↓
Koo explicit start authorization
  ↓
3 independent implementations
  - GPT-5.6 Luna / Codex
  - Claude Sonnet 5 / Claude Code
  - Grok 4.5 / Cursor high
  ↓
common evaluation
  ↓
element-level Selection / Adjudication
  ↓
curated Final Synthesis from current main
  ↓
S0 static gate
  ↓
L1 Composer project-quality review
  ↓
L2 Luna contract-conformance review
  ↓
Light finding fix / CI / Final Head lock
  ↓
H1 Sol architecture final review — 原則1回
  ↓
H2 Opus adversarial final review — 原則1回
  ↓
B0 / M0
  ├─ YES → merge gate
  └─ NO  → targeted fix / finding-owned re-review
```

JudgeはSol / OpusのBlocker・Major、root cause、required fix、merge readiness等が割れた場合だけconditionalに実行する。

OpenCodeは使用しない。

## Authority model

### Product authority

1. Koo-approved product policy / approved specification
2. Accepted ADR-0001 / ADR-0008 / ADR-0009
3. Issue #43
4. `AGENTS.md`
5. locked FND-05 contracts

### Gate / current-state evidence

- Parent Issue #3
- WP-1 Issue #33
- dependency #42
- Issue Ready
- Koo start authorization

Parent / WPをProduct authorityとして使用しない。

## Self-review policy

独立Formal Self-Review / H1 phaseは実施しない。

Implementation promptへ事前定義したevidence-backed Completion Checksを組み込む。

- 「自由にセルフレビューせよ」と指示しない
- Checkごとに一次証拠を要求する
- Author自己申告を最終証拠にしない
- rule catalog全件自己採点は要求せず、Static / Light / Heavyへ責任分離する

## Implementation candidates — 3

| Slot | Model + Harness | Effort |
| --- | --- | --- |
| C1 | GPT-5.6 Luna / Codex | exact labelを実行前lock |
| C2 | Claude Sonnet 5 / Claude Code | exact labelを実行前lock |
| C3 | Grok 4.5 / Cursor | high。high fast禁止 |

## Light reviews — 2

| Slot | Model + Harness | Primary responsibility |
| --- | --- | --- |
| L1 | Composer 2.5 / Cursor | Composer-owned code/config quality / Project Rule |
| L2 | GPT-5.6 Luna / Codex | ADR → Issue → implementation → test → evidence traceability |

L1は全catalogを再採点しない。S0 / Luna / Heavy-owned ruleは既存resultをconsumeし、Blocker / Major root cause候補だけescalateする。

## Heavy final reviews — 2

| Slot | Model + Harness | Primary responsibility | Full-review budget |
| --- | --- | --- | ---: |
| H1 | GPT-5.6 Sol / Codex | architecture / contract / responsibility | 1 |
| H2 | Claude Opus 5 / Claude Code | failure / lifecycle / false assurance | 1 |

Heavy reviewerが原則確認しないのは、Lightで**ACCEPTED + FIXED + VERIFIED**された低リスク項目。

REJECTED / UNRESOLVED / ESCALATED Blocker・Major候補は除外しない。Heavyのprimary scopeに入る場合、exact Final Headから独立再確認する。

## Observable contract vs implementation preference

Issue #43が必須とするobservable behaviorをMUSTとする。

- PostgreSQL 18 runtime / named volume
- PostgreSQL usable後にFND-04 Migratorを実行
- Migrator success後だけAPI start
- Migrator failure時API never-start
- API no-auto-migration
- digest pinning
- external secret / connection configuration
- deterministic lifecycle / external evidence

Dedicated `postgres` / `migrator` / `api` service names、exact Compose condition、exact file path等はreference design / conventionであり、pre-runでKooが共通shapeとしてlockしない限り独立ACへ昇格しない。

## Mutation model

CandidateにはM-01〜M-10のprotected contract / observable propertyを開示する。Evaluatorのexact injection recipeへ過学習させない。

Final Synthesisでは原則M-01〜M-10をすべて実行し、

```text
baseline GREEN
→ one controlled defect
→ RED for expected reason
→ revert
→ GREEN
→ residue 0
```

を確認する。

M-08はtest oracle自体を壊さず、Migratorがexit 0でもexpected migration stateを作らないruntime defectとして検証する。

## Immutable stage artifact handoff

Evaluation以降のstage artifactは次を持つ。

```text
artifact_path
content_sha256
prompt_revision
target_head_sha
source_artifact_refs
producer_slot
producer_commit_sha
```

`run.json.stage_artifacts`へ同じidentityを記録する。Downstream promptはexact refを照合してから読む。

## Open decisions before lock

D-01〜D-08は`run.json.open_decisions`で`locked_value = null`の間は未確定。

- D-01 minimum Compose version / features
- D-02 PostgreSQL + .NET exact image identities
- D-03 secret source / reader design
- D-04 lifecycle commands + semantics
- D-05 external state capture method
- D-06 failure injection override
- D-07 cross-platform contract
- D-08 Final Synthesis exact identity

Example / draft noteをcandidateへの必須answerとみなさない。

## Pre-run gates

- [ ] PR #144 review済み
- [ ] PR #145 3-review共通finding修正済み
- [ ] finding-owned targeted re-review B0 / M0
- [ ] D-01〜D-08 locked with evidence
- [ ] prompt / reference revision locked
- [ ] common base full SHA fixed
- [ ] 3 candidate branches / Draft PRs created from same base
- [ ] exact Model / Harness / Effort locked
- [ ] candidate output 0件
- [ ] Issue #43 Issue Ready PASS
- [ ] Koo explicit start authorization

Issue Ready PASSだけではcandidate executionを開始しない。

## Files

### State / scoring

- `run.json`
- `pre-run-checklist.md`
- `scoring.md`

### Reference

- `reference/assumption-ledger.md`
- `reference/implementation-and-test-design-contract.md`
- `reference/project-rule-catalog.md`
- `reference/review-perspective-matrix.md`
- `reference/mandatory-mutations.md`

### Prompts

- `prompts/implementation.md`
- `prompts/implementation-evaluation.md`
- `prompts/selection-adjudication.md`
- `prompts/final-synthesis.md`
- `prompts/light-review-project-quality.md`
- `prompts/light-review-contract-conformance.md`
- `prompts/light-findings-fix.md`
- `prompts/heavy-review-sol.md`
- `prompts/heavy-review-opus.md`
- `prompts/conditional-judge.md`
- `prompts/targeted-fix.md`
- `prompts/targeted-re-review.md`
- `prompts/issue-ready-review.md`

## Start boundary

Prompt-suite targeted re-review → D-01〜D-08 lock → Issue #43同期 → Issue Ready PASS → Koo明示開始許可、の順序を崩さない。

現時点ではFND-05 implementationは禁止。
