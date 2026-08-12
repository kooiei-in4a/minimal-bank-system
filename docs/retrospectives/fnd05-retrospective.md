# FND-05 Retrospective

## Status

RETROSPECTIVE: IN PROGRESS

FND-05 product implementation, candidate archive, and pre-retrospective repository cleanup are complete.

This document is the working record for the FND-05 retrospective.

No FND-06 process change is authorized by this document alone.

```yaml
RETROSPECTIVE:
  STATUS: IN_PROGRESS

SECTION_A_DEVELOPMENT_FLOW:
  STATUS: KOO_DECISIONS_RECORDED

SECTION_B_EVIDENCE_AUTOMATION:
  STATUS: DECISION_PACKAGE_RECORDED

SECTION_C_OBSERVATIONS:
  STATUS: KOO_DECISIONS_RECORDED
  O_01:
    FINAL_DECISION: ADOPT
  O_02:
    FINAL_DECISION: ADOPT
  O_03:
    FINAL_DECISION: ADOPT
  O_04:
    FINAL_DECISION: ADOPT
  O_05:
    FINAL_DECISION: ADOPT
  O_06:
    FINAL_DECISION: ADOPT
    ADOPTION_TYPE: LIMITED_PILOT
  O_07:
    FINAL_DECISION: ADOPT

SECTION_D_FND06_EXPERIMENTS:
  STATUS: IN_PROGRESS
  D_01:
    DECISION: ADOPT_FOR_FND06_PILOT
    PILOT_MODE: WARNING_LEVEL
  D_02_TO_D_08:
    STATUS: PENDING_KOO_DECISION

FND06_PROCESS_CHANGES:
  STATUS: NOT_AUTHORIZED

FND06:
  STATUS: NOT_STARTED
```

---

## 1. Retrospective Scope

この振り返りで確認する対象を記載する。

現時点では評価を行わない。

- FND-05 product implementation
- candidate implementation / evaluation
- Selection / Adjudication
- Final Synthesis
- Light Review
- Heavy Review
- Conditional Judge
- targeted fix / targeted re-review
- artifact / SHA / handoff management
- repository cleanup / archive
- operator workload / process complexity

---

## 2. Fixed Facts / Timeline

GitHub一次証拠から確認する事実を後で整理するための領域。

現時点では主要な確定identityのみ記録する。

- Issue #43
- Final Synthesis PR #153
- Final Product Head
- Final Merge Commit
- candidate archive
- final post-merge state

詳細な評価や解釈はまだ書かない。

---

## 3. What Worked Well

TBD

---

## 4. What Did Not Work Well

TBD

---

## 5. Quality Gain vs Process Cost

TBD

以下を後で比較する。

- 品質向上に実際に寄与した工程
- 同じ品質を得るために簡略化できそうな工程
- 重複していたreview / handoff / verification
- 人間の判断負荷
- prompt / artifact / branch数
- STOP / reworkの原因

---

## 6. Candidate / Review Lessons

TBD

candidate比較やreview funnelから得られた知見を後で整理する。

---

## 7. Operational Observation Review

FND-05実行中に記録されたnon-normative Observation Ledgerを、
retrospectiveで個別に評価する。

Observation Ledger:

`docs/retrospectives/fnd05-operational-observations.md`

各ObservationはKooの最終判断に基づき、次のいずれかへ分類する。

- ADOPT
- DEFER
- REJECT

以下は採用方針の記録であり、このPRでprocess変更を実装する承認ではない。

### O-01 — Execution prompt + handoff contract

```yaml
FINAL_DECISION: ADOPT
```

prompt本文だけでなく投入先、Model / Harness / Effort / Context、STOP条件、Coordinatorへ返すevidence、次stageを勝手に開始してよいかを明示する。簡単な作業に巨大なhandoff templateは要求せず、将来的にgenerated handoffへ寄せる。

### O-02 — Model identity authority

```yaml
FINAL_DECISION: ADOPT
```

Model / Harness identityのauthorityは、Harness / platform execution metadata、machine-readable run metadata、operator attestation、Agent self-reportの順を基本とする。Agentの自己申告をauthoritative evidenceにせず、外部execution metadataがlocked identityと異なる場合はfail-closedでSTOPする。

### O-03 — Non-normative Observation Ledger

```yaml
FINAL_DECISION: ADOPT
```

実行中に見つかったmeaningfulな改善は、current runへ即時反映せずObservationとして記録し、current runの条件を維持したうえでretrospectiveでADOPT / DEFER / REJECTを判断する。Observationごとのbranch、PR、重いevidence packageは必須にしない。

### O-04 — Exact Git blob handoff hash verification

```yaml
FINAL_DECISION: ADOPT
```

critical handoff、canonical registry、重要review evidenceなどのcritical artifactでは、commit + pathからexact Git blobを特定し、hashを再計算してidentityを確認する。全docsやtemporary artifactには適用せず、人手転記ではなく将来的にscript化する。

### O-05 — Artifact production commit / registry lock commit separation

```yaml
FINAL_DECISION: ADOPT
```

artifactを生成したidentityと、正式なstage resultとしてregistryへlockしたidentityを区別する。適用対象はcritical stage artifactに限定し、minor artifactへの一律適用、extra branch、手作業のSHA転記は要求しない。producer identity取得とregistry importは将来自動化する方向とし、B-01のfinal run.json consolidationとは別問題として扱う。

### O-06 — Just-in-Time Spec / CI Rule Check experiment

```yaml
FINAL_DECISION: ADOPT
ADOPTION_TYPE: LIMITED_PILOT
```

Full JIT Spec policyを全面採用せず、小さく試す。Core Specは「今回何を作るか」を設計方針確定後・初回実装前に記録し、run後はhistorical referenceとする。ADRは「なぜその設計にしたか」を記録するlong-livedな正本とし、明らかな矛盾は実装修正またはADR更新 / supersedeで扱う。JIT Stage Specは対象runだけで有効なrun-scoped資料として保持し、後続runで再利用しない。

初回pilotはstage-local handoff instructions、stage-local artifact format、stage-specific evidence formatなどから小さく始め、Acceptance Criteria、重要なarchitecture constraint、security requirement、persistence behavior、failure behavior、critical oracle requirementは初回実装前に伝える。機械的に短時間で判定できるものはDECISION-05のFast Mechanical Gate pilotと連携可能だが、このObservation自体でprocess実装は開始しない。ADR/Core Specの複雑なdependency graph、重いversion governance、専用drift management systemは導入しない。

### O-07 — Windows / WSL Git EOL contract

```yaml
FINAL_DECISION: ADOPT
```

minimal `.gitattributes`とlightweight EOL preflightにより、Windows / WSL間でEOLだけの大量偽差分を抑える方向を採用する。ただし今回のPRでは実装しない。将来もrepository全体の一括変換、全ファイルの強制normalize、複雑なplatform別ruleは最初から導入せず、大量diffを伴うnormalizeが必要なら独立cleanupとして扱う。

必要に応じてLedgerに存在する追加Observationも、
GitHub一次証拠を確認したうえでこのsectionへ追加する。

---

## 8. Keep / Simplify / Remove

最終的にFND-05のprocessを以下へ分類する。

### KEEP

TBD

### SIMPLIFY

TBD

### REMOVE

TBD

---

## 9. Candidate Improvements for FND-06

TBD

重要:

ここには最初から改善案を大量に入れない。

FND-05 retrospectiveの結果として、
FND-06で実際に試す変更を少数へ絞る。

---

## 10. Decisions

このsectionは、Kooが確定した開発フロー判断と、後続判断のために整理した証拠・自動化のdecision packageを記録する。

Section Bはprocess変更の実装承認ではない。Section CのObservation最終判断、Section DのFND-06 experiment最終選定、およびretrospective完了前に、AGENTS.md、CI、script、test、run registryその他のprocess codeへ反映しない。

### 10.1 Development Flow Decisions — Koo Approved

#### DECISION-01 — Candidate Policy

```yaml
STATUS: APPROVED
DECISION: 固定3候補制を廃止する
FUTURE_DIRECTION: riskとperspectiveに応じた2〜3候補制
PERSPECTIVE_ASSIGNMENT: 各candidateへ事前に異なるperspectiveを割り当てる
FND06_CANDIDATE_REDUCTION_EXPERIMENT: DEFER
```

制度上は「常に3候補」を廃止する。ただしFND-06では、他の改善と同時にcandidate数削減実験を行わない。

#### DECISION-02 — Implementation Evaluation

```yaml
STATUS: APPROVED
DECISION: KEEP
```

Candidateを共通基準で比較するstageとして維持する。candidate scoreとmerge readinessは同一ではなく、引き続き分離する。

#### DECISION-03 — Selection / Adjudication

```yaml
STATUS: APPROVED
DECISION: KEEP_AS_INDEPENDENT_STAGE
```

Implementation Evaluationとは統合しない。高得点candidateを丸ごと採用するstageではなく、authority-firstでelement selectionを行う独立stageとして維持する。

#### DECISION-04 — Final Synthesis

```yaml
STATUS: APPROVED
DECISION: KEEP
HANDOFF_DIRECTION: AUTOMATE
```

Final Synthesis自体は維持する。次のhandoff組み立ては将来の自動化候補とする。

- Model / Harness / Effort / Context
- Target Head
- artifact identity
- STOP条件
- 返却evidence

#### DECISION-05 — Light Review

```yaml
STATUS: APPROVED
DECISION: SIMPLIFY

CURRENT:
  LIGHT_REVIEW_COUNT: 2

TARGET:
  FAST_MECHANICAL_GATE:
    CHECK_COUNT_GUIDE: 5-8
  LIGHT_REVIEW:
    COUNT: 1
```

新しい基本構造は次とする。

```text
Final Synthesis
  ↓
Fast Mechanical Gate
  ↓
Light Contract / Evidence / Oracle Review ×1
  ↓
Heavy Review ×2
```

Fast Mechanical Gateへ移すのは、短時間かつ決定論的にYES / NO判定できるものに限定する。

- checkout identity
- forbidden patterns
- digest pin
- `docker compose config`
- `run.json` required-field completeness
- EOL preflight
- critical artifact hash
- file placement

次はFast Mechanical Gateへ入れない。

- Docker runtime startup
- integration test全体
- full mutation suite
- semantic oracle judgment
- failure meaningの判断

目的はCIを重くすることではない。FND-06へ導入する最終判断はSection Dに残し、導入する場合はFast Mechanical Gateの実行時間を観測して、重くなりすぎていないことを確認する。

Light Reviewerには次の意味判断を残す。

- contract correctness
- evidence sufficiency
- oracle correctness
- negative case sufficiency
- false assurance
- failure signature discrimination
- Acceptance Criteriaとの対応

#### DECISION-06 — Heavy Review

```yaml
STATUS: APPROVED_WITH_KOO_OVERRIDE
DECISION: KEEP_TWO_HEAVY_REVIEWERS
HEAVY_REVIEW_COUNT: 2
PERSPECTIVE_DIVERSITY: REQUIRED
RISK_BASED_ONE_REVIEWER_REDUCTION: NOT_ADOPTED
```

Synthesisのrisk-based削減案は採用しない。FND-05ではH1がMajor 0、H2がMajor 2であり、現時点でHeavy Reviewを1本へ減らす十分な根拠がない。Heavy Reviewは2本を維持し、同じperspectiveを繰り返さず、異なるreview responsibilityを事前に割り当てる。

#### DECISION-07 — Conditional Judge

```yaml
STATUS: APPROVED
DECISION: KEEP
DEFAULT: OFF
```

Conditional Judgeは第3のfull reviewerとして常時実行しない。次のようなmeaningful disagreementがある場合だけ起動する。

- Blocker / Major disagreement
- root cause disagreement
- fix direction disagreement
- merge readiness disagreement
- meaningful unverified assumption

#### DECISION-08 — Targeted Fix / Targeted Re-review

```yaml
STATUS: APPROVED
DECISION: KEEP
```

changed surfaceが限定され、root causeが明確で、exact-head full CIがGREENであり、finding ownerと別perspective verifierが確認できる場合は、full Heavy rerunを要求しない。

production architecture変更、security boundary変更、cross-cutting変更、またはchanged surfaceを限定できない場合はfull re-reviewへ戻る。

### 10.2 Evidence / Automation — Recommended Decision Package

このpackageは、Section C / Dの判断とretrospective完了後に別PRで扱うprocess変更候補である。このPRでは設計判断を記録するだけであり、process変更を実装しない。

#### B-01 — `run.json` Final Consolidation

```yaml
RECOMMENDATION: YES
DIRECTION:
  - final consolidation
  - required-stage completeness check
FND06_PILOT_DIRECTION: WARNING_LEVEL_FIRST
```

FND-05ではlater stageのlocked結果がcontrol branchに存在した一方、canonicalとされたmain側の`run.json`へ最終集約されなかった。将来は次の構造を候補とする。

```text
各stage immutable result
  ↓
single registryへ自動取込み
  ↓
final consolidated run.json
  ↓
required-stage completeness check
```

最初からmerge blockerにはしない。FND-06でpilot対象に選定される場合はwarning-levelから開始する。

#### B-02 — Identity / SHA Automation

```yaml
RECOMMENDATION: YES
```

次のidentity確認は削らない。

- final product Head
- merge commit / tree identity
- actual checkout SHA
- critical artifact SHA256
- exact Git blob identity
- producer commit
- external Model / Harness identity
- final consolidated registry identity

人間による取得、転記、重複記録を減らし、scriptまたはgenerated manifestで一度取得し、一度記録する方向とする。

削減候補は、同じSHAをprompt / Issue / artifact / `run.json`へ手入力で複製すること、generated値を別Markdownへ再転記すること、およびAgent自己申告Modelをauthoritative evidenceにすることである。

#### B-03 — Generated Execution Handoff

```yaml
RECOMMENDATION: YES
AUTOMATION_LEVEL: GENERATE_ONLY
HUMAN_APPROVAL: REQUIRED
```

候補構造は次とする。

```text
run.json / stage metadata
  ↓
generate-handoff
  ↓
Model / Harness / Effort
Context
Target branch / Head
完全版prompt
STOP条件
必須返却evidence
next-stage prohibition
  ↓
Koo確認
  ↓
手動投入
```

Agentへの完全自動投入、STOP後の自動再実行、findingの自動棄却、candidate数の自動決定、PR Ready化、およびmergeは自動化対象に含めない。

#### B-04 — Branch / Archive Cleanup

```yaml
RECOMMENDATION: YES
```

将来は次の構造を候補とする。

```text
final consolidation
  ↓
final manifest生成
  ↓
recovery annotated tag生成
  ↓
tag dereference確認
  ↓
不要review-control branch削除
```

これはevidenceを消す提案ではない。branchによる保持から、manifestとrecovery tagによる保持へ変更する提案である。PR #154ではbranch削除もtag作成も実施しない。

### 10.3 Decision State

```yaml
RETROSPECTIVE_DECISIONS:
  STATUS: IN_PROGRESS

SECTION_A_DEVELOPMENT_FLOW:
  STATUS: KOO_DECISIONS_RECORDED

SECTION_B_EVIDENCE_AUTOMATION:
  STATUS: DECISION_PACKAGE_RECORDED

SECTION_C_OBSERVATIONS:
  STATUS: KOO_DECISIONS_RECORDED
  O_01:
    FINAL_DECISION: ADOPT
  O_02:
    FINAL_DECISION: ADOPT
  O_03:
    FINAL_DECISION: ADOPT
  O_04:
    FINAL_DECISION: ADOPT
  O_05:
    FINAL_DECISION: ADOPT
  O_06:
    FINAL_DECISION: ADOPT
    ADOPTION_TYPE: LIMITED_PILOT
  O_07:
    FINAL_DECISION: ADOPT

SECTION_D_FND06_EXPERIMENTS:
  STATUS: IN_PROGRESS
  D_01:
    DECISION: ADOPT_FOR_FND06_PILOT
    PILOT_MODE: WARNING_LEVEL
  D_02_TO_D_08:
    STATUS: PENDING_KOO_DECISION

FND06_PROCESS_CHANGES:
  STATUS: NOT_AUTHORIZED

FND06:
  STATUS: NOT_STARTED
```

### 10.4 Section D — FND-06 Experiments

#### D-01 — `run.json` Final Consolidation

```yaml
DECISION: ADOPT_FOR_FND06_PILOT
PILOT_MODE: WARNING_LEVEL
SCOPE:
  - final run.json consolidation
  - required-stage completeness check
INITIAL_ENFORCEMENT:
  MERGE_BLOCKER: false
  WARNING_ONLY: true
```

このpilotは、stage自体は完了しているのにcanonicalな`run.json`では`not_started`のままになる状態を防ぐために試す。最終的なrun状態を見る場所を一本化し、operatorが複数branch / artifactを人手で突合する負担を減らす。FND-05で実際に観測されたprocess defectへの直接対策として、FND-06で小さく試す。

最初からmerge blockerにせずwarning-levelから開始する。consolidation / completeness checker自身の誤判定で開発を止めないため、またFND-06の実runで挙動を観測してから強制gate化を判断するためである。

```yaml
D_01_MEASUREMENT:
  REQUIRED_STAGES_DETECTED:
    OBSERVE: true
  MISSING_STAGE_WARNING_CORRECT:
    OBSERVE: true
  FINAL_RUN_JSON_MATCHES_ACTUAL_STAGE_STATE:
    OBSERVE: true
  MANUAL_CORRECTION_NEEDED:
    OBSERVE: true

D_01_IMPLEMENTATION:
  STATUS: NOT_STARTED
PROCESS_CHANGE_IMPLEMENTATION:
  AUTHORIZED: false
FND06:
  STARTED: false
```

観測するのは、必須stageを正しく認識できたか、stage欠落時に正しくwarningできたか、final `run.json`と実際のstage状態が一致したか、人間による手修正が必要だったかである。過剰なKPIやscore制度は追加しない。

D-02〜D-08（Mutation Meta-Verifier、Generated Execution Handoff、Fast Mechanical Gate、O-06 LIMITED PILOT、minimal EOL contract、Identity / SHA automation、Branch / Archive cleanup automation）は今回判断せず、`PENDING_KOO_DECISION`のまま維持する。

---

## 11. Next Step

Coordinatorへ返却し、KooがSection DのFND-06 experiment選定を別工程で判断する。Section CのO-01〜O-07最終判断は本sectionへ記録済みである。

retrospectiveは`IN_PROGRESS`に維持し、PR #154をDraftのままにする。FND-06を開始せず、process変更を実装しない。
