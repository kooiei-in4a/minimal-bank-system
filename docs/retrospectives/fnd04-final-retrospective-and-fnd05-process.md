# FND-04 最終振り返り — レビューのリスクファネル化とFND-05改善計画

```yaml
DOCUMENT_STATUS: "FINAL SYNTHESIS / DRAFT PR"
TARGET_REPOSITORY: "kooiei-in4a/minimal-bank-system"
RETROSPECTIVE_TARGET:
  ISSUE: 42
  PR: 140
  REVIEWED_HEAD: "3511688401533f60bb77c7dcc647c4c2c4aa84c6"
  MERGE_COMMIT: "9a352a3a61945647273ccc7dfbc8e1816c3ca07c"
NEXT_TARGET:
  ISSUE: 43
  TITLE: "[FND-05] Docker Compose実行基盤を確立する"
SOURCE_RETROSPECTIVES:
  - PR: 141
    EXECUTION: "GPT5.6 Sol / Browser / Pro"
  - PR: 142
    EXECUTION: "Claude Opus 5 / Claude Code / xHigh"
  - PR: 143
    EXECUTION: "Grok 4.5 + Composer 2.5 / Cursor Auto"
ADDITIONAL_DIRECTION_OWNER: "Koo"
SYNTHESIS_DATE: "2026-08-10"
```

## 1. Executive Summary

FND-04は、FND-03より明確に改善した。

初期candidateを14から8へ減らし、独立reviewを17本の同質投入から5本のrole-diverse reviewへ変更し、Judgeを3名常設から2名＋条件付き第3 Judgeへ変更した。さらに、Major発見後の14候補再実装を廃し、1件のtargeted fixと2件のtargeted re-reviewで閉じた。結果として、Final Synthesisに残ったtest oracleのMajorをmerge前に検出し、production architectureへ触れずに修正できた。

ただし、FND-04の方式を通常開発へそのまま適用するなら過剰である。全8candidateのFormal Self-Review、no-changeを含むH1管理、5 review、2 Judge、Gold、2 re-review、Formal Agent Bという構造は、研究計装としては有効でも、日常的な開発プロセスとしては重い。

FND-05では、単純に工程を削るのではなく、次のリスクファネルへ移行する。

> **ADRとテスト設計を先に固定し、軽量モデルで広く洗い、安定した最終HeadだけをSolとOpusで深く確認する。**

基本方針は次のとおりである。

1. **Design before Code** — ADR、Issue、実装設計、failure path、テストoracleを実装前に固定する。
2. **Review before Review** — review観点、必要証拠、mutationをreview開始前ではなく実装開始前に固定する。
3. **Cheap and Wide First** — 軽量モデルでscope、契約、典型バグ、test gapを広く洗い、明らかな問題を先に除去する。
4. **Expensive and Deep Last** — SolはADR・仕様・scope、Opusはlifecycle・failure path・false assuranceを、安定した最終Headへ各1回投入する。
5. **Re-review by Blast Radius** — 修正後に重いモデルを一律再投入しない。変更範囲とfindingの性質に応じて再確認範囲を決める。
6. **Evidence over Confidence** — `CI green`、`exit != 0`、レビュー人数、モデルの評判を証拠の代替にしない。

FND-05は、この方式へ移行するための実験と位置づける。製品実装だけでなく、軽量事前レビューがどこまで重い最終レビューの負荷を減らせたか、ADR起点の設計固定が後工程の手戻りを減らしたかを測定する。

---

## 2. Inputs and Evidence Policy

本Synthesisは、次を入力とする。

- FND-04の一次証拠
  - Issue #42
  - PR #140
  - exact Head / merge-ref CI
  - Implementation Evaluation
  - Selection / Adjudication
  - role-diverse reviews
  - Judge A / B
  - Gold / Major-fix clearance
  - Formal Agent B
- 3本の独立振り返り
  - PR #141: GPT5.6 Sol
  - PR #142: Claude Opus 5
  - PR #143: Cursor Auto execution
- Kooの追加方針
  - review観点を先に明示する
  - 軽量モデルによる広い事前レビューを置く
  - Sol / Opusは最後の確認へ集中させる
  - ADR起点で実装設計・テスト設計を詰める
- FND-05の正本候補
  - Issue #43
  - ADR-0001
  - ADR-0008
  - ADR-0009
  - AGENTS.md

モデルの一般的評判ではなく、FND-04で得られた実際の結果と、FND-05の正本契約を根拠とする。

---

## 3. 三つの振り返りの共通結論と相違点

### 3.1 共通結論

3 executionは、次で一致した。

- FND-04はFND-03より効率化した。
- candidate、reviewer、Judge、Major-fix attemptの大量投入は標準へ戻さない。
- H0 / Self-Review / H1の分離には測定価値があるが、全candidateへの一律適用は過剰である。
- role-diverse reviewは同質reviewerの大量投入より有効だった。
- Judgeの価値は評点平均ではなく、blocking root causeを独立probeで固定することにある。
- `green CI`やnegative outcomeだけではtest oracleの正しさを証明できない。
- Major確定後はselected Final Synthesisへのtargeted fixを標準とする。
- direct-head、merge-ref、reviewed Head、merge commitを分離して記録する。
- artifact管理はSingle Source of Truthへ寄せる必要がある。

### 3.2 相違点

| 論点 | Sol | Opus | Cursor Auto | 本Synthesisの判断 |
|---|---|---|---|---|
| 現行方式の重さ | 通常運用には過剰 | 製品開発には過剰、研究目的なら適正 | やや過剰だが適正寄り | FND-04は研究runとして成功。標準運用には過剰 |
| FND-05 candidate数 | 6 | 6 | 6〜8 | 基本6、追加2は条件付き |
| review構成 | 4 role＋1 conditional | 5枠、Test Assuranceを2枠 | 5枠、Mutation役を明示 | 軽量2枠＋重量2枠、Judge conditionalへ再構成 |
| 最重要変更 | role-diverse review＋Judge/Gold | Judge Phase A blind reference | targeted fix＋mutation review | review観点・mutation・独立probeの組合せ |
| 最大の無駄 | 全candidate Self-Review | artifact/CI運用 | 全candidate Self-Review | 両方を削減する |

相違は結論の対立ではなく、どのコストを最も重く見るかの違いである。

---

## 4. FND-04の最終評価

### 4.1 成功した点

- FND-03で高コストだったreviewer大量投入とMajor-fix全数競争を削減した。
- candidateを単純rankingでmergeせず、要素単位でSelection / Adjudicationした。
- Final Synthesisをcurrent mainから再構成し、candidate履歴や不要な設計を持ち込まなかった。
- test oracleのfalse assuranceをproduction bugと分離してMajor判定した。
- Majorをtest-onlyの小さな変更で修正した。
- fix後のmutationで、修正前のdefect classが実際にredになることを確認した。
- benchmark判定とproduct merge gateを分離した。

### 4.2 過剰だった点

- 全8candidateへのFormal Self-Review。
- code changeがないcandidateの独立H1処理。
- reviewer、Judge、Gold、clearance、Formal Agent Bの一部重複。
- heavy modelを複数工程で繰り返す構造。
- artifactごとの細かいcommitとdocs-only CI。
- parent / sub-run / READMEの状態同期を手作業へ依存したこと。

### 4.3 最も重要な新知見

> **testがgreenであることと、そのtestが守るべきdefect classを検出できることは別である。**

FND-04では、対象testが無関係なtool failureや列挙外destinationでもgreenになり得た。これを見つけたのは、説明を読んだreviewerではなく、実際にmutationを入れて壊したreviewerだった。

したがって、review promptへ観点を書くことは必要だが、それだけでは不十分である。観点は次の三つへ変換する必要がある。

```text
review viewpoint
  → required evidence
  → controlled mutation / failure injection
```

---

## 5. FND-05で採用する基本原則

### Principle 1 — ADR-to-Design Traceability

実装前に、各設計要素をADR / Issueへ追跡可能にする。

```text
ADR / Issue requirement
  → implementation responsibility
  → expected runtime state
  → test case
  → review viewpoint
  → required evidence
```

追跡できない重要設計判断が出た場合、candidateが独自決定してはいけない。Issue Readyを停止し、Koo判断またはADR更新へ戻す。

### Principle 2 — Review Perspective Lock

review観点はFinal Synthesis完成後に考えない。実装開始前に固定する。

reviewerは自由探索を行ってよいが、最低限確認すべき観点と証拠は全員共通にする。roleは重点であって、他領域を見ない理由にしない。

### Principle 3 — Lightweight Sweep Before Heavy Gate

軽量事前レビューはmerge gateではない。目的は次のとおりである。

- scope外変更を除去する
- ADR / Issueとの明白な不一致を除去する
- 典型的なsecret leak、digest未固定、test不足を除去する
- test名とassertionの乖離を洗う
- 重いreviewerへ渡すnoiseを減らす
- 重いreviewerが深いfailure pathへ集中できる状態を作る

### Principle 4 — Heavy Models Review a Stable Head Once

Sol / Opusへ未整理のHeadを何度も渡さない。

- 軽量reviewと明白な修正を完了する
- exact direct-head CIを成功させる
- review input snapshotを固定する
- その後、Sol / Opusを各1回投入する

Blocker / Major修正後も、両者を一律に再投入しない。

### Principle 5 — Re-review Is Finding-Owned

- Minor / Nit: lightweight verifierとCIで完了可能
- test-only Major: finding ownerのheavy reviewer 1名＋lightweight mutation verifier
- localized production Major: finding owner＋隣接観点1名
- architecture / security / cross-cutting Major: Sol＋Opusを再投入

---

## 6. Model / Harness Role Allocation

モデル名を絶対視せず、FND-04で観測した性質を役割へ変換する。

| Role | Primary | Responsibility | Default invocation |
|---|---|---|---:|
| ADR / Design Lead | GPT-5.6 Sol | ADR解釈、責務境界、state machine、test design contract | 1回 |
| Routine Implementer | GPT-5.6 Luna | 方針確定後の実装、明白な修正、targeted fix | 必要回数 |
| Optional Implementation Planner | GPT-5.6 Terra | 方針は確定しているが実装判断が複数残る場合のみ | 条件付き1回 |
| Lightweight Sweep A | Luna等の軽量モデル | ADR / Issue / scope / static diffを広く洗う | 1回 |
| Lightweight Sweep B | 軽量または中量の別Harness | test assurance、mutation、runtime evidenceを洗う | 1回 |
| Final Contract Reviewer | GPT-5.6 Sol | ADR / Issue / AC / scope / CI identityの最終確認 | 1回 |
| Final Adversarial Reviewer | Claude Opus | lifecycle、failure path、secret、test oracle、暗黙依存 | 1回 |
| Conditional Judge | Sol / Opusと異なるexecution | Blocker / Majorのroot causeまたはrequired fixが割れた場合のみ | 0〜1回 |

重要なのは、Solを設計と最終contract reviewへ使い、Opusを最終adversarial reviewへ集中させることである。両者を粗い事前レビューやMinor修正確認へ繰り返し使わない。

---

## 7. FND-05 ADR-Based Implementation Design Gate

Issue #43は現在、Issue Readyが未評価でImplementationは禁止状態である。実装開始前に、次を固定してIssue Readyを再評価する。

### 7.1 Authorityから導く固定事項

#### ADR-0001

- Docker Compose v2を使用する。
- .NET 10 / ASP.NET Core 10 / PostgreSQL 18を維持する。
- local / closed environmentの実行基盤とする。
- Redis、message broker、外部identity、cloud-only serviceを追加しない。
- container imageはapproved major内のexact digestへ固定する。

#### ADR-0009

- schema evolutionはEF Core migrationのみ。
- normal API startupでmigrationしない。
- one-shot explicit MigratorをAPI前に実行する。
- migration失敗時はdeploymentをfail closedする。
- bounded timeoutと非0 exitを維持する。

#### ADR-0008

- PostgreSQL dataはnamed volumeを使用する。
- technical logはconsoleへ出すがsecretを出さない。
- credentialをcommand-line引数へ直接展開しない。
- FND-05ではbackup / restoreやhealth endpointロジックを先取りしない。

### 7.2 実装前に固定すべきstate machine

```text
Compose project start
  ↓
PostgreSQL service starts
  ↓
PostgreSQLがMigrator接続可能な状態になる
  ↓
Migrator one-shot process starts
  ├─ success / exit 0
  │    ↓
  │  API starts
  │
  └─ missing config / connection / migration / timeout failure
       ↓
     non-zero exit
       ↓
     API must not start
```

次を曖昧なままにしない。

- PostgreSQLを「Migrator接続可能」とみなす具体的条件
- Migrator成功を示すexit codeとpositive marker
- API start許可条件
- APIが「開始した」ことをFND-06 health endpointなしでどう観測するか
- existing named volumeでのrerun semantics
- `start`、`stop`、`restart`、`clean reset`の正本command
- Compose標準path以外でAPIだけを起動した場合の扱い
- test-only failure injectionの方法

単なる`depends_on`記述だけで順序保証を主張してはいけない。実際のservice state、exit code、schema state、API process stateで証明する。

### 7.3 Secret contract

- repositoryへpassword / connection stringをcommitしない。
- command / process argvへpasswordを展開しない。
- `docker compose config`、container logs、CI logsへsecretを出さない。
- sample configurationにはplaceholderのみを置く。
- testではsentinel secretを注入し、stdout / stderr / generated config / argvに現れないことを確認する。

### 7.4 Scope boundary

FND-05では実装しない。

- `/health/live`、`/health/ready`のロジック
- business endpoint / business smoke
- business schema / business migration
- backup / restore
- production orchestrator
- automatic API migration
- monitoring / metrics / alerting

---

## 8. FND-05 Test Design Contract

### 8.1 Positive scenarios

| ID | Scenario | Required evidence |
|---|---|---|
| P-01 | clean volume start | PostgreSQL start、Migrator exit 0、migration history、API startの順序 |
| P-02 | existing volume rerun | Migrator再実行が成功し、history重複なし、API start |
| P-03 | API restart | normal API startupがschemaを変更しない before / after fingerprint |
| P-04 | Compose stop / start | named volume上のmigration historyが保持される |
| P-05 | clean reset | volumeが明示的に削除され、次回P-01へ戻る |
| P-06 | digest verification | PostgreSQL / API imageが想定digestで解決される |

### 8.2 Negative scenarios

| ID | Scenario | Expected result |
|---|---|---|
| N-01 | connection config missing | Migrator non-zero、API未起動、secret非露出 |
| N-02 | PostgreSQL unreachable | Migrator non-zero、API未起動、失敗理由を識別可能 |
| N-03 | invalid credential | Migrator non-zero、API未起動、sentinel非露出 |
| N-04 | migration execution failure | Migrator non-zero、API未起動、success表示なし |
| N-05 | migration timeout | bounded failure、API未起動 |
| N-06 | Migrator未完了状態 | APIをserving状態へしない |
| N-07 | API standalone startup probe | API startupでmigration history / schemaが変化しない |
| N-08 | repository / argv secret probe | prohibited secret placementをfailさせる |

### 8.3 Mandatory mutation set

| Mutation | Defect class | Test that must become red |
|---|---|---|
| M-01 | API dependencyをMigrator成功ではなく単なる開始へ弱める | P-01 / N-04 / N-06 |
| M-02 | Migratorが例外をcatchしてexit 0を返す | N-02〜N-05 |
| M-03 | API startupへ`Migrate` / `EnsureCreated`相当を追加する | P-03 / N-07 |
| M-04 | passwordをCompose commandへ直接展開する | N-08 |
| M-05 | image digestをtag-onlyへ変更する | P-06 |
| M-06 | named volumeをanonymous volumeへ変える | P-04 / P-05 |
| M-07 | failure testをMigrator到達前の無関係なcommand failureへ変える | N-01〜N-05のpositive path marker |

少なくともM-01、M-02、M-03、M-07はpre-runでlockし、Final Synthesis review時に実際のmutationまたは同等のcontrolled probeを行う。

### 8.4 Test oracle rules

negative testは、単に`non-zero`だけをassertしてはいけない。次を可能な範囲で固定する。

- expected componentへ到達した証拠
- expected failure class
- Migrator exit status
- API container / process state
- migration history / schema fingerprint
- secret非露出
- mutation sensitivity

---

## 9. FND-05 Review Perspective Matrix

実装開始前に次を固定する。

| ID | Viewpoint | Minimum question | Required evidence | Lightweight owner | Heavy owner |
|---|---|---|---|---|---|
| R-01 | ADR / Issue traceability | 全変更がADR-0001/0008/0009とIssue #43へ追跡できるか | traceability matrix | Sweep A | Sol |
| R-02 | Scope boundary | health、backup、business schemaを先取りしていないか | changed-file classification | Sweep A | Sol |
| R-03 | Compose state machine | DB→Migrator→APIが実状態で成立するか | container state / exit / ordering | Sweep B | Opus |
| R-04 | Fail-closed | migration失敗時にAPIが開始しないか | N-01〜N-06 | Sweep B | Opus |
| R-05 | API no-auto-migration | API startupがschemaを変えないか | before / after fingerprint | Sweep B | Sol + Opus |
| R-06 | Secret safety | repo、argv、logs、configへsecretが出ないか | sentinel probe | Sweep A | Opus |
| R-07 | Test assurance | test名・主張・assertion・mutationが一致するか | mutation results | Sweep B | Opus |
| R-08 | Reproducibility | start/stop/restart/resetが再現できるか | exact commands / logs | Sweep A | Sol |
| R-09 | Image / volume contract | digest pin、named volumeが固定されるか | rendered config / inspect | Sweep A | Sol |
| R-10 | CI identity | reviewed Headとcheckout SHAが一致するか | direct-head / merge-ref | Sweep A | Sol |

軽量reviewerの出力は、最低限次のschemaを持つ。

```yaml
finding_id:
severity:
viewpoint_id:
claim:
primary_evidence:
probe_executed:
mutation_executed:
expected_result:
observed_result:
scope_of_fix:
heavy_review_attention_required: true|false
```

---

## 10. FND-05 Recommended Execution Process

### Phase 0 — Design and Gate Lock

1. Issue #43、ADR-0001 / 0008 / 0009、FND-04 final implementationを確認する。
2. `Implementation and Test Design Contract`を作成する。
3. Solを1回だけDesign Leadとして投入する。
4. unresolved decisionをKooへ提示する。
5. review perspective matrixとmandatory mutationsをlockする。
6. Issue Readyを正式評価する。
7. PASSするまでcandidate実装を開始しない。

### Phase 1 — Candidate H0

```yaml
candidate_pool:
  default: 6
  core: 4
  challengers: 2
  conditional_expansion_to_8:
    - top candidates are materially tied but evidence differs
    - required harness coverage is missing
    - a new model is intentionally re-entered
```

- 全candidateを同一Base / 同一Contractで実行する。
- H0 exact HeadとCIを固定する。
- durationは分単位で必須収集する。精度より収集率を優先する。
- candidateによるIssue / ADR変更を禁止する。

### Phase 2 — Risk-Based Self-Review

Formal Self-Review対象は次に限定する。

- 上位2〜3candidate
- 少なくとも1 challenger
- evidence gapを持つcandidate
- test oracleやfailure pathが弱いcandidate

finding 0 / no-changeの場合、独立H1 executionを作らない。H0 aliasとして記録する。H1 CIは変更candidateのみ実行する。

### Phase 3 — Selection and Curated Final Synthesis

- candidateをそのままmerge / cherry-pickしない。
- architecture、runtime behavior、test、secret handlingを要素単位で選択する。
- rejected patternもmandatory regressionへ変換する。
- Final Synthesisは方針確定後の実装なので、原則としてLuna等の規律あるImplementerで実施できる。
- Final Synthesis Headでdirect-head CIを成功させる。

### Phase 4 — Lightweight Sweep Review

重いreview前に2本実施する。

#### Sweep A — Contract / Scope / Static Review

- ADR / Issue traceability
- scope drift
- image digest / named volume
- secret placement
- command / documentation reproducibility
- CI identity

#### Sweep B — Runtime / Failure / Test Assurance Review

- Compose state machine
- migration failure injection
- API non-start
- API no-auto-migration
- mandatory mutations
- test oracleのpositive marker

明白なfindingをLuna等で修正し、CIを再実行する。ここでHeadを安定させる。

### Phase 5 — Heavy Final Gate

安定した同一Headへ、fresh contextで各1回だけ投入する。

#### Sol Final Review

- ADR / Issue / AC整合
- responsibility boundary
- scope
- exact identity
- missing decision
- implementation / test design contractへの適合

#### Opus Final Review

- lifecycle
- failure path
- container / process ownership
- secret / implicit dependency
- false assurance
- test reachability
- mutation sensitivity

両者のfindingを多数決で処理しない。Blocker / Majorのroot cause、required fix、merge readinessが一致するかを確認する。

### Phase 6 — Conditional Adjudication

次の場合だけJudgeを追加する。

- Blocker / Major severityが割れる
- root causeが割れる
- required fix方向が割れる
- merge readinessが割れる
- SolとOpusに共通盲点の疑いがある

一致している場合、独立Gold artifactのためだけに追加実行しない。quorum synthesisをmachine-readable stateへ記録する。

### Phase 7 — Fix and Re-review

- fixはselected Final Synthesis branchへ行う。
- required fix方向が一意なら1実装者・1回を標準とする。
- lightweight verifierがmutationと回帰を確認する。
- heavy re-reviewはfinding ownerだけを原則とする。
- architecture / security / cross-cutting変更の場合のみSol＋Opusを再投入する。

### Phase 8 — Merge and Close

次を分離して記録する。

```yaml
technical_verdict:
github_review_state:
repository_enforcement_snapshot:
reviewed_head_sha:
direct_head_ci:
merge_ref_ci:
merge_commit_sha:
main_tree_identity:
issue_close_evidence:
```

benchmark control stateは単一JSONを正本とし、Markdown statusを生成する。Issue close時にmainから見える状態を実態と一致させる。

---

## 11. KEEP / MODIFY / NEW / DROP

### KEEP

- Issue Ready / fixed contract
- common Base / candidate identity lock
- H0 immutable snapshot
- challenger枠
- evaluator reference / probesの事前lock
- Selection / Adjudication
- current mainからのcurated Final Synthesis
- exact direct-head / merge-ref CI
- targeted fix
- mutation付きtargeted re-review
- Technical Approval / GitHub State / Repository Enforcementの分離
- merge / close evidence bundle

### MODIFY

- candidate 8 → 基本6
- Formal Self-Review全件 → risk-based
- H1全件 → changed candidateのみ
- role-diverse review 5本 → lightweight 2本＋heavy 2本
- 2 Judge常設的運用 → Sol / Opus不一致時のみJudge
- Formal Agent Bの重複full review → Sol / Opus final gateへ統合
- re-review範囲 → severityではなくblast radius基準
- duration計測 → 分単位の単純・高収集率方式
- artifact管理 → single JSON + generated Markdown
- docs-only変更のCI → product/testへ影響するときだけ

### NEW

- ADR-based Implementation and Test Design Gate
- Review Perspective Matrixのpre-run lock
- Lightweight Sweep Review
- Heavy Model Invocation Budget
- mandatory mutation set
- finding-owned re-review
- light→heavy escape-rate測定
- ADR→implementation→test→review evidenceのtraceability matrix
- artifact stale-state lint

### DROP

- 同質reviewerの大量投入
- 全candidateのno-change H1 execution
- Major発見後の全candidate再実装
- Minor / Nit修正後のheavy full review
- finding normalization、Gold、clearanceを理由にした独立実行の乱立
- GitHub timestampからagent処理時間を推測すること
- docs-only commitごとのfull product CI
- model数や評判を品質証拠として扱うこと

---

## 12. FND-05 Experiments and Metrics

FND-05では、新プロセスが本当に有効か測る。

### Experiment A — Lightweight Sweep Effectiveness

```yaml
measure:
  - light_findings_total
  - light_blocker_major
  - findings_fixed_before_heavy
  - heavy_unique_findings
  - heavy_findings_that_light_should_have_caught
```

目的は軽量モデルがheavy modelを置き換えることではない。重いreviewerが明白なfindingへ時間を使わず、深いriskへ集中できたかを見る。

### Experiment B — Heavy Model Efficiency

```yaml
target:
  heavy_full_review_invocations: 2
  heavy_re_review_default: 0
  conditional_heavy_re_review: "Blocker/Major or material blast radius only"
```

### Experiment C — ADR-First Design Stability

```yaml
measure:
  - unresolved_decisions_before_h0
  - design_changes_after_h0
  - adr_or_issue_change_after_candidate_start
  - scope_drift_findings
```

目標は、candidate開始後の未承認設計変更を0にすること。

### Experiment D — Mutation Kill Rate

```yaml
target:
  mandatory_mutations_defined_before_h0: 100%
  mandatory_mutations_killed_by_final_tests: 100%
  mutation_residue_after_review: 0
```

### Experiment E — Process Cost

```yaml
measure:
  - candidate_count
  - self_review_count
  - h1_changed_count
  - light_review_count
  - heavy_review_count
  - judge_count
  - fix_round_count
  - review_iteration_count
  - duration_coverage
  - docs_only_ci_runs
```

FND-04の34 logical slotsを基準とした場合、FND-05の標準形は概ね18〜21 slotsを目標とする。ただし、これは品質を削る目標ではなく、重複工程をlightweight sweepと事前設計へ置き換える目標である。

---

## 13. Stop Conditions for FND-05

次の場合、implementationを開始または継続しない。

- Issue #43 Issue ReadyがPASSしていない。
- PostgreSQL→Migrator→APIの成功条件を説明できない。
- 単なる`depends_on`だけで成功保証しようとしている。
- API startの観測方法がFND-06 health endpoint先取りを必要とする。
- secret injection方式が未固定である。
- migration failure injectionがproduction codeへのtest hook追加を必須とする。
- API startup auto-migrationが必要になる。
- business schema、health、backup、production orchestratorを同時実装しないと成立しない。
- mandatory mutationを検出できるtest oracleを設計できない。
- candidateがADRにない重要設計判断を独自補完する必要がある。

---

## 14. Practical Default Profile After FND-05

FND-05はbenchmarkを兼ねるため6 candidateを残すが、通常開発の標準形はさらに軽くできる。

```text
ADR / Issue / Test Design Lock
  ↓
Luna implementation
  ↓
Lightweight Sweep A / B
  ↓
obvious fixes + CI
  ↓
Sol final contract review
  +
Opus final adversarial review
  ↓
conditional targeted fix / re-review
  ↓
merge
```

この形では、SolとOpusを「何度も考え直させるモデル」ではなく、「最後に独立して止めるモデル」として使う。

---

## 15. Final Decisions

### FND-04最終判定

**研究runとして成功。通常開発標準としては過剰。**

品質を維持したまま工程を減らせる一次証拠がある。減らす対象は独立性やfailure-path verificationではなく、重複review、全candidate Self-Review、no-change H1、artifact運用である。

### FND-05で最も重要な変更

**review観点とtest mutationを実装前に固定すること。**

FND-04のMajorは、実装者が決められたguardを入れなかったことより、そのguardが何を検出すべきかをSelection時点で十分に固定していなかったことから生じた。FND-05では、実装設計とテスト設計を同じGateでlockする。

### FND-05で最も重要な新実験

**軽量事前レビュー → 重量最終レビューの二段階方式。**

軽量モデルの目的は最終承認ではなく、広く洗ってノイズを減らすことである。Sol / Opusの目的は、安定Headに対する最後の独立停止判断である。この役割分離が有効かを、escape rateとheavy invocation countで測る。

### FND-05開始前の推奨アクション

1. 本レポートをreviewし、FND-05採用方針をKooが決定する。
2. Issue #43向け`Implementation and Test Design Contract`を作成する。
3. Review Perspective Matrixとmandatory mutation setを正式lockする。
4. Issue #43のGate statusを一次証拠から再評価する。
5. PASS後にcandidate pool、branch、promptを準備する。
6. それまではDocker Compose実装を開始しない。

---

## 16. Source Links

- [FND-04 Final Synthesis PR #140](https://github.com/kooiei-in4a/minimal-bank-system/pull/140)
- [FND-04 Sol retrospective PR #141](https://github.com/kooiei-in4a/minimal-bank-system/pull/141)
- [FND-04 Opus retrospective PR #142](https://github.com/kooiei-in4a/minimal-bank-system/pull/142)
- [FND-04 Cursor Auto retrospective PR #143](https://github.com/kooiei-in4a/minimal-bank-system/pull/143)
- [FND-05 Issue #43](https://github.com/kooiei-in4a/minimal-bank-system/issues/43)
- [ADR-0001](../adr/0001-application-platform-baseline.md)
- [ADR-0008](../adr/0008-audit-logging-technical-logging-and-backup.md)
- [ADR-0009](../adr/0009-database-schema-migration-and-rollback.md)
