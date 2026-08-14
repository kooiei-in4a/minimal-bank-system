# WP-2 Transition Bundle v2 Pilot

## Status

```yaml
DOCUMENT:
  STATUS: PROPOSED_FOR_PILOT
  PROCESS_ISSUE: 197
  BASELINE_MAIN: ed64f0481337363da77b5df62ea86cb7fec7ff11

PILOT_MODEL:
  VERSION: V2
  SCOPE: EXPLICIT_SINGLE_LEAF_ONLY
  NEXT_INTENDED_TARGET:
    LEAF_ID: WP2-AUD-01
    ISSUE: 166
  STATE: NOT_ACTIVE
```

本文書の有効化は、このファイルを再編集することでは行わない。この process PR が独立レビューを通過し、人間の最終承認で`main`へ反映された後も、`NEXT_INTENDED_TARGET`（WP2-AUD-01 / #166）は**意図された次候補**であるに過ぎず、この process PR のmergeだけでは`AUTONOMOUS_EXECUTION_SCOPE_V2`は`ACTIVE`にならない。ACTIVE化は別の明示的なhuman activationを必要とする（§8参照）。

---

## 1. v1 pilotとの関係

- WP2-AUTHN-01 pilotの正本である `docs/retrospectives/wp2-human-decision-handoff-pilot.md`（以下v1文書）は、完了済みhistorical evidenceとして**変更しない**。
- Issue #191はWP2-AUTHN-01の完了済みhistorical pilot Issueであり、v2の正本として再利用しない。v2の正本はIssue #197および本文書とする。
- v1文書が確立した次の中核原則は、v2でも維持する。

  > Human semantic decisions are requested only when existing authority/evidence cannot determine the result uniquely.
  > Rule-decidable stage progression does not require repeated human semantic approval inside an explicitly activated bounded pilot.

- v2が変更するのは、**stage transitionの materialization契約（Transition Bundle）のみ**である。Human Decision Escalation条件（v1 §3）、Agent Handoff Contractの必須属性（v1 §5）、Human Decision Request Contract（v1 §6）、Final Approval Packetの基本schema（v1 §7）は、v2でも同一の原則を引き継ぐ。v2固有の追加事項だけを本文書に定義する。

---

## 2. Pilot scope（v2）

### Included

- Transition Bundleという単一の中心概念によるstage transition materialization契約。
- Bundle未完成／不整合時のmechanical STOP / repairルール。
- Next-agent Stage Entry Checkへのbundle完全性チェックの追加。
- PR body current-state報告の義務化。
- Final Approval Packet前のexact-head coherence確認。
- v2向け`AUTONOMOUS_EXECUTION_SCOPE_V2`の正準schema定義。

### Out of scope

- WP2-AUD-01の製品実装、Acceptance Criteria変更。
- candidate policy、Light Review、Heavy Review、Critical Mutation、targeted fix/re-review等、既存WP-2 review policyの変更（§7で明示的に維持）。
- 本process PRのmergeによるWP2-AUD-01の自動activation。
- Agentの自動起動、mergeの自動化、最終human approvalの撤廃。
- v1文書またはIssue #191の遡及的な書き換え。

今回のpilotも、v1と同様に**人間介在の方法だけ**を主な変更対象とする。

---

## 3. Core principle

v1 §2で確立した原則を維持する。

```text
Human semantic decisions are requested only when existing authority/evidence cannot determine the result uniquely.
Rule-decidable stage progression does not require repeated human semantic approval inside an explicitly activated bounded pilot.
```

pilot適用外（`AUTONOMOUS_EXECUTION_SCOPE_V2: INACTIVE`、または対象leafが一致しない場合）は、既定の`HUMAN_APPROVAL: required`を維持する。これはv1 §2.5および`AGENTS.md` §5.5の`OUTSIDE_PILOT_SCOPE`と同一の扱いとする。

---

## 4. Transition Bundle — canonical contract

WP2-AUTHN-01 pilotで唯一発生した主要なprocess defectは次の通りである。

> Narrow Fix後のPR Headは進んだが、Parent/WP-2 Current Authorityが旧Headに残り、Heavy Review H1がPreflight STOPした。

v2では、stage transitionを次の4成果物の**bundle**として定義することでこれを再発防止する。

```yaml
TRANSITION_BUNDLE:
  STAGE_RESULT_EVIDENCE:
  PARENT_CURRENT_AUTHORITY:
  WP_CURRENT_AUTHORITY:
  NEXT_AGENT_HANDOFF:
```

GitHub writeはtransactionalではない。したがって、これら4成果物が「同時に」書き込まれたかのように装ってはならない。代わりに、次の**順序契約**を正本とする。

```text
1. STAGE_RESULT_EVIDENCE を materialize する（review結果・CI run・comment等）
2. Parent Current Authority を materialize し、STAGE_RESULT_EVIDENCE と同じ exact target identity を指す
3. WP Current Authority を materialize し、同じ exact target identity を指す
4. NEXT_AGENT_HANDOFF を最後に生成する
```

`NEXT_AGENT_HANDOFF`は、単なる次工程への連絡ではなく、**bundle finalization record**として扱う。`NEXT_AGENT_HANDOFF`には最低限、次を含める。

```yaml
TRANSITION_BUNDLE:
  STATUS: COMPLETE
  STAGE_RESULT_EVIDENCE: <comment/review/run ref>
  PARENT_CURRENT_AUTHORITY: <comment id>
  WP_CURRENT_AUTHORITY: <comment id>
  TARGET_IDENTITY:
    ISSUE:
    PR:
    BASE:
    HEAD:
  COMPLETED_STAGE:
  NEXT_AUTHORIZED_STAGE:
```

### Bundle completeness rule

- `STAGE_RESULT_EVIDENCE` / `PARENT_CURRENT_AUTHORITY` / `WP_CURRENT_AUTHORITY`の3要素が揃っておらず、かつ同一のexact target identity（同じIssue、同じPR/branch、同じHead/base、同じcompleted stage、同じnext authorized stage）で一致しない限り、Coordinator AIは`NEXT_AGENT_HANDOFF`を生成してはならない。
- したがって、**`NEXT_AGENT_HANDOFF`が実在すること自体が、直前3要素の同期完了の証拠**として機能する。`STATUS: COMPLETE`は、生成時点でCoordinator AIが上記3要素のexact identity一致を確認した結果としてのみ記載する。
- 毎stageごとに新しい専用の"bundle manifest"commentを追加してはならない。bundle finalizationの記録は`NEXT_AGENT_HANDOFF`そのものに統合し、process weightを追加で増やさない。

---

## 5. Next-agent Stage Entry Check（v2）

活性化されたv2 pilotで、重要なagent launch前に最低限、次を確認する。

```text
STAGE_RESULT_RECORDED
PARENT_AUTHORITY_SYNCED
WP_AUTHORITY_SYNCED
HANDOFF_RECORDED_LAST
BUNDLE_STATUS_COMPLETE
BUNDLE_TARGET_IDENTITY_EXACT
NO_UNRESOLVED_BLOCKER_MAJOR
NEXT_STAGE_AUTHORIZED
```

`NEXT_AGENT_HANDOFF`の内容だけを信用してはならない。受領Agentは、着手前にGitHub一次証拠（Parent #3 Current Authority、WP-2 #34 Current Authority、target Issue、exact identity）を再確認し、handoffの記載と一次証拠が一致するかを検証する。この点はv1 §5・`AGENTS.md` §5.5の「promptはauthorityではない」原則をそのまま踏襲する。

### Partial / mismatchの既定結果

上記チェックが部分的にしか満たされない、またはbundle memberのidentityが不一致の場合、既定は次とする。

```yaml
RESULT: STOP
CLASSIFICATION: MECHANICAL_CONTROL_SYNC_REPAIR
HUMAN_DECISION_REQUIRED: false
```

Coordinator AIは、Current Authorityを正しいexact identityへ同期し直すmechanical repairを行ってよい。ただし、その修復が製品仕様・ADR・Issue scope・processのsemantics（Human Decision Escalation条件、review policy等）を変更する場合に限り、`HUMAN_DECISION_REQUIRED: true`へ切り替える。

---

## 6. Single Current Authority preservation

v2でも、`AGENTS.md` §2.3が定めるSingle Current Authority原則を維持する。Transition Bundleは、第二のCurrent Authorityとして機能してはならない。

- **Parent Current Authority** = Parent control level（Issue #3）における現在のoperational authority。
- **WP Current Authority** = WP control level（Issue #34）における現在のoperational authority。
- **Transition Bundle** = stage transitionの**coherence契約**（4要素が同じexact identityを指しているかを確認するための構造）であり、それ自体は独立したauthorityの発生源ではない。`PARENT_CURRENT_AUTHORITY`と`WP_CURRENT_AUTHORITY`は、既存のSingle Current Authorityへのpointerであり、Transition Bundleが別の並行authorityを新設するものではない。

---

## 7. PR body boundary

PR bodyはhistorical implementation/evidence descriptionを含み得るが、**Current Authorityではない**。PR bodyの記述だけを根拠に、stage/Headの現在状態を判断してはならない。

Final Approval Packet生成時に、Coordinator AIは機械的に次を報告する。

```yaml
PR_BODY_CURRENT_STATE:
  STATUS: CURRENT | HISTORICAL_STALE
```

PR bodyが`HISTORICAL_STALE`であっても、Current Authority（Parent / WP）・PR API上の実際のHead・review記録・required CIがexact identityで一致していれば、原則として**warning**として扱い、それ単独でSTOP条件にはしない。PR bodyのstale状態が、Current AuthorityやHeadそのものの不一致を隠す手段になってはならない。

---

## 8. Final Approval Packet coherence

v1 §7の`FINAL_APPROVAL_PACKET`schemaはそのまま正本とし、本文書では複製しない。v2では、Final Approval Packet提示前に、追加で次のexact-head coherenceを機械的に確認する。

```text
PR_CURRENT_HEAD == REVIEWED_HEAD
PARENT_CURRENT_AUTHORITY_HEAD == REVIEWED_HEAD
WP_CURRENT_AUTHORITY_HEAD == REVIEWED_HEAD
REQUIRED_CI_HEAD == REVIEWED_HEAD
```

いずれか一つでも一致しない場合、Final Approval Packetを提示せず、§5のmechanical STOP / repairへ戻る。

---

## 9. Activation

v2 autonomous scopeの正準schemaを次の通り定義する。`AGENTS.md`はこのschemaを複製せず、本節を参照する。

```yaml
AUTONOMOUS_EXECUTION_SCOPE_V2:
  STATE: INACTIVE | ACTIVE
  PILOT_VERSION: V2
  TARGET_LEAF:
  TARGET_ISSUE:
  ACTIVATED_BY: HUMAN
  ACTIVATION_COMMENT:
  TERMINATES_ON: TARGET_ISSUE_CLOSED_COMPLETED
  TRANSITION_BUNDLE_REQUIRED: true
```

### 現在の状態

```yaml
AUTONOMOUS_EXECUTION_SCOPE_V2:
  STATE: INACTIVE
  PILOT_VERSION: V2
  TARGET_LEAF: WP2-AUD-01
  TARGET_ISSUE: 166
  ACTIVATED_BY: null
  ACTIVATION_COMMENT: null
  TERMINATES_ON: TARGET_ISSUE_CLOSED_COMPLETED
  TRANSITION_BUNDLE_REQUIRED: true
```

### 必須ルール

- 本process PRのmergeだけでは`STATE`を`ACTIVE`にしない。初回ACTIVE化は、独立した process/control reviewと人間の最終承認を経て、Parent #3 / WP-2 #34 Current Authorityで対象leafを限定した別個のhuman activationによってのみ行う。Coordinator AIが自己承認してACTIVEにしてはならない。
- `AUTONOMOUS_EXECUTION_SCOPE_V2`はleafをまたいでcarryしない。ACTIVE化は`TARGET_LEAF` / `TARGET_ISSUE`に明示された単一leafに限定される。次leafで同じ仕組みを使う場合は、新しいhuman activationが必要である。
- `TARGET_ISSUE`が`closed`かつ`state_reason: completed`になった時点で`STATE`は自動的に`INACTIVE`へ終了する。
- `STATE` / `TARGET_LEAF` / `TARGET_ISSUE` / `ACTIVATION_COMMENT` / `TERMINATES_ON`の変更、およびscopeの拡張は、いずれもHuman Decisionを要する。
- `TRANSITION_BUNDLE_REQUIRED: true`は固定値とする。v2 pilotが適用されるすべてのstage transitionで、§4のTransition Bundle契約を省略できない。

---

## 10. Preserve existing review policy

次のpolicyは、v2導入によって変更しない。

- Light Review: 1件のsemantic review。
- Heavy Review: 1件を既定とし、2件目はrisk-basedで判断する（`AGENTS.md` §5.3）。
- targeted fix / targeted re-review（full rerunを既定にしない）。
- Critical Mutation policy（high-risk leafのみ、leafあたり最大3件、semantic failure signature必須。`AGENTS.md` §5.4）。
- candidate policy（candidate数、選定手続き）。
- 最終human merge approval（`FINAL_PRODUCT_MERGE_APPROVAL: HUMAN_REQUIRED`）。
- direct-main-write prohibition。

---

## 11. Stop / rollback

次の場合、v2 pilotによる自動的なstage progressionを停止する。

- Transition Bundleの4要素が揃っていない、または同一のexact target identityで一致しない。
- `AUTONOMOUS_EXECUTION_SCOPE_V2`がACTIVEでない、対象leafが一致しない、またはscope外のauthorityをmaterializeしようとしている。
- mechanical repairのつもりで、実際には製品仕様・ADR・Issue scope・process semanticsを変更しようとしている。
- Final Approval Packet前のexact-head coherence確認（§8）が一致しない。
- PR body staleを理由に、Current AuthorityやHeadの不一致を実質的に隠蔽しようとしている。
- v1文書§11の既存stop条件（Human Decision Escalationの回避、handoff不足による人間の意味的再構成の必要性等）に該当する。

重大なprocess defectが確認された場合は、対象leafの製品実装を止め、本pilotを修正または撤回してから再開する。
