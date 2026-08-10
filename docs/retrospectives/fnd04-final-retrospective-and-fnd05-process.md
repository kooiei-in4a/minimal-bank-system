# FND-04 最終振り返り — ADR-first設計とFND-05レビュー・ファネル

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
    EXECUTION: "GPT-5.6 Sol / Browser / Pro"
  - PR: 142
    EXECUTION: "Claude Opus 5 / Claude Code / xHigh"
  - PR: 143
    EXECUTION: "Grok 4.5 + Composer 2.5 / Cursor Auto"
ADDITIONAL_DIRECTION_OWNER: "Koo"
SYNTHESIS_DATE: "2026-08-10"
```

## 1. 結論

FND-04は、FND-03より明確に改善した。

初期candidateを14から8へ減らし、17本の同質reviewを5本のrole-diverse reviewへ置き換え、Judgeを3名常設から2名＋条件付き第3 Judgeへ変更した。Major発見後も14候補を再実装させず、1件のtargeted fixと2件のtargeted re-reviewで閉じた。その結果、Final Synthesisに残っていたtest oracleのMajorをmerge前に発見し、production architectureへ触れずに修正できた。

一方、FND-04の方式を通常開発へそのまま適用するなら過剰である。

- 全8candidateへの独立Formal Self-Review
- no-change candidateを含むH1管理
- 5 review、2 Judge、Gold、2 re-review、Formal Agent B
- benchmark control artifactごとの細かなcommitとCI

これらは研究計装としては有効だったが、日常的な開発プロセスには重い。

FND-05では次の方式へ移行する。

> **ADR・Issue・実装設計・テスト設計・確認観点を先に固定し、3モデルで実装する。軽量モデルで広く洗い、安定した最終HeadだけをSolとOpusで各1回深く確認する。**

これは単純なreview削減ではない。浅い問題を前段で除去し、重いモデルをBlocker / Majorの探索へ集中させるリスク・ファネルである。

---

## 2. FND-04で確認できたこと

### 2.1 有効だったこと

1. **Issue Ready / fixed contract**
   - candidateごとの仕様解釈差を抑えた。
2. **common baseとexact Headの固定**
   - branch差、CI targetの混同、後付け評価を抑えた。
3. **Selection / Adjudication**
   - winner branchをそのままmergeせず、採用要素とreject patternを分離した。
4. **curated Final Synthesis**
   - current mainから製品用実装を再構成し、candidate由来の不要差分を持ち込まなかった。
5. **mutationを伴うreview / Judge**
   - `CI green`や`exit != 0`だけではtest oracleの正しさを証明できないことを実証した。
6. **targeted fix**
   - root causeが限定できるMajorを1 fileの修正で閉じた。
7. **Technical Approval / GitHub Review State / Repository Enforcementの分離**
   - 技術判定とplatform上の状態を混同しなかった。

### 2.2 過剰だったこと

1. 全candidateへの独立Formal Self-Review
2. Finding 0 / code change 0 candidateのH1再実行
3. 同じfindingをJudge、re-review、Formal Agent Bで何度も再現すること
4. docs-only変更ごとにproduct CIを走らせること
5. 同じ状態を複数Markdown / JSONへ手作業で複製すること

### 2.3 最も重要な技術的知見

> **green testは、そのtestが守るべき欠陥を検出できることを意味しない。**

FND-04のMajorでは、production behavior自体は正しかったが、negative testが無関係な失敗でもgreenになり得た。

今後のnegative testでは、次を要求する。

- 対象component / pathへ到達したpositive marker
- 期待したfailure reasonまたはstateのpositive marker
- 守るべきdefect classを再導入したcontrolled mutationでREDになること
- mutationを戻した後にGREENへ回復し、残渣がないこと

---

## 3. FND-05で確定したモデル構成

OpenCodeは使用しない。

### 3.1 実装candidate — 3本

| Slot | Model + Harness | 目的 |
| --- | --- | --- |
| C1 | GPT-5.6 Luna / Codex | ADR・Issueへ忠実な基準実装 |
| C2 | Claude Sonnet 5 / Claude Code | GPT系と異なる実装解釈を確保 |
| C3 | Grok 4.5 / Cursor | 別Harness・運用経路・Compose実装の異質性を確保 |

3候補で見るのはModel単体ではなく、`Model + Harness + Effort + 1 execution attempt`である。

### 3.2 Light Review — 2本

| Slot | Model + Harness | 役割 |
| --- | --- | --- |
| L1 | Composer 2.5 / Cursor | Project Quality / Rule Conformance Sweep |
| L2 | GPT-5.6 Luna / Codex | ADR / Issue / AC Contract Conformance Sweep |

### 3.3 Heavy Final Review — 2本

| Slot | Model + Harness | 役割 |
| --- | --- | --- |
| H1 | GPT-5.6 Sol / Codex | Architecture / Contract Final Gate |
| H2 | Claude Opus 5 / Claude Code | Adversarial / Failure / False-Assurance Final Gate |

Heavy reviewerは安定した最終Headへ原則1回だけ投入する。

---

## 4. Self-Reviewの扱い

FND-05では独立したFormal Self-Review phaseを廃止する。

ただし、`AGENTS.md`が要求するAuthorの基本的な自己確認を削除するわけではない。実装promptへ、確認すべき観点をあらかじめ固定して組み込む。

```text
FND-04
  H0 implementation
  → fresh Formal Self-Review
  → H1 fix

FND-05
  Implementation prompt内に確認観点・必要証拠・mutationを明記
  → 1回の実装executionで実装・検証・diff確認まで完了
```

「最後にセルフレビューせよ」という自由形式の指示は使わない。

代わりに、実装promptへ次を明記する。

- ADR / Issue / Scope / Out of scope
- project rule catalog
- placement / dependency rules
- startup ordering contract
- failure path
- test oracle requirements
- mandatory mutations
- required commands and evidence
- prohibited implementation patterns
- final diff check

これにより、モデル自身がreview観点を後付けで考えるのではなく、事前に固定されたDefinition of Doneへ沿って実装する。

独立Self-Review execution数は0とする。

---

## 5. Project Conformance ReviewをHeavy Reviewの前へ置く

Heavy Review前に、コード品質とproject rule遵守を確認する軽量ゲートを置く。

### 5.1 Static / Automated Check

機械判定できるものを最初に実行する。

- restore / build / test
- `docker compose config --quiet`
- resolved service / image / volume確認
- prohibited path / secret scan
- package / digest / Dockerfile base image確認
- `git diff --check`
- changed-file allowlist
- CI checkout identity

### 5.2 Composer — Project Quality / Rule Conformance

Composerは広く速く確認する。

- project rule違反
- 責務と配置の不一致
- Compose syntax / structure
- command / entrypoint / environment / volume / portの明白な問題
- 重複、不要設定、magic value
- exception / exit codeの握り潰し
- secretやcredentialの露出
- test名・コメント・assertionの不一致
- scope外のhealth、backup、business schema先取り

目的は、Heavy reviewerへ明白な問題を持ち込まないことである。

### 5.3 Luna — ADR / Issue / AC Contract Conformance

Lunaは新しい設計を提案せず、traceabilityを確認する。

```text
ADR / Issue requirement
  → implementation location
  → runtime behavior
  → test / verification
  → evidence
```

各Acceptance Criteriaについて、実装・test・証拠がつながっているかを確認する。

Light Review後にfindingを修正し、CIを再実行し、Headを固定する。その固定HeadだけをHeavy Reviewへ渡す。

---

## 6. Heavy Reviewの責任範囲

Heavy Review promptには、見る項目と見ない項目を同じ強さで明記する。

### 6.1 Sol — Architecture / Contract Final Gate

#### 確認する

- Accepted ADRの意図と最終設計の整合性
- Issue #43の本質的充足
- PostgreSQL / Migrator / APIの責務境界
- FND-04 / FND-05 / FND-06の境界
- migration成功後だけAPIを開始する設計
- migration失敗時にAPIを開始しない設計
- API startup no-auto-migration
- security / secret設計の重大な逸脱
- mergeを止めるべきarchitecture / scope defect

#### 原則確認しない

- formatter / whitespace
- unused using
- 細かな命名
- 軽微なコメント表現
- READMEの軽微な文言
- 通常のDRY指摘
- magic string等の局所的なcode quality
- 単純なファイル配置の全件再監査
- package / digestの単純な存在確認
- AC checklistの機械的な再実行
- Light Reviewで解消済みのMinor / Nit

ただし、除外項目がBlocker / Majorのroot causeへ直結する場合は指摘できる。

### 6.2 Opus — Adversarial / Failure Final Gate

#### 確認する

- partial failure
- startup / shutdown / restart lifecycle
- ordering / race
- container / volume / process ownership
- failure exit semantics
- retry / rerun behavior
- unexpected fallback
- secret / credentialの重大な漏洩経路
- fail-open
- hidden dependency
- false assurance
- testが本当に対象pathへ到達しているか
- mutationで守るべき欠陥を検出できるか

#### 原則確認しない

- code style
- naming
- formatter
- unused codeの軽微な問題
- 通常のDRY
- 単純なdirectory placement
- README typo
- package versionの単純照合
- AC一覧の全件再監査
- Light Review済みの一般的quality finding
- 承認済み設計を好みで全面変更する提案

Opusの目的は「より好みの設計」を提案することではない。承認済み設計を壊す重大欠陥を探すことである。

### 6.3 Heavy Reviewの成功条件

- Blocker / Majorを見逃さない
- Light Reviewの仕事を繰り返さない
- 指摘数を増やすことを目的にしない
- 推奨改善とmerge blockerを混同しない

Heavy Reviewで単純rule違反が多数見つかった場合、Heavy reviewerの成果ではなく、Light Gateのfailureとして記録する。

---

## 7. FND-05のADR-first設計Gate

Issue Readyの前に、`Implementation and Test Design Contract`を固定する。

最低限、次を決める。

### 7.1 Runtime design

```text
PostgreSQL container starts
  ↓
PostgreSQLが接続可能になる
  ↓
one-shot Migrator starts
  ├─ exit 0
  │    ↓
  │  API start is permitted
  └─ non-zero
       ↓
     API must not start
```

Composeのdependency conditionは実装手段として使用できるが、`depends_on`の記載だけをverification evidenceにはしない。

### 7.2 FND-06を先取りしない観測

FND-05ではhealth endpointを追加しない。

API開始順序は次で観測する。

- Migrator containerのexit code
- Migrator containerのfinished timestamp
- API containerのcreated / started / running state
- API containerのstarted timestamp
- migration history
- Compose logs

API containerの開始時刻がMigrator成功終了より後であることを確認する。migration failure時はAPI containerが開始されていないことを確認する。

### 7.3 Secret design

- repositoryへcredentialを保存しない
- password / connection stringをcommand-line argumentへ展開しない
- serviceごとに必要なsecretだけを付与する
- rendered Compose config、logs、process args、PR diffへsentinelが出ないことを確認する

### 7.4 Lifecycle design

- clean start
- stop
- restart
- rerun with existing named volume
- clean reset
- migration failure
- cleanup後のresource absence

を事前にtest scenarioとして固定する。

---

## 8. FND-05 mandatory mutation

実装開始前に、最低限次のmutationを固定する。

| ID | Mutation | 守るcontract |
| --- | --- | --- |
| M-01 | API dependencyをMigrator成功から単なるservice startへ弱める | 成功後だけAPI開始 |
| M-02 | Migrator failureをexit 0へ変換する | fail-closed |
| M-03 | API startupへmigration実行を追加する | API no-auto-migration |
| M-04 | passwordをCompose commandへ展開する | secret非露出 |
| M-05 | digest参照をtag-onlyへ変える | image pinning |
| M-06 | named volumeをanonymous / bind誤設定へ変える | persistence contract |
| M-07 | intended path到達前に無関係な失敗を発生させる | test reachability |
| M-08 | migration history未作成でもsuccess扱いする | migration完了証拠 |

Final Synthesisのtest / validatorは、該当mutationでREDになり、復旧後GREENになることを示す。

すべてのcandidateへ全mutation実行を強制する必要はない。candidate実装promptでは対応するtest oracleを要求し、Final Synthesisでmandatory mutation setを完全実行する。

---

## 9. FND-05実行フロー

```text
1. Parent #3 / WP-1 #33 / Issue #43確認
2. ADR / Issue / Scope lock
3. Implementation and Test Design Contract lock
4. Project Rule Catalog lock
5. Review Perspective Matrix lock
6. Mandatory Mutation Set lock
7. common base / 3 candidate branches lock
8. 3 independent implementations
   - Luna / Codex
   - Sonnet / Claude Code
   - Grok / Cursor
9. common evaluation / Selection / Adjudication
10. curated Final Synthesis
11. Static Project Rule Check
12. Composer Project Quality Sweep
13. Luna Contract Conformance Sweep
14. Light findings fix / CI / Final Head lock
15. Sol Heavy Final Review — 原則1回
16. Opus Heavy Final Review — 原則1回
17. B0 / M0ならmerge gate
18. B/Mありならtargeted fix
19. blast radiusに応じたfinding-owned re-review
20. repository rule再取得 / merge / main identity / close evidence
```

Judgeは通常工程へ置かない。

次の場合だけConditional Judgeを発動する。

- SolとOpusでBlocker / Major有無が割れる
- root causeが割れる
- required fix方向が実質的に異なる
- merge readinessが割れる
- 両者が同じ未検証assumptionへ依存している

---

## 10. 修正後の再確認方針

Heavy modelを修正のたびに一律再投入しない。

| Change / Finding | Re-review |
| --- | --- |
| Minor / Nit / docs-only | Light reviewer + CI |
| test-only Major | finding owner 1名 + lightweight mutation verifier |
| localized production Major | finding owner + adjacent heavy perspective 1名 |
| architecture / security / cross-cutting Major | Sol + Opus |
| Head unchanged | 再reviewなし |

判断基準はseverityだけでなくblast radiusと責務変更の有無とする。

---

## 11. KEEP / MODIFY / NEW / DROP

### KEEP

- Issue Ready / fixed contract
- common baseとcandidate branch identity
- 3 candidateの独立性
- H0 snapshot
- common evaluator probes
- Selection / Adjudication
- candidate merge / cherry-pick禁止
- curated Final Synthesis
- exact Head / merge-ref identity
- targeted fix
- merge / close evidence

### MODIFY

- candidate 8 → 3
- review 5＋Judge 2 → Light 2＋Heavy 2
- Formal Agent B full review → Sol / Opusの明確なfinal gateへ再構成
- self-review別phase → implementation prompt内の固定確認観点
- re-review → finding owner / blast radius基準
- artifact複製 → single machine-readable stateから生成
- docs-only CI →必要最小限

### NEW

- ADR-based Implementation and Test Design Gate
- Project Rule Catalog
- Review Perspective Matrix
- Heavy Reviewの`DO NOT CHECK`定義
- Composer Project Quality Sweep
- Luna Contract Conformance Sweep
- mandatory mutation set
- Heavy Model Invocation Budget
- Light-to-Heavy escape rate
- rule conformance escape rate

### DROP

- OpenCode
- 独立Formal Self-Review phase
- finding 0 candidateのH1実行
- 同質reviewer大量投入
- Major後の全candidate再実装
- Minor / Nit後のHeavy full re-review
- Judge常設
- docs-only変更ごとのproduct full CI
- GitHub timestampからのagent duration推定

---

## 12. FND-05で測る指標

### 12.1 Implementation

```yaml
candidate_count: 3
separate_formal_self_review_count: 0
h1_phase_count: 0
open_code_execution_count: 0
```

### 12.2 Light Review

```yaml
measure:
  - static_rule_failures
  - composer_findings_total
  - luna_findings_total
  - findings_fixed_before_heavy
  - rule_conformance_escape_to_heavy
  - obvious_quality_escape_to_heavy
```

### 12.3 Heavy Review

```yaml
target:
  sol_full_review_invocations: 1
  opus_full_review_invocations: 1
  default_heavy_re_review_invocations: 0
measure:
  - heavy_unique_blocker_major
  - heavy_obvious_findings
  - heavy_role_overlap_findings
  - conditional_judge_triggered
```

`heavy_obvious_findings`の目標値は0である。

### 12.4 Test design

```yaml
target:
  mandatory_mutations_defined_before_candidate_start: 100%
  final_mutation_kill_rate: 100%
  mutation_residue: 0
measure:
  - design_changes_after_candidate_start
  - test_design_changes_after_candidate_start
  - false_assurance_findings
```

### 12.5 Cost

```yaml
measure:
  - model_execution_count
  - review_execution_count
  - fix_round_count
  - duration_coverage
  - docs_only_ci_runs
  - human_decision_count
```

処理時間はexecution wrapperまたはagentの開始・終了記録から取得する。取得できない場合はN/Aとし、GitHub timestampから補完しない。

---

## 13. FND-05開始前の停止条件

次を満たすまでimplementationを開始しない。

- Issue #43 Issue Ready = PASS
- FND-04 final retrospective方針がreview済み
- Implementation and Test Design Contractが固定済み
- Project Rule Catalogが固定済み
- Review Perspective Matrixが固定済み
- mandatory mutation setが固定済み
- candidate 3本のexact identity / branch / common baseが固定済み
- model / harness / effortの実表示を実行直前に確認済み
- secret injection方式が固定済み
- API start orderingの観測方法が固定済み
- FND-06を先取りしないことを確認済み

次の場合も停止する。

- 単なるshort syntax `depends_on`でDB readinessまたはmigration successを代替しようとする
- API startup auto-migrationが必要になる
- failure injectionのためにproductionへ専用backdoorを追加する必要がある
- business schema、health endpoint、backup、production orchestratorを同時実装する必要がある
- mandatory mutationを検出できるtest oracleを設計できない
- ADRにない重要設計判断をcandidateが独自補完する必要がある

---

## 14. 通常開発へ展開する場合の標準形

FND-05は3候補benchmarkを兼ねるが、通常Issueではさらに簡潔にできる。

```text
ADR / Issue / Test Design Lock
  ↓
Luna implementation
  ↓
Static checks
  ↓
Composer project-rule sweep
  + Luna contract sweep
  ↓
light fix / CI
  ↓
Sol final contract review
  + Opus final adversarial review
  ↓
conditional targeted fix / re-review
  ↓
merge
```

SolとOpusは「何度も考え直させるモデル」ではなく、「最後に独立して止めるモデル」として使用する。

---

## 15. 最終判断

### FND-04

**研究runとして成功。通常開発標準としては過剰。**

### FND-05

**上流設計＋軽量事前review＋重量最終gateを検証するrunとする。**

最も重要な変更は、review観点とtest mutationを実装前に固定することである。

最も重要な新実験は、Composer / Lunaで明白な問題を除去した後、Sol / Opusを各1回だけ使用して品質を維持できるかを測ることである。

---

## 16. Source Links

- FND-04 Final Synthesis: https://github.com/kooiei-in4a/minimal-bank-system/pull/140
- Sol retrospective: https://github.com/kooiei-in4a/minimal-bank-system/pull/141
- Opus retrospective: https://github.com/kooiei-in4a/minimal-bank-system/pull/142
- Cursor Auto retrospective: https://github.com/kooiei-in4a/minimal-bank-system/pull/143
- FND-05 Issue: https://github.com/kooiei-in4a/minimal-bank-system/issues/43
- ADR-0001: ../adr/0001-application-platform-baseline.md
- ADR-0008: ../adr/0008-audit-logging-technical-logging-and-backup.md
- ADR-0009: ../adr/0009-database-schema-migration-and-rollback.md
- Docker startup order: https://docs.docker.com/compose/how-tos/startup-order/
- Compose services / depends_on: https://docs.docker.com/reference/compose-file/services/
- Compose secrets: https://docs.docker.com/compose/how-tos/use-secrets/
- Compose config: https://docs.docker.com/reference/cli/docker/compose/config/
