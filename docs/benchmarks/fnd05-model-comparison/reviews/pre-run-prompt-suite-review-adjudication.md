# FND-05 Pre-Run Prompt Suite — Review Adjudication

```yaml
DOCUMENT_STATUS: "PRELIMINARY ADJUDICATION — CODEX FULL ARTIFACT PENDING"
TARGET_PR: 145
TARGET_HEAD: "57df6ae1a30ac23151fbcd707f191f5d26dba029"
TARGET_FILES: 22
CURRENT_VERDICT: "FIX_REQUIRED"
D_LOCK_PERMITTED: false
ISSUE_READY_REVIEW_PERMITTED: false
FND05_IMPLEMENTATION_PERMITTED: false
```

## 1. Review registry

### R1 — GPT-5.6 Sol / Browser / xHigh

```yaml
VERDICT: FIX_REQUIRED
BLOCKER: 0
MAJOR: 5
MINOR: 4
NIT: 0
SNAPSHOT_COMMIT: "8131dba42d3b24999e7a5395bb80e2fbabac13a3"
SNAPSHOT_BRANCH: "agent/fnd05-prompt-suite-review-browser-snapshot"
ORIGINAL_PATH: "docs/benchmarks/fnd05-model-comparison/reviews/pre-run-prompt-suite-independent-review.md"
```

### R2 — GPT-5.6 Sol / Codex / xHigh

```yaml
VERDICT: FIX_REQUIRED
BLOCKER: 1
MAJOR: 4
MINOR: 2
NIT: 0
LOCAL_COMMIT_REPORTED: "83b5099c43e5ad87c10f3512205985bb221c11c6"
REMOTE_ARTIFACT: "PENDING"
OUTPUT_BRANCH: "agent/fnd05-prompt-suite-review-codex"
OUTPUT_PR: 147
OUTPUT_PATH: "docs/benchmarks/fnd05-model-comparison/reviews/pre-run-prompt-suite-independent-review-gpt-5.6-sol-codex-xhigh.md"
```

Codex完成reviewはremoteへ未pushのため、現時点ではuser共有のsummaryだけを使用する。Blockerの詳細は推測しない。

### R3 — Grok 4.5 / Cursor / High

```yaml
VERDICT: FIX_REQUIRED
BLOCKER: 0
MAJOR: 6
MINOR: 4
NIT: 1
SNAPSHOT_COMMIT: "75230e1138e4a635551d0de61d663c63085369ae"
SNAPSHOT_BRANCH: "agent/fnd05-prompt-suite-review-grok-snapshot"
ORIGINAL_PR: 146
ORIGINAL_PATH: "docs/benchmarks/fnd05-model-comparison/reviews/pre-run-prompt-suite-independent-review.md"
```

## 2. Preservation decision

レビュー成果物を上書きして1本へ集約しない。

- Browser reviewはcommit / snapshot branchで保存する。
- Grok reviewはcommit / snapshot branchで保存する。
- Codex reviewはPR #147の専用fileへ保存する。
- Adjudicationは別fileでroot causeを正規化する。

Reviewer間の件数差を多数決へ使用しない。root cause、failure path、required fixを比較する。

## 3. High-confidence normalized findings

BrowserとGrokが独立に同じroot causeへ到達し、Codex summaryとも整合するものをP0とする。

### N-01 — Draft implementation shape has been promoted above Issue authority

```yaml
SEVERITY: Major
CONFIDENCE: HIGH
SUPPORTED_BY:
  - Browser F-01
  - Grok F-01
  - Codex summary: authority / canonical-source concern
```

#### Root cause

Issue #43はone-shot Migrator serviceまたは同等のCompose正本経路を許可する。一方、draft suiteはexact 3-service topology、exact service names、exact condition mechanism、exact file placement、restart semantics、追加hardeningの一部をMUST化している。

#### Impact

- Issue / ADR適合candidateをfalse rejectする
- candidate比較をunapproved draft shapeへの適合試験に変える
- D-03 / D-04等のopen decisionを事前に漏らす

#### Required fix

MUSTをobservable contractへ限定する。

- PostgreSQL usable before migration
- explicit Migrator failure is non-zero
- successだけがAPI startを許可
- failure時API non-start
- API no-auto-migration
- named volume
- image identity pinning
- secret non-disclosure
- reproducible lifecycle

Exact topology、names、condition mechanism、placement、hardeningは次のいずれかにする。

1. SHOULD / preferred conventionへ降格する。
2. Kooがcandidate-common shapeとして明示lockするD-09を追加する。

D-09は自動追加・推測lockしない。

### N-02 — M-08 mutates the oracle instead of the runtime defect

```yaml
SEVERITY: Major
CONFIDENCE: HIGH
SUPPORTED_BY:
  - Browser F-02
  - Grok F-02
  - Codex summary: mutation oracle concern
```

#### Root cause

M-08はmigration-history assertionを削除または常時successへし、そのtestがREDになることを期待している。検出器自体を壊しているため、defect-class mutationとして成立しない。

#### Required fix

oracleを変更しない。

Runtime / Migrator pathだけを一時変更し、exit 0のまま期待migration rowを記録しない状態を作る。unchanged history oracleがREDになることを確認する。

Exact injection mechanismはD-06でlockする。

### N-03 — Rejected or unresolved Light B/M can bypass Heavy Review

```yaml
SEVERITY: Major
CONFIDENCE: HIGH
SUPPORTED_BY:
  - Browser F-03
  - Grok F-03
  - Codex summary: Light closure concern
```

#### Root cause

Light fixerはB/M candidateをREJECTEDまたはUNRESOLVEDにできる一方、HeavyはLight findingを繰り返さないと読める。誤ったAuthor rejectionがHeavy blind spotになる。

#### Required fix

Light fix outputへ必須handoffを追加する。

```text
HEAVY_HANDOFF:
- resolved_and_verified_findings:
- rejected_or_unresolved_blocker_major_candidates:
- escalated_blocker_major_candidates:
- evidence_incomplete_findings:
```

Heavyのnon-goalは`ACCEPTED + FIXED + VERIFIED Minor/Nit`へ限定する。

REJECTED / UNRESOLVED / ESCALATED / evidence-incomplete B/M candidateが自身のprimary scopeへ入る場合、Sol / Opusは独立再確認する。

### N-04 — Authority and gate evidence are mixed

```yaml
SEVERITY: Major
CONFIDENCE: HIGH
SUPPORTED_BY:
  - Browser F-05
  - Grok F-04
  - Codex summary: source-of-truth order concern
```

#### Root cause

一部promptの`Authority`番号リストでParent #3 / WP #33がApproved specification / ADR / Issueより上に見える。Parent / WPはphase・gate・progress evidenceであり、製品設計の正本ではない。

#### Required fix

全実行promptで次へ統一する。

```text
Product authority:
1. Koo-approved product policy / approved specification
2. Accepted ADR
3. Target Issue #43
4. AGENTS.md governance
5. Locked FND-05 benchmark contracts
6. PR descriptions / model self-report

Gate / current-state evidence:
- Parent Issue #3
- WP-1 Issue #33
- dependency / Issue Ready / Koo start authorization
```

矛盾時は上位正本を優先して停止する。

### N-05 — Open decisions leak preferred or inconsistent answers

```yaml
SEVERITY: Major_or_Minor_pending_Codex
CONFIDENCE: HIGH
SUPPORTED_BY:
  - Browser F-06
  - Grok F-05 / F-09
  - Codex summary: start / authority / lock concerns
```

#### Root cause

- D-03へpreferred secret designを先書き
- D-08へdefault Luna / Codex authorを先書き
- D-05がfile間でAPI timestampだけ / full state captureに分裂
- D-02とP-07でimage identityを二重管理
- D-04でcommandはopenだがrestart semanticsの一部をMUST化

#### Required fix

D entryはquestion / required evidence / locked valueだけにする。

```yaml
status: TO_LOCK
question: "..."
required_evidence: []
draft_note_non_binding: null
locked_value: null
evidence_refs: []
```

- D-02へPostgreSQL + .NET image identitiesを統合
- D-05をMigrator exit/finish、API state/start、history、project identityを含むfull external state captureへ拡張
- D-08のdefault authorを削除
- D-04をcommand stringsとrestart semanticsへ分解するか、semanticsを明示lockする

### N-06 — Locked stage artifacts have no immutable identity contract

```yaml
SEVERITY: Major
CONFIDENCE: HIGH
SUPPORTED_BY:
  - Browser F-04
  - Grok F-06
  - Codex summary: artifact lock concern
```

#### Root cause

Evaluation、Selection、Light、Heavy、Judge、targeted fixが`<LOCKED>`やrevision labelだけで連携し、artifact path、content hash、target Head、producer identityを共通schemaで固定していない。

#### Impact

fresh harnessがstale Evaluationや別HeadのLight findingを読み、wrong-target fixを行える。

#### Required fix

`run.json`へ`stage_artifacts` registryを追加する。

```json
{
  "stage": {
    "artifact_path": null,
    "content_sha256": null,
    "prompt_revision": null,
    "target_head_sha": null,
    "source_artifact_refs": [],
    "producer_slot": null,
    "producer_commit_sha": null,
    "status": "not_started"
  }
}
```

後続promptはexact artifact refとtarget Head bindingを検証してから内容を読む。

## 4. High-confidence P1 findings

### N-07 — Completion Checks permit checklist theater

```yaml
SEVERITY: Minor
SUPPORTED_BY:
  - Browser Self-Review replacement assessment
  - Grok F-07
```

Independent Formal Self-Reviewは復活させない。

Implementation C-01〜C-11を、少数のevidence-backed DoDへ圧縮する。各PASSにartifact path / command / runtime observationを必須化する。Project Rule全件再監査はStatic / primary Light ownerへ委譲する。

### N-08 — Rule ownership is contradicted by full L1 catalog review

```yaml
SEVERITY: Minor
SUPPORTED_BY:
  - Browser F-07
  - Grok F-08
```

Primary ownerがfull PASS/FAIL/N/Aを行う。ComposerはComposer-owned rulesとobvious cross-cutting escapeを確認し、Static / Luna / Heavy-owned ruleを全件再採点しない。Scope rulesへprimary ownerを付与する。

### N-09 — Mutation disclosure model is ambiguous

```yaml
SEVERITY: Minor
SUPPORTED_BY:
  - Browser F-08
```

Candidateへ開示するものを固定する。

- mutation ID
- protected contract
- required observable oracle property

Evaluator-only injection recipeはcandidate promptへ含めない。

Full recipeを開示する場合は、general mutation sensitivityではなくknown-mutation conformanceとしてmetricを明記する。

### N-10 — Mutable run state lacks a declared single source of truth

```yaml
SEVERITY: Minor
SUPPORTED_BY:
  - Browser F-09
  - Grok F-06
```

`run.json`をmutable run identity / decision / artifact / gate stateのSSOTとする。Markdownはfixed policyを説明し、mutable valueはrun keyから生成またはcopyする。更新順序を`run.json first`へ固定する。

## 5. Codex review unresolved item

Codex summaryはBlocker 1を報告している。

```text
開始権限、正本順序、artifact lock、Light closure、mutation oracleの修正が必要
```

正本順序、artifact lock、Light closure、mutation oracleはN-02 / N-03 / N-04 / N-06へ正規化できる。

**開始権限に関するBlockerのexact root cause、affected files、failure pathはfull Codex artifactなしでは確定しない。**

したがって次を禁止する。

- Codex Blockerを推測でcloseする
- P0修正だけでB0と宣言する
- D-01〜D-08 lockへ進む
- Issue Ready reviewへ進む

PR #147へfull Codex reviewを保存後、本Adjudicationを更新する。

## 6. Current verdict

```text
VERDICT: FIX_REQUIRED
KNOWN_BLOCKER: 1 reported by Codex, detail pending remote artifact
NORMALIZED_MAJOR: 6 high-confidence root causes
NORMALIZED_MINOR: 4 high-confidence root causes
D_LOCK_PERMITTED: NO
ISSUE_READY_REVIEW_PERMITTED: NO
FND05_IMPLEMENTATION_PERMITTED: NO
```

## 7. Repair order

### P0-A — Preserve all raw reviews

- Browser snapshot branch created
- Grok snapshot branch created
- Codex dedicated PR #147 created
- no overwrite

### P0-B — Obtain Codex full artifact

PR #147へ完成reviewをpushする。

### P0-C — Apply common Major fixes to PR #145

1. N-01 observable contract vs preferred implementation shape
2. N-02 M-08 redesign
3. N-03 Light→Heavy handoff
4. N-04 authority order
5. N-05 open decision integrity
6. N-06 immutable artifact registry

### P1 — Efficiency and evidence fixes

1. Completion Checks compression / evidence refs
2. rule owner enforcement
3. mutation disclosure
4. run.json SSOT

### P2 — Targeted re-review

- review N-01〜N-10 and Codex Blocker only
- do not rerun general review from scratch unless fix surface becomes cross-cutting

## 8. Fixed policy status

次のfunnel自体は3 reviewerで概ね支持されている。

- 3 independent implementation candidates
- no OpenCode
- no separate Formal Self-Review / H1
- predefined Completion Checks
- curated Final Synthesis
- Static + Composer + Luna
- fixed Head
- Sol + Opus Heavy each once
- Heavy non-goals
- conditional Judge
- finding-owned / blast-radius re-review

根本再設計は不要。Authority、contract level、artifact identity、handoff、mutation validityを修正する。

## 9. Operation confirmation

```text
TARGET_PR_145_CHANGED: NO
TARGET_22_FILES_CHANGED: NO
ISSUE_43_CHANGED: NO
FND05_IMPLEMENTATION_STARTED: NO
D_01_TO_D_08_LOCK_STARTED: NO
RAW_REVIEW_OVERWRITTEN: NO
```
