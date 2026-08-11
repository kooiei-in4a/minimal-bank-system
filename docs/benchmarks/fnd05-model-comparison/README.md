# FND-05 Model Comparison — Pre-Run Preparation

Target Issue: #43 `[FND-05] Docker Compose実行基盤を確立する`

Status: **D-01〜D-08 LOCKED / GATE-ORDER TARGETED RE-REVIEW REQUIRED / IMPLEMENTATION PROHIBITED**

このdirectoryはFND-05実装開始前に、ADR・Issue・実装設計・test oracle・Project Rule・review責任を固定する。

Benchmark文書は製品仕様、Accepted ADR、Issue #43を上書きしない。

## Mutable-state source of truth

Mutableなrun identity / gate / decision / stage artifact stateの正本は`run.json`。

D-01〜D-08の可読なlock正本は`reference/pre-run-decision-locks.md`、local evidenceは`evidence/local-pre-lock-evidence-20260811.md`。

## Current gate

```yaml
PSR005_TARGETED_RE_REVIEW:
  BLOCKER: 0
  MAJOR: 0
  RESULT: PASS

D_01_TO_D_08:
  STATUS: LOCKED

GATE_ORDER_ALIGNMENT:
  FINDING: FND05-GATE-001
  FIX: fnd05-issue-ready-review-v3
  TARGETED_RE_REVIEW: REQUIRED

ISSUE_READY:
  STATUS: NOT_YET_PASSED

KOO_START_AUTHORIZATION:
  STATUS: NOT_GRANTED

IMPLEMENTATION_PERMITTED:
  false
```

## Final process shape

```text
Product authority / D-01..D-08 lock
  ↓
Issue #43 current-contract sync
  ↓
fresh Issue Ready PASS
  ↓
Koo explicit start authorization
  ↓
common base / 3 candidate branches / Draft PRs / exact execution identity preparation
  ↓
pre-execution identity verification
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

Candidate branch / Draft PRをIssue Readyより前に要求しない。Issue Ready PASSだけでも作成せず、Koo explicit start authorization後にpre-execution preparationとして作成する。

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

## Prompt-suite review history

Initial prompt suite remediation and `FND05-PSR-005` targeted re-review are complete.

```text
PSR-005 Reviewed Head:
6c626451fc7d8059e468d19afbfc3c80b666acb9

Direct-head CI:
31443292973 — SUCCESS

Reviewer:
GPT-5.6 Sol / Codex / xHigh

FND05-PSR-005 = FIXED
Blocker 0 / Major 0
```

その後、D-lock後のfresh Issue Ready準備で、Issue Ready promptがcandidate branch/common baseをIssue Ready前提として要求する循環を発見した。

```text
FND05-GATE-001
Issue Ready requires candidate branches
but fixed process creates candidate branches only after
Issue Ready PASS + Koo authorization
```

`issue-ready-review.md` v3で、Issue Readyとpost-authorization pre-execution preparationを分離した。現在はこのchanged surfaceだけのtargeted re-review待ちである。

## D-01〜D-08 lock summary

Full contract: `reference/pre-run-decision-locks.md`

```text
D-01: Compose >= 2.38.2 + required feature set
D-02: exact digest-qualified PostgreSQL/.NET images
D-03: host env -> Compose secret -> mounted secret file / explicit grant
D-04: canonical down/up lifecycle with migration-gate re-evaluation
D-05: ps JSON + docker inspect + migration history + Compose labels
D-06: deterministic mutation precondition/barrier/signature contract
D-07: linux/amd64; Ubuntu 24.04; Bash >=5.2; jq >=1.7; LF shell assets
D-08: GPT-5.6 Terra / Codex / xHigh Final Synthesis
```

D-08 exact identityがFinal Synthesis開始時に利用不能なら、silent substitutionせず停止してre-lockする。

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

Dedicated `postgres` / `migrator` / `api` service names、exact Compose condition、exact file path等はreference design / conventionであり、別lockでMUST化されていない限り独立ACへ昇格しない。

## Mutation model

CandidateにはM-01〜M-10のprotected contract、observable property、deterministic precondition property、barrier/fixture class、expected/invalid failure-signature classを開示する。

Evaluatorのexact injection patch / exact source editはcandidateへ開示しない。

Final Synthesisでは原則M-01〜M-10をすべて実行し、

```text
baseline GREEN
→ deterministic precondition PASS
→ one controlled defect
→ RED for expected signature
→ revert
→ GREEN
→ residue 0
```

を確認する。

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

## Start boundary

D-01〜D-08 lockは完了しているが、`FND05-GATE-001` targeted re-reviewが完了するまでfresh Issue Ready Gateへ進まない。

```text
FND05-GATE-001 targeted re-review B0/M0
↓
current Head CI SUCCESS
↓
Issue #43 current-contract sync確認
↓
fresh Issue Ready Review
↓
Issue Ready PASS
↓
Koo explicit start authorization
↓
candidate preparation
↓
candidate execution
```

現時点ではFND-05 implementationは禁止。
