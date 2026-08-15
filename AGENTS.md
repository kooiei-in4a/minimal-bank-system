# AGENTS.md

## 1. 目的

本リポジトリで作業する人間およびAIエージェントは、要件定義からリリースまでの追跡可能性を維持する。

## 2. 正本の優先順位

1. Kooが承認した製品方針および仕様
2. Kooが承認したprocess decision package（process / controlに適用）
3. `Accepted`状態のADR
4. GitHub Issueで定義された作業範囲
5. コードおよび自動テスト
6. Pull Requestの説明とコメント

下位成果物が上位成果物と矛盾する場合、下位成果物を修正する。IssueまたはPRコメントだけで仕様やADRを暗黙変更してはならない。

## 2.1 プロジェクト統制Issue

プロジェクト全体の進行は、Parent Issue #3で管理する。

- Parent Issue: https://github.com/kooiei-in4a/minimal-bank-system/issues/3
- 計画、仕様化、ADR作成、Issue分割、実装、レビュー、merge、releaseに着手する前に、必ずParent Issue #3を確認する。
- 確認対象は、現在フェーズ、前提フェーズゲート、対象作業を管理するIssue、未決定のBlocker、現在の禁止事項、プロジェクト目的と対象外とする。

Parent Issue #3は、進行、フェーズ、ゲート、Blocker、子Issueおよび検証証拠を管理する統制Issueであり、仕様、ADR、受入条件または設計判断の正本ではない。

Parent Issue #3と、Kooが承認した仕様または`Accepted`状態のADRが矛盾する場合は、承認済み仕様またはADRを優先し、矛盾を報告して停止する。Parent Issue #3のコメントまたはチェックボックスだけで、仕様またはADRを暗黙変更してはならない。

## 2.2 Rolling WaveとIssueの追跡ルール

各作業は、原則として次のRolling Wave hierarchyから追跡可能な対象Issueを持つ。

```text
Parent / Control Issue #3
  -> current Work Package Issue
    -> target leaf Issue
```

対象Issueには、最低限、次を記録する。

- Parent / Control Issue: #3
- Work Package Issue
- target leaf / target Issue
- Current Authority
- Required gate
- Required gate status

専用の対象Issueが必要な作業であるにもかかわらず対象Issueが存在しない場合は、独断でIssueを作成したり、実装へ進んだりせず、停止して報告する。

Parent Issue #3またはWork Package Issueの詳細をleaf Issueへ複製する必要はない。具体的な作業範囲、対象外、Acceptance Criteriaおよび操作権限は、対象となるleaf Issueを正本とする。

## 2.3 Single Current Authority

- 各control levelでCurrent Authorityは1つとする。
- historical Current Authorityはimmutable evidenceとして保持し、書き換えない。
- 新しいCurrent Authorityは、旧記録を明示的にsupersedeする新規recordとして追加する。
- historical stateをcurrent stateとして使用しない。

## 3. 成果物の責任範囲

- `docs/requirements/`: 受領した原始要件。内容を黙って書き換えない。
- `docs/reviews/`: 要件・仕様・設計・リリース成果物のレビュー結果。
- `docs/specs/`: 承認済みの製品挙動、契約、受入条件。
- `docs/adr/`: 重要かつ変更コストの高い設計判断。
- `docs/plans/`: 実行計画。仕様やADRの代替にしない。
- `docs/benchmarks/`: AIモデル／Agent等の比較実験、評価方法、結果、再現用snapshotを記録する。製品仕様、ADR、実装Issueの正本にはしない。
- `docs/retrospectives/`: retrospective記録、およびKoo承認済みprocess decisionを保持する。全retrospectiveがauthorityになるわけではなく、Koo承認済みdecisionのみをprocess authorityとする。
- `docs/traceability/`: 要件、仕様、Issue、PR、テスト、リリース証拠の対応関係。
- `docs/releases/`: リリース判定、手順、結果、既知制約。

## 4. 役割

### Koo: Product Owner / Decision Owner

- 要件上の未決事項を決定する。
- 仕様およびADRを承認する。
- AIだけでは決めるべきでない重要なトレードオフやprocess decisionを決定する。
- 明示的に活性化されたbounded pilot scope（explicitly activated bounded pilot scope）内に限り、既定ルールと客観的証拠から一意に決まる定型的なstage progressionを毎回semantic approvalしない。pilot適用範囲外では、既定の`HUMAN_APPROVAL: required`を維持する。
- 最終的なGo / No-Goを判断する。

### Agent A: Author / Implementer

- 探索、計画、実装、テスト、セルフレビューを行う。
- Draft PRを作成し、検証証拠と未検証事項を明示する。
- 仕様不足を独自解釈で埋めない。

### Agent B: Independent Reviewer

- 実装者の説明を前提にせず、仕様、ADR、Issue、差分、テストの順で再検証する。
- 原則としてレビュー対象のコードを変更しない。
- Blocker / Major / Minor / Nitで指摘を分類する。

### Agent C: Release Reviewer

- Release Candidate、migration、デプロイ、ロールバック、smoke testの証拠を独立検証する。

## 5. 共通作業フロー

1. Parent Issue #3を確認し、現在フェーズ、前提ゲート、Blocker、禁止事項を確認する。
2. 対象Issueと正本を確認する。
3. 対象IssueがParent Issue #3から追跡可能であることを確認する。
4. 対象範囲、対象外、依存関係を確定する。
5. 計画を作成し、仕様・ADRとの整合性をセルフレビューする。
6. 許可された変更だけを実施する。
7. 自動テストと必要な手動検証を実施する。
8. 差分をセルフレビューする。
9. Draft PRに証拠、未検証事項、既知リスクを記録する。
10. Agent Bの独立レビューを受ける。

## 5.1 Stage Entry Check

重要なstageまたはagent launchの前に、最低限、次を確認する。全コマンド実行前の巨大なチェックリストにはしない。

```text
PREVIOUS_STAGE_COMPLETE
NEXT_STAGE_AUTHORIZED
CONTROL_STATE_CONSISTENT
TARGET_SHA_EXACT
NO_UNRESOLVED_BLOCKER_MAJOR
```

対象leafでv2 Transition Bundle pilot（§5.5）が`ACTIVE`な場合は、上記に加えて`docs/retrospectives/wp2-human-decision-handoff-pilot-v2.md`のBundle completeness gate（Transition Bundleの4要素が揃い、同一exact identityで一致していること）も満たす。

## 5.2 Write Preflightとmain保護

GitHubへの最初のwriteより前に、次を明示して確認する。これはmechanical preflightであり、新しいsemantic review工程ではない。

```yaml
WRITE_PREFLIGHT:
  TARGET_REPOSITORY:
  TARGET_BRANCH:
  EXPECTED_BASE_SHA:
  WRITE_SCOPE:
  DIRECT_MAIN_WRITE_ALLOWED: false
```

Issue comment、PR comment、Ruleset操作等のbranchless GitHub writeでは、`TARGET_BRANCH`および`EXPECTED_BASE_SHA`は`N/A`としてよい。

通常運用では`main`へ直接writeしてはいけない。commit、push、GitHub file API write、その他のrepository content writeは、必ずbranchとPull Requestを経由する。`main`への直接writeが必要に見える場合は停止して報告する。

## 5.3 Review policy

- Light Reviewは1件のsemantic reviewとする。
- Heavy Reviewは1件を既定とし、2件目はrisk-basedで判断する。risk signalの詳細はKoo承認済みprocess decision packageを参照する。
- narrow fixは変更範囲に対するtargeted re-reviewを行い、full rerunを既定にしない。

## 5.4 Critical Mutation

Critical Mutationはhigh-risk leafにだけ適用し、leafあたり最大3件とする。全testをmutation対象にせず、各mutationでsemantic failure signatureを必須とする。

```text
BASELINE_GREEN
-> mutation
-> MUTATION_RED
-> expected semantic failure confirmed
-> restore
-> RESTORE_GREEN
```

単なるREDは成功証拠ではない。

## 5.5 Agent launchとJIT Handoff

通常のagent自動起動はまだ行わない。ただし、Agent launchの実操作を人間が行うことと、stage progressionのsemantic approvalを人間が行うことを分離する。

### v1 contract（historical / WP2-AUTHN-01限定・完了済み）

WP2-AUTHN-01では、Issue #191および次のKoo-approved pilot contract（v1）を適用した。

- `docs/retrospectives/wp2-human-decision-handoff-pilot.md`

```yaml
PILOT_SCOPE: WP2-AUTHN-01_ONLY
AUTOMATIC_AGENT_LAUNCH: false
MANUAL_AGENT_TRANSPORT: true
RULE_DECIDABLE_STAGE_PROGRESSION: AI_DECIDES
AUTHORITY_RECORDS: REQUIRED
AUTONOMOUS_EXECUTION_SCOPE: ACTIVE_REQUIRED
DERIVED_AUTHORITY_WITHIN_ACTIVE_SCOPE: AI_MAY_MATERIALIZE
SCOPE_EXTENSION: HUMAN_DECISION_REQUIRED
HUMAN_DECISION_ESCALATION: CONDITION_BASED
FINAL_PRODUCT_MERGE_APPROVAL: HUMAN_REQUIRED
JIT_HANDOFF: COPY_PASTE_READY
```

v1 pilotはWP2-AUTHN-01の完了（Issue #167 closed/completed）をもって`AUTONOMOUS_EXECUTION_SCOPE: INACTIVE`へ終了しており、historical evidenceとして`docs/retrospectives/wp2-human-decision-handoff-pilot.md`とIssue #191をそのまま保持する。v1文書とIssue #191は書き換えず、v2の正本として再利用しない。

### v2 pilot（historical completed / Transition Bundle）

Post-AUTHN-01評価`MODIFY`（Issue #191 comment `5293594071`）を受け、Issue #197でTransition Bundleを中心概念とするv2 pilotを導入した。正準schemaと詳細は次を唯一の正本とし、本書では複製しない。

- `docs/retrospectives/wp2-human-decision-handoff-pilot-v2.md`

v2は、v1が確立したrule-decidable stage progressionの原則を維持しつつ、stage transitionのmaterialization契約を、`STAGE_RESULT_EVIDENCE` / `PARENT_CURRENT_AUTHORITY` / `WP_CURRENT_AUTHORITY` / `NEXT_AGENT_HANDOFF`の4要素からなるTransition Bundleへ置き換える。GitHub writeはtransactionalでないため、`NEXT_AGENT_HANDOFF`は必ず他の3要素が同一exact identityで揃った後に生成する**bundle finalization record**とする。前3要素が揃っていない、またはidentity/stageが一致しない場合、`NEXT_AGENT_HANDOFF`を生成してはならない。

v2はWP2-AUD-01（Issue #166）限定で実行済みであり、Issue #166の`closed / completed`をもってscopeを終了した。Parent #3 Current Authority comment `5300368789`およびWP-2 #34 Current Authority comment `5300369662`に基づく現在状態は、`AUTONOMOUS_EXECUTION_SCOPE_V2: INACTIVE`である。これは完了済みhistorical process evidenceであり、WP2-AUTHZ-01 / #168を含む次leafへ自動carryしない。

```yaml
AUTONOMOUS_EXECUTION_SCOPE_V2:
  STATE: INACTIVE
  COMPLETED_SCOPE: WP2-AUD-01_ONLY
  COMPLETED_ISSUE: 166
  TERMINATION_REASON: TARGET_ISSUE_CLOSED_COMPLETED
  V2_PILOT_CARRY_TO_NEXT_LEAF: PROHIBITED
  NEXT_LEAF_ACTIVATION: NOT_GRANTED
```

```yaml
OUTSIDE_PILOT_SCOPE:
  HUMAN_APPROVAL: required
  JIT_HANDOFF: GENERATE_ONLY
```

WP2-AUTHN-01のv1 pilotとWP2-AUD-01のv2 pilotは、それぞれの対象leafで完了したhistorical evidenceである。WP2-AUTHZ-01 / #168以降の次leafでは`OUTSIDE_PILOT_SCOPE`が既定であり、v1・v2いずれのpilot authorityも自動適用されない。同じ仕組みを次leafで使う場合は、新しいhuman activationとscope拡張の決定が必要である。

`AUTONOMOUS_EXECUTION_SCOPE_V2`の初回ACTIVE化は、process pilotの独立レビューと人間の最終承認後に、Parent #3 / WP-2 #34 Current Authorityで対象leafを限定して行う。Coordinator AIが自己承認してACTIVEにしてはならない。

ACTIVEな対象leafの内部では、Independent Issue Ready Reviewが確定したverdict（PASS）、Blocker / Major 0、required CI PASS等から一意に導けるCurrent Authority / implementation authorizationを、Coordinator AIがGate / Current Authorityとして形式化(materialize)してよい。加えて、対応するTransition Bundleの4要素を、上記の順序契約に従ってmaterializeする。Coordinator AI自身がIssue Readyをsemantic評価することは含まない。正式なauthority record自体は省略しない。rule-decidableかsemantic decisionかの分類自体に合理的な迷いがある場合は`HUMAN_DECISION_REQUIRED`とする。

重要なagent launch前のStage Entry Check（§5.1）には、v2 pilotがACTIVEな対象leafでBundle completeness gate（`STAGE_RESULT_RECORDED` / `PARENT_AUTHORITY_SYNCED` / `WP_AUTHORITY_SYNCED` / `HANDOFF_RECORDED_LAST` / `BUNDLE_STATUS_COMPLETE` / `BUNDLE_TARGET_IDENTITY_EXACT`）を含める。詳細はv2文書を参照する。

人間がAgent間のpromptや結果を搬送する場合でも、原則として意味的な追記・要約・書き換えを必要としない完成handoffをAgent側が生成する。受領Agentは、handoffのpromptをauthorityとして信用しない。着手前にGitHub一次証拠（Parent #3 Current Authority、WP-2 #34 Current Authority、target Issue、exact identity、write/operation authorization）を再確認し、promptと一次証拠が矛盾する場合はSTOPする。v2 pilotがACTIVEな対象leafでは、加えてTransition Bundleの完全性（Bundle completeness gate）を再確認し、部分的・不整合な場合は既定でmechanical STOP / repairとする（v2文書参照）。

### Agent Result Contract

Agentの結果報告は、実装の成否、Agent自身のlocal/harness verification、repository-standard required CI、Coordinatorへの要求状態を単一の`RESULT`へ混在させず、次の4軸で記録する。

```yaml
AGENT_RESULT_CONTRACT:
  IMPLEMENTATION_RESULT:
    PASS | FAIL | NOT_APPLICABLE

  LOCAL_VERIFICATION_RESULT:
    PASS | FAIL | ENVIRONMENT_BLOCKED | NOT_RUN

  REQUIRED_CI_RESULT:
    PASS | FAIL | PENDING | NOT_REQUIRED

  HANDOFF_STATE:
    COMPLETE | AWAIT_CI | FIX_REQUIRED | HUMAN_DECISION_REQUIRED | STOP
```

`IMPLEMENTATION_RESULT`は実装・修正そのものが成立したか、`LOCAL_VERIFICATION_RESULT`はAgent自身のlocal/harness環境でverificationを完了できたか、`REQUIRED_CI_RESULT`はrepository-standard required CIの状態、`HANDOFF_STATE`はCoordinatorへ何を要求するかを表す。Coordinatorは、取得可能なGitHub上のrequired CIとexact Headを確認し、local environment failureとproduct defectを分離して評価する。

各`HANDOFF_STATE`の意味は次の通りである。

- `COMPLETE`: 必要な作業・検証が成立し、次stageへ進行可能。
- `AWAIT_CI`: implementationは成立しているが、repository-standard required CIの確定待ち。
- `FIX_REQUIRED`: existing authorityから修正方法を一意に導けるrule-decidable defectがある。
- `HUMAN_DECISION_REQUIRED`: 複数の合理的なsemantic choiceがあり、§5.6に従って人間へ意思決定を要求する。
- `STOP`: identity / authority / scope / control precondition mismatch等により、current targetのまま作業を継続してはいけない。

特に、次の組み合わせは有効であり、単独で`FIX_REQUIRED`を意味しない。

```yaml
IMPLEMENTATION_RESULT: PASS
LOCAL_VERIFICATION_RESULT: ENVIRONMENT_BLOCKED
REQUIRED_CI_RESULT: PENDING
HANDOFF_STATE: AWAIT_CI
```

rule-decidable defectは`FIX_REQUIRED`、genuine semantic decisionは`HUMAN_DECISION_REQUIRED`として§5.6 Human Decision Escalationへ接続し、identity / authority / scope / control mismatchは`STOP`へ分類する。`HUMAN_DECISION_REQUIRED`を`FIX_REQUIRED`または`STOP`へ縮退させてはならない。local verificationが環境要因で完了できないことだけを理由に、実装不成立と扱ってはならない。

## 5.6 Human Decision Escalation

次の場合はAIが独断で進めず、人間へ意思決定を要求する。

- 要求に複数の合理的解釈があり、承認済み正本から一意に決まらない。
- Accepted ADRにない新しい重要設計判断が必要である。
- Issueの承認済みscopeを変更または拡張する必要がある。
- 複数案に実質的なメリット／デメリットがあり、客観的証拠だけでは決着しない。
- 戻せない、または重大な影響を持つ操作・判断が必要である。
- 独立したAI間の結論が割れ、追加検証でも解消しない。
- 既存の承認済み製品方針、仕様、ADR、process decisionを変更する必要がある。

逆に、Gate PASS、Blocker / Major 0、required CI PASS、exact SHA一致、dependency completion等、既定ルールと証拠から一意に判定できる事項だけを理由に人間へ「進めてよいか」と質問してはならない。

人間判断が必要な場合は、判断事項、選択肢、メリット、デメリット、影響、AI推奨、推奨理由を整理してから要求する。詳細形式はpilot contractを参照する。

## 5.7 Agent Handoff Output

WP2-AUTHN-01 pilotで次のAgentが必要な場合、前stageはcopy-paste-ready handoffを出力する。

Agent Handoff schemaの正本は`docs/retrospectives/wp2-human-decision-handoff-pilot.md`（Agent Handoff Contract節）のみとする。本書ではschema全文を複製しない。

最低限、次を必須原則とする。

- `TARGET`は次のAgentが会話履歴なしでもexact targetを特定できること。
- `EVIDENCE`はGitHub一次証拠または実行結果を優先すること。
- ローカルAgentを依頼する場合は、モデル名＋バージョンを明示した`MODEL`と、実行基盤名を明示した`HARNESS`を指定し、人間が編集せずそのまま投入できる完成promptを`PROMPT`へ含めること。
- `PROMPT`には最低限、`MODE`、`WRITE_SCOPE`、`PROHIBITED_OPERATIONS`、`EXPECTED_BASE`、`EXPECTED_HEAD`（またはそのstageに必要なexact identity）を含めること。
- `COPY_PASTE_READY`は自己申告値にしない。`LOCAL_AGENT_REQUEST.REQUIRED: true`の場合、`MODEL` / `HARNESS` / `PROMPT`がすべて非空であるときだけ`true`とし、1つでも欠ければ`false`とする。
- 実際に投入した完成prompt全文は、対象Issueのhandoff commentへ記録し追跡可能にすること。
- 次AgentがGitHubから取得できる情報を、人間に手作業で再構成させないこと。

## 5.8 Final Approval Packet

正式反映前の最終human approvalでは、大量の会話履歴やAgent報告を人間に再調査させない。

最低限、次を一つのFinal Approval Packetとして提示する。

- target Issue / PR / reviewed Head / merge target;
- Acceptance Criteria達成状況;
- required tests / CI結果;
- Blocker / Major / 未解決finding;
- reviewed Headとmerge対象の同一性;
- known risks / unverified items;
- rollback方法;
- 今この時点で人間判断が必要な事項;
- AIのMERGE / DO_NOT_MERGE推奨と理由。

必要な詳細はGitHub evidence pointerとして追跡可能にし、人間がFinal Approval Packet以外の長い履歴を読み返さなくても判断できる状態を目指す。

## 6. 停止条件

次の場合は推測して進めず、未決事項として報告する。

- 原始要件または承認済み仕様に矛盾がある。
- 重要な設計判断にADRが必要だが存在しない。
- Issueの範囲を超える変更が必要である。
- 残高整合性、データ消失、監査証跡に影響する不明点がある。
- 必須検証を実行できない。
- 前提フェーズゲートが`FAIL`または`NOT EVALUATED`である。
- 対象作業がParent Issue #3または対象Issueから追跡できない。
- Parent Issue #3の現在地と依頼された作業フェーズが一致しない。
- Parent Issue #3で次工程の開始が許可されていない。
- 未承認の推奨案を実装へ反映する必要がある。
- プロジェクト目的より機能追加自体が優先されている。
- `main`への直接writeが必要である。
- `HUMAN_DECISION_REQUIRED`に該当する未決事項が残っている。
- pilot対象leafで`AUTONOMOUS_EXECUTION_SCOPE`がACTIVEでない、または対象leaf/scopeが一致しない。

ただし、ゲート再評価、Blocking Decision確定、統制文書更新など、ゲートを通過させるための作業はこの限りではない。前提ゲートが未通過の場合は、その先の工程に着手せず停止する。

## 7. 要件・仕様・ADR承認前の初期フェーズ禁止事項

要件レビュー、仕様化、必要なADRの承認が完了するまでは、次を実施しない。

- アプリケーション雛形の作成
- DBスキーマまたはmigrationの作成
- APIの実装
- Docker構成の確定
- 本実装用Issueの確定

## 8. セキュリティとデータ

- 実在人物の個人情報を使用しない。
- 実口座、実送金、実金融機関への接続を行わない。
- secret、credential、tokenをリポジトリへ保存しない。
