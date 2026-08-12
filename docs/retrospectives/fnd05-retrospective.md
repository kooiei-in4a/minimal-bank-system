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
  STATUS: KOO_DECISIONS_RECORDED
  D_01:
    DECISION: ADOPT_FOR_FND06_PILOT
    PILOT_MODE: WARNING_LEVEL
  D_02:
    DECISION: ADOPT_FOR_FND06_PILOT
    PILOT_SCOPE: LIMITED
    TARGET: CRITICAL_MUTATIONS_ONLY
  D_03:
    DECISION: ADOPT_FOR_FND06_PILOT
    PILOT_SCOPE: GENERATE_ONLY
    HUMAN_APPROVAL: REQUIRED
    AUTOMATIC_AGENT_LAUNCH: false
  D_04:
    DECISION: ADOPT_FOR_FND06_PILOT
    PILOT_SCOPE: SMALL_LOW_RISK
    INITIAL_CHECK_COUNT: 3
    WARNING_ONLY: true
  D_05:
    DECISION: ADOPT_FOR_FND06_PILOT
    PILOT_SCOPE: MINIMAL
    CORE_SPEC_COUNT: 1
    JIT_STAGE_SPEC_COUNT: 1
    JIT_TARGET: FINAL_SYNTHESIS_TO_LIGHT_REVIEW_HANDOFF
  D_06:
    DECISION: DEFER_FROM_FND06
    O_07_DIRECTION: REMAINS_ADOPTED
    FND06_INCLUDE: false
  D_07:
    DECISION: DEFER_AS_STANDALONE_FND06_PILOT
    DIRECTION: REMAINS_ADOPTED
    FND06_STANDALONE_EXPERIMENT: false
  D_08:
    DECISION: DEFER_FROM_FND06
    DIRECTION: REMAINS_ADOPTED
    FND06_INCLUDE: false

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
  STATUS: KOO_DECISIONS_RECORDED
  D_01:
    DECISION: ADOPT_FOR_FND06_PILOT
    PILOT_MODE: WARNING_LEVEL
  D_02:
    DECISION: ADOPT_FOR_FND06_PILOT
    PILOT_SCOPE: LIMITED
    TARGET: CRITICAL_MUTATIONS_ONLY
  D_03:
    DECISION: ADOPT_FOR_FND06_PILOT
    PILOT_SCOPE: GENERATE_ONLY
    HUMAN_APPROVAL: REQUIRED
    AUTOMATIC_AGENT_LAUNCH: false
  D_04:
    DECISION: ADOPT_FOR_FND06_PILOT
    PILOT_SCOPE: SMALL_LOW_RISK
    INITIAL_CHECK_COUNT: 3
    WARNING_ONLY: true
  D_05:
    DECISION: ADOPT_FOR_FND06_PILOT
    PILOT_SCOPE: MINIMAL
    CORE_SPEC_COUNT: 1
    JIT_STAGE_SPEC_COUNT: 1
    JIT_TARGET: FINAL_SYNTHESIS_TO_LIGHT_REVIEW_HANDOFF
  D_06:
    DECISION: DEFER_FROM_FND06
    O_07_DIRECTION: REMAINS_ADOPTED
    FND06_INCLUDE: false
  D_07:
    DECISION: DEFER_AS_STANDALONE_FND06_PILOT
    DIRECTION: REMAINS_ADOPTED
    FND06_STANDALONE_EXPERIMENT: false
  D_08:
    DECISION: DEFER_FROM_FND06
    DIRECTION: REMAINS_ADOPTED
    FND06_INCLUDE: false

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

#### D-02 — Mutation Meta-Verifier

```yaml
DECISION: ADOPT_FOR_FND06_PILOT
PILOT_SCOPE: LIMITED
TARGET: CRITICAL_MUTATIONS_ONLY
FULL_FRAMEWORK: false
INITIAL_TARGET_COUNT:
  GUIDE: 1-3
```

FND-06では、すべてのmutationを対象とする重いframeworkにはせず、重要なfailure behavior / oracle correctnessを確認するmutationから1〜3件程度を選んで小さくpilotする。`1-3`はguideであり、無理に3件作る必要はない。

FND-05では、`CI GREEN`と`Mutation KILLED`だけでは、shipped oracleが意図したfailureを検出したことを証明できないケースが確認された。mutation後にtestがREDになっても、期待した理由でREDになったとは限らないため、D-02はこのfalse assuranceへの対策として試す。

```yaml
D_02_REQUIRED_CORE:
  BASELINE_BEFORE:
    REQUIRED: true
  MUTATION_APPLIED:
    REQUIRED: true
  SHIPPED_ORACLE_RED:
    REQUIRED: true
  EXPECTED_FAILURE_SIGNATURE:
    REQUIRED: true
  MUTATION_RESTORED:
    REQUIRED: true
  BASELINE_AFTER_RESTORE:
    REQUIRED: true
```

正常状態でPASSした後にmutationを適用し、既存のshipped oracleを実行してREDになること、狙ったfailure signatureでREDになったことを確認する。その後mutationを戻し、正常状態で再度PASSする。単にtestが失敗したのではなく、狙った理由で失敗したことを確認する。

`EXPECTED_FAILURE_SIGNATURE`は巨大なsnapshotや完全なログ一致を必須にせず、対象に応じてexpected exception type、error code、marker、assertion、stage、failure classificationなどから、別原因によるREDと区別できる最小限のsignatureを使う。脆い全文ログ一致は標準化しない。

全mutationへの一律meta-verification義務、巨大なmutation framework、mutationごとの専用branchや独立PR、過剰な証跡package、mutation数を増やすこと自体を目的とする運用にはしない。目的は、重要なmutationについてshipped oracleが正しいfailureを検出しているかを確認することである。

```yaml
D_02_MEASUREMENT:
  BASELINE_BEFORE_PASSED:
    OBSERVE: true
  MUTATION_APPLIED_CONFIRMED:
    OBSERVE: true
  SHIPPED_ORACLE_RED:
    OBSERVE: true
  EXPECTED_FAILURE_SIGNATURE_MATCHED:
    OBSERVE: true
  MUTATION_RESTORE_CONFIRMED:
    OBSERVE: true
  BASELINE_AFTER_RESTORE_PASSED:
    OBSERVE: true
  UNEXPECTED_RED_CAUSE_FOUND:
    OBSERVE: true

D_02_IMPLEMENTATION:
  STATUS: NOT_STARTED
```

最後の観測項目では、mutationはKILLEDしたが実際には期待とは別の理由でtestが落ちていたケースを検出したかを確認する。複雑なscoreや重み付けは追加しない。

#### D-03 — Generated Execution Handoff

```yaml
DECISION: ADOPT_FOR_FND06_PILOT
PILOT_SCOPE: GENERATE_ONLY
HUMAN_APPROVAL:
  REQUIRED: true
AUTOMATIC_AGENT_LAUNCH:
  ENABLED: false
INITIAL_SCOPE: SMALL
```

FND-06ではagent実行そのものを自動化せず、`run.json` / stage metadataなどからexecution handoffを生成し、Kooが内容を確認した後、人間が対象Harnessへ投入するところまでをpilot対象とする。目的は、stage間handoffで人間が毎回組み立てている情報の転記負担と誤りを減らすことである。

```yaml
D_03_INITIAL_FIELDS:
  - MODEL
  - HARNESS
  - EFFORT
  - CONTEXT
  - TARGET_HEAD
  - STOP_CONDITIONS
  - REQUIRED_RETURN_EVIDENCE
```

必要に応じて、既存metadataから安全に得られる範囲でROLE、target artifact identity、next action / no-next-actionを含めてもよいが、pilot scopeは不必要に拡大しない。最初は1種類程度の小さなexecution handoff生成から始め、詳細formatはprocess implementation時に決める。

```yaml
D_03_EXECUTION_POLICY:
  HANDOFF_GENERATION:
    AUTOMATED: true
  HUMAN_REVIEW_BEFORE_USE:
    REQUIRED: true
  AGENT_EXECUTION:
    AUTOMATED: false
  AUTO_NEXT_STAGE:
    ENABLED: false
```

handoffの生成を自動化しても、execution開始の判断は人間に残す。生成されたhandoffをそのまま自動投入せず、automatic merge、automatic Ready化、next stageの自動開始、Harness横断の巨大なorchestrator、複雑なworkflow engineには拡張しない。prompt本文全体を毎回AIが自由生成する仕組みにもせず、既存のauthoritative metadataから小さなhandoff artifactを生成する。

Model、Harness、Effort、Target Head、artifact identityなどは、可能な限りmachine-readable / externally confirmed metadataから取得する。Agent self-reportをauthoritative sourceにせず、O-02 Model Identity Authorityの原則を維持する。

```yaml
D_03_MEASUREMENT:
  MANUAL_FIELDS_REQUIRED:
    OBSERVE: true
  GENERATED_FIELDS_CORRECT:
    OBSERVE: true
  HUMAN_CORRECTIONS_REQUIRED:
    OBSERVE: true
  TRANSCRIPTION_ERROR_FOUND:
    OBSERVE: true
  HANDOFF_USABLE_WITHOUT_REBUILD:
    OBSERVE: true

D_03_IMPLEMENTATION:
  STATUS: NOT_STARTED
HANDOFF_GENERATOR:
  IMPLEMENTED: false
AUTOMATIC_AGENT_EXECUTION:
  IMPLEMENTED: false
```

観測するのは、生成後も手入力が必要だった項目、metadataから生成した値の正しさ、使用前の人間による修正、SHA / Model / STOP条件などの転記問題、promptを最初から組み直さずhandoffを使えたかである。秒単位の工数計測や複雑なscore制度は追加しない。

#### D-04 — Fast Mechanical Gate

```yaml
DECISION: ADOPT_FOR_FND06_PILOT
PILOT_SCOPE: SMALL_LOW_RISK
INITIAL_CHECK_COUNT:
  TARGET: 3
ENFORCEMENT:
  WARNING_ONLY: true
  MERGE_BLOCKER: false
SEMANTIC_CHECKS:
  INCLUDED: false
```

従来候補の5〜8チェックを一度に導入せず、FND-06ではリスクの低いものを小さく試す。最初のpilotは3チェックだけとし、semantic judgmentは含めない。

```yaml
D_04_INITIAL_CHECKS:
  CHECK_01:
    NAME: checkout identity
    PURPOSE: actual checkoutがexpected Headと一致していることを確認する
  CHECK_02:
    NAME: run.json required-stage completeness
    PURPOSE: required stageの記録漏れを確認する
    IMPLEMENTATION_DIRECTION:
      REUSE_D01: true
  CHECK_03:
    NAME: critical artifact identity/hash
    PURPOSE: critical artifactがexpected identityと一致していることを確認する
```

CHECK_02はD-04専用の別checkerを作るのではなく、D-01で採用したrun.json final consolidationとrequired-stage completeness checkの結果を再利用する。D-01がrun.json consolidation / completenessを担い、D-04はfast mechanical checksをまとめるgateを担う。重複実装は許可しない。

```yaml
D_04_CHECK_REQUIREMENTS:
  FAST:
    REQUIRED: true
  DETERMINISTIC:
    REQUIRED: true
  YES_NO_DECISION:
    REQUIRED: true
  SEMANTIC_INTERPRETATION:
    ALLOWED: false
```

初期pilotにはforbidden pattern check、docker compose config、EOL preflight、broad file placement rules、Docker runtime startup、integration test、full mutation suite、semantic oracle judgment、failure meaning judgmentを含めない。これらは`NOT_INCLUDED_IN_INITIAL_D04_PILOT`として扱い、REJECTとは分類しない。pattern設計によるfalse positive、repository / environment依存、D-06未決定、pilot scopeの拡大、Fast Mechanical Gateの責務外であることを理由とする。

Fast Mechanical Gateへ入れるチェックは、fast、deterministic、YES / NO decisionをすべて満たし、semantic interpretationを必要としないものに限る。意味の判断が必要な確認はSemantic Light Review / Heavy Reviewに残す。

```yaml
D_04_REVIEW_FLOW:
  ORDER:
    - Final Synthesis
    - Fast Mechanical Gate
    - Semantic Light Review x1
    - Heavy Review x2
  ENFORCEMENT:
    WARNING_ONLY: true
    MERGE_BLOCKER: false
```

warning-onlyから開始するのは、checker自身の誤判定でrunを止めず、false warningを観測し、実運用で安定性を確認してから強制gate化を判断するためである。

```yaml
D_04_MEASUREMENT:
  CHECKOUT_IDENTITY_CORRECT:
    OBSERVE: true
  RUN_JSON_COMPLETENESS_RESULT_CORRECT:
    OBSERVE: true
  CRITICAL_ARTIFACT_IDENTITY_CORRECT:
    OBSERVE: true
  FALSE_WARNING_OCCURRED:
    OBSERVE: true
  HUMAN_CORRECTION_REQUIRED:
    OBSERVE: true
  EXECUTION_TIME:
    OBSERVE: true

D_04_EXPANSION:
  INITIAL_CHECK_COUNT: 3
  AUTO_EXPAND:
    ALLOWED: false
  ADDITIONAL_CHECKS:
    REQUIRE:
      - explicit Koo decision
      - later retrospective decision

D_04_IMPLEMENTATION:
  STATUS: NOT_STARTED
FAST_MECHANICAL_GATE:
  IMPLEMENTED: false
```

execution timeは厳密なperformance benchmarkではなく、processが不必要に重くなっていないかを確認できる程度に観測する。pilot実行中に追加候補を見つけてもcurrent runへ自動追加せず、必要ならObservationとして残す。複雑なscoreや重み付けは追加しない。

#### D-05 — O-06 LIMITED PILOT: Core Spec / JIT Stage Spec

```yaml
DECISION: ADOPT_FOR_FND06_PILOT
PILOT_SCOPE: MINIMAL
CORE_SPEC:
  COUNT: 1
  CREATED_BEFORE_INITIAL_IMPLEMENTATION: true
JIT_STAGE_SPEC:
  COUNT: 1
  TARGET: FINAL_SYNTHESIS_TO_LIGHT_REVIEW_HANDOFF
AUTOMATIC_ENFORCEMENT: false
HEAVY_GOVERNANCE:
  ENABLED: false
```

Core Specは、FND-06で何を作るかを初回実装前に簡潔に固定するためのpilot資料とする。初回実装前に必要なAcceptance Criteria、重要なarchitecture constraint、security requirement、persistence behavior、failure behavior、critical oracle requirement、明確なout-of-scopeなどを置く方向とするが、今回のdecision記録ではFND-06固有の実仕様やCore Spec自体を作成しない。

```yaml
CORE_SPEC_LIFECYCLE:
  CREATED:
    BEFORE_INITIAL_IMPLEMENTATION: true
  DURING_RUN:
    PURPOSE: implementation baseline
  AFTER_RUN:
    STATUS: HISTORICAL_REFERENCE
  PERMANENT_CURRENT_AUTHORITY:
    REQUIRED: false
```

Core Specは永久に現行authorityとして維持する重い制度にはしない。Core Specが今回何を作るかを定めるのに対し、ADRはなぜその設計にしたかを記録する。複雑なADR dependency graphやCore Spec専用dependency graphは導入しない。

JIT Stage Specは、今このstageで何をするかだけを定義するrun-scoped資料として、FND-06では1種類だけ試す。対象はFinal SynthesisからSemantic Light Reviewへのhandoffであり、stage-localな情報に限定する。

```yaml
JIT_STAGE_SPEC_INITIAL_FIELDS:
  - STAGE_ROLE
  - TARGET_HEAD
  - INPUT_ARTIFACT
  - STAGE_LOCAL_REVIEW_FOCUS
  - STOP_CONDITIONS
  - REQUIRED_RETURN_EVIDENCE
  - AFTER_COMPLETION_ACTION
```

JIT Stage Specは、Final Synthesis完了、review対象Head確定、入力artifact確定の後、Light Review開始直前に作成する。Acceptance Criteria、重要architecture constraint、security requirements、persistence behavior、failure behavior、critical oracle requirementsはJITへ追い出さず、Core Spec側で初回実装前に伝える。

```yaml
JIT_BOUNDARY:
  CORE_SPEC_OWNS:
    - acceptance_criteria
    - critical_architecture
    - security
    - persistence_behavior
    - failure_behavior
    - critical_oracle_requirements
  JIT_STAGE_SPEC_OWNS:
    - stage_local_handoff
    - target_identity
    - stage_local_focus
    - stage_local_stop
    - stage_return_evidence

D_03_D_05_RELATION:
  D_05:
    DEFINES_HANDOFF_CONTENT_BOUNDARY: true
  D_03:
    MAY_GENERATE_FROM_AUTHORITATIVE_METADATA: true
  DUPLICATE_HANDOFF_SYSTEM:
    ALLOWED: false
```

D-05はJIT Stage Specとして何を渡すかを定義し、D-03はauthoritative metadataからそのhandoffを可能な範囲で生成する。両者を重複したhandoff systemにはしない。

FND-06では、Core Spec version management system、Core Spec専用dependency graph、ADR dependency graph、automatic drift detection、dedicated drift management system、全stageへのJIT Stage Spec必須化、複数種類のJIT Stage Spec、専用branch / PR、JIT Stage SpecのCI gate化、Core Specとimplementationの自動同期、Core Specを永久に最新仕様として維持する制度は導入しない。

```yaml
JIT_STAGE_SPEC_LIFECYCLE:
  VALID_FOR:
    FND_06_ONLY: true
  AFTER_RUN:
    STATUS: EXPIRED_HISTORICAL_ONLY
  REUSE_FOR_LATER_RUN:
    ALLOWED: false

D_05_MEASUREMENT:
  CORE_SPEC_HELPED_INITIAL_IMPLEMENTATION:
    OBSERVE: true
  IMPORTANT_REQUIREMENT_MISSING_FROM_CORE_SPEC:
    OBSERVE: true
  JIT_STAGE_SPEC_HAD_NEEDED_STAGE_INFO:
    OBSERVE: true
  JIT_STAGE_SPEC_REQUIRED_REBUILD:
    OBSERVE: true
  DUPLICATE_INFORMATION_BECAME_PROBLEM:
    OBSERVE: true
  OPERATOR_FOUND_STRUCTURE_USEFUL:
    OBSERVE: true

D_05_IMPLEMENTATION:
  STATUS: NOT_STARTED
CORE_SPEC:
  CREATED: false
JIT_STAGE_SPEC:
  CREATED: false
```

JIT Stage Specはrun-scopedで、FND-06終了後はexpired historical-onlyとして扱い、後続runのcurrent authorityとしてそのまま再利用しない。複雑なscoreは追加せず、初回実装に必要な情報がCore Specで十分だったか、JIT Stage Specがstage-local情報に絞れていたか、二重管理にならなかったか、実際に使いやすかったかを観測する。

#### D-06 — minimal EOL contract

```yaml
DECISION: DEFER_FROM_FND06
O_07_DIRECTION:
  REMAINS_ADOPTED: true
FND06:
  INCLUDE: false
FUTURE_IMPLEMENTATION:
  TARGET: SEPARATE_PROCESS_UPDATE
  TIMING: AFTER_FND05_RETROSPECTIVE
  SCOPE:
    - minimal .gitattributes
    - lightweight EOL preflight
MASS_NORMALIZATION:
  ALLOWED: false
```

O-07の方向性は採用済みのまま維持するが、FND-06のpilotには含めない。FND-06にはすでにD-01〜D-05の改善を小さく試す方針が入っているため、repository hygieneであるEOL対策まで同じrunへ追加せず、変更要因を増やさない。

minimal `.gitattributes`とlightweight EOL preflightは、FND-05 retrospective完了後の独立したProcess Update候補として扱う。repository全体の一括normalize、大量EOL変更、複雑なplatform別ruleは行わない。

```yaml
D_06_IMPLEMENTATION:
  STATUS: NOT_STARTED
PROCESS_CHANGE_IMPLEMENTATION:
  AUTHORIZED: false
FND06:
  STARTED: false
```

D-06をFND-06から外すことはO-07のREJECTを意味しない。EOL contract自体は採用方向を維持し、FND-06でのexperiment対象からのみ外す。

#### D-07 — Identity / SHA automation

```yaml
DECISION: DEFER_AS_STANDALONE_FND06_PILOT
DIRECTION:
  REMAINS_ADOPTED: true
FND06:
  STANDALONE_EXPERIMENT: false
INTEGRATION_DIRECTION:
  D_03:
    USE_AUTHORITATIVE_IDENTITY_METADATA: true
  D_04:
    REUSE_IDENTITY_CHECK_LOGIC: true
DUPLICATE_IDENTITY_SYSTEM:
  ALLOWED: false
```

Identity / SHA automationの方向性は採用済みのまま維持するが、FND-06では独立した追加pilotとして扱わない。D-03 Generated Execution HandoffがTarget HeadやModel / Harnessなどのauthoritative identity metadataを利用し、D-04 Fast Mechanical Gateがcheckout identityやcritical artifact identity/hashの確認ロジックを利用するため、D-07専用の第三のidentity取得・転記systemは作らない。

重要identityの確認自体は削らない。人間によるSHA取得・転記・重複記録を減らす方向は維持し、FND-06ではD-03 / D-04の実装で同じidentity source / logicを可能な範囲で再利用する。D-07を独立experimentから外すことはB-02 Identity / SHA AutomationのREJECTを意味しない。

```yaml
D_07_IMPLEMENTATION:
  STATUS: NOT_STARTED
PROCESS_CHANGE_IMPLEMENTATION:
  AUTHORIZED: false
FND06:
  STARTED: false
```

#### D-08 — Branch / Archive cleanup automation

```yaml
DECISION: DEFER_FROM_FND06
DIRECTION:
  REMAINS_ADOPTED: true
FND06:
  INCLUDE: false
CURRENT_OPERATION:
  MANUAL_ARCHIVE_AND_CLEANUP: CONTINUE
FUTURE_IMPLEMENTATION:
  TARGET: SEPARATE_PROCESS_UPDATE
  TIMING: AFTER_FND06
  SAFETY_ORDER:
    - final consolidation
    - final manifest
    - recovery annotated tag
    - tag verification
    - only then branch cleanup
AUTOMATIC_BRANCH_DELETION:
  BEFORE_RECOVERY_VERIFICATION: PROHIBITED
```

Branch / Archive cleanup automationの方向性は採用済みのまま維持するが、FND-06のexperimentには含めない。FND-06では既存のmanual archive / cleanupを継続し、run品質に直接関係しない後処理のautomationを追加して変更要因を増やさない。

将来の自動化では、evidenceを消すのではなく、最終状態をconsolidateし、final manifestとrecovery annotated tagを作成し、そのtagから復元できることを確認してから不要なreview / control branchを整理する。recovery verification前のautomatic branch deletionは禁止する。

```yaml
D_08_IMPLEMENTATION:
  STATUS: NOT_STARTED
BRANCH_CLEANUP_AUTOMATION:
  IMPLEMENTED: false
PROCESS_CHANGE_IMPLEMENTATION:
  AUTHORIZED: false
FND06:
  STARTED: false
```

D-08をFND-06から外すことはB-04 Branch / Archive CleanupのREJECTを意味しない。FND-06後の独立Process Update候補として維持する。

Section DのD-01〜D-08についてKooの判断はすべて記録済みである。D-01〜D-05をFND-06 pilot対象とし、D-06〜D-08はFND-06へ追加しない。この判断完了はFND-05 retrospective全体の完了、process変更の実装承認、またはFND-06開始承認を意味しない。

---

## 11. Next Step

Section DのFND-06 experiment選定判断は完了した。次はFND-05 retrospective本文の未完了領域（What Worked Well、What Did Not Work Well、Quality Gain vs Process Cost、Candidate / Review Lessons、Keep / Simplify / Remove、Candidate Improvements for FND-06）を一次証拠と既存review結果に基づいて完成させる。

retrospectiveは`IN_PROGRESS`に維持し、PR #154をDraftのままにする。FND-06を開始せず、process変更を実装しない。