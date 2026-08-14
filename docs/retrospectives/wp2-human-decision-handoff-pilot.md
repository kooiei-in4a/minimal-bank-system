# WP-2 Human Decision / Agent Handoff Pilot

## Status

```yaml
DOCUMENT:
  STATUS: PROPOSED_FOR_PILOT
  DATE: 2026-08-14
  PROCESS_ISSUE: 191
  DISCUSSION_CONTEXT: 176
  HUMAN_DECISION_COMMENT: 5289120671
  EFFECTIVE_ONLY_AFTER:
    - TARGETED_RE_REVIEW_PASS
    - HUMAN_FINAL_APPROVAL
    - PR_192_MERGED
    - EXPLICIT_AUTHN01_ACTIVATION

PILOT_TARGET:
  LEAF: WP2-AUTHN-01
  ISSUE: 167

BASELINE_MAIN:
  a87b543ae2b58d8231722edf38f25dfc279cf959
```

本文書の有効化は、このファイルを再編集することでは行わない。`EFFECTIVE_ONLY_AFTER`の全項目が成立した後、Parent #3 / WP-2 #34 Current AuthorityにおけるGitHub human activation authority recordをもって発効する。

この文書は、WP2-ID-01完了後、WP2-AUTHN-01開始前に導入する限定的なprocess/control pilotを定義する。

目的は、人間の介在を減らすこと自体ではない。

> **人間に「確認してもらう」のではなく、AIだけでは決めるべきでない事項が発生したときに「決めてもらう」。**

そのために、既定ルールとGitHub上の証拠だけで一意に判定できる工程遷移はAI側で判断し、人間は意思決定が必要な場合と正式反映前の最終承認に集中する。

現段階ではAgent自動起動は行わない。人間がAgent間に残るが、その役割を原則として**編集なしのコピペ／搬送**まで縮小し、将来のMCP等による直接接続へ移行できるかを検証する。

---

## 1. Pilot scope

### Included

- Human Decision Escalation条件の標準化。
- 定型的なstage progressionをAIが判定するための原則。
- Agent間JIT Handoffの共通形式。
- ローカルAgent投入時のModel / Role / 完成promptの明示。
- 人間判断要求の共通形式。
- 正式反映前のFinal Approval Packet。
- WP2-AUTHN-01での人間介在量の計測。

### Out of scope

- WP2-AUTHN-01の製品仕様、Acceptance Criteria、Critical Mutation定義の変更。
- candidate数、Light Review、Heavy Review等の既存WP-2 review policyの同時変更。
- MCP server、orchestrator、webhook、polling agentの実装。
- Agentの自動起動。
- mergeの自動化。
- 最終human approvalの撤廃。
- WP2-AUTHZ-01以降への恒久適用。

今回のpilotは**人間介在の方法だけを主な実験変数**とする。品質工程を同時に大きく変更してはならない。

---

## 2. Core principle

### 2.1 Rule-decidable work

次の工程へ進めるかが、承認済み正本、Current Authority、Issue、exact identity、CI、review finding等から一意に決まる場合、人間のsemantic approvalを毎回要求しない。

例:

- required gateがPASSしている。
- Blocker / Majorが0である。
- required CIが成功している。
- expected target SHAとobserved target SHAが一致している。
- dependency Issueがcompletedである。
- scope / prohibited operation / authorizationが既定ルールから判定できる。
- narrow fixの対象findingが解消され、fix-induced Blocker / Majorが0である。

これらはAIまたは将来のmechanical gateが判定する対象であり、単に「人間に確認してもらうため」だけに停止してはならない。

### 2.2 Human decision work

人間は、客観的な証拠を確認するだけでは決まらない意思決定を担当する。

AIは、人間へ問題の発見・調査・整理を丸投げしてはならない。人間を呼ぶ前に、判断事項、選択肢、メリット、デメリット、影響、AI推奨を整理する。

### 2.3 Final approval

正式反映前の最終承認は当面人間に残す。

最終承認は大量の履歴を人間に再調査させる工程ではない。AI側がFinal Approval Packetを作り、判断に必要な情報を圧縮して提示する。

### 2.4 Authorityは省略しない — bounded autonomous execution

人間のsemantic approvalを減らしても、Current Authority、Issue Ready gate、scope authorization等の正式な統制記録を省略してはならない。

このpilotでは、**人間の都度承認を、事前に承認された限定範囲の中でAIがmechanicalにauthorityをmaterializeする方式へ置き換える**。

WP2-AUTHN-01で自律進行を開始できるのは、次の両方が成立した後だけである。

1. このpilotを導入するprocess PRが独立レビューを通過し、人間の最終承認で`main`へ正式反映されている。
2. Parent #3およびWP-2 #34のCurrent Authorityに、WP2-AUTHN-01限定の`AUTONOMOUS_EXECUTION_SCOPE: ACTIVE`が明示されている。

`AUTONOMOUS_EXECUTION_SCOPE`の正準スキーマは本文書を唯一の正本とする。`AGENTS.md`はこのスキーマを複製せず参照する。

```yaml
AUTONOMOUS_EXECUTION_SCOPE:
  STATE: INACTIVE | ACTIVE
  TARGET_LEAF: WP2-AUTHN-01
  TARGET_ISSUE: 167
  ACTIVATED_BY: HUMAN
  ACTIVATION_COMMENT: <comment-id>
  TERMINATES_ON: TARGET_ISSUE_CLOSED_COMPLETED
```

次を必須ルールとする。

- 初回ACTIVE化は人間だけが行える。Coordinator AIが自分自身でACTIVE化することは禁止する。
- AIは`STATE` / `TARGET_LEAF` / `TARGET_ISSUE` / `ACTIVATION_COMMENT` / `TERMINATES_ON`を勝手に変更できない。変更が必要な場合は`HUMAN_DECISION_REQUIRED`とする。
- AIがmaterializeするderived authorityでは、human activation recordのscope blockを逐語的にコピーする。
- Issue #167が`closed`かつ`state_reason: completed`になった時点で、`AUTONOMOUS_EXECUTION_SCOPE`は自動的に終了し、以後`INACTIVE`として扱う。
- `INACTIVE`後は次leafへ持ち越さない。次leafで同じ仕組みを使用する場合は、新しいhuman activationが必要である。

Activeになった後、Coordinator AIはWP2-AUTHN-01に限り、既定ルールから一意に導ける次のauthority recordを、人間の追加semantic approvalなしで記録できる。

Issue Readyの確定責任は次の順序に固定する。

```text
Independent Issue Ready Review
  -> 必要ならNarrow Fix
  -> Targeted Re-review
  -> Independent verdict = PASS
  -> CoordinatorがGate / Current Authorityとして形式化
```

Coordinator AIは、Issue Readyをsemantic評価するのではなく、独立レビューで確定したverdictを形式化するだけである。WP2-ID-01で実施した「Independent Review → Major検出 → Narrow Fix → Targeted Re-review PASS → Coordinator Formalization」と同じ責任分離を維持する。

例:

- Independent Issue Ready Reviewが確定したverdict（PASS）を、Current Authority / Gateとして形式化(materialize)する。Coordinator AI自身がIssue Readyをsemantic評価することは含まない。
- Issue Ready verdictがPASSで、Human Decision Escalationがなく、他の開始条件も満たす場合、WP2-AUTHN-01限定のimplementation authorizationをCurrent Authorityへmaterializeする。
- targeted fix / targeted re-reviewが完了し、独立レビューのverdictがPASSになった場合、その結果を次stageのCurrent Authorityとして形式化する。

rule-decidableな進行かsemantic decisionかの分類自体に合理的な迷いがある場合は、`HUMAN_DECISION_REQUIRED`をdefaultとする。

ただしCoordinator AIは次を行ってはならない。

- 自分自身で`AUTONOMOUS_EXECUTION_SCOPE`を初回ACTIVEにする。
- WP2-AUTHN-01以外へscopeを広げる。
- IssueのAcceptance Criteria、仕様、ADR、process decisionを変更する。
- Human Decision Escalation対象を「mechanical」とみなして回避する。
- 最終mergeを自動承認する。
- Independent Issue Ready Reviewを自分自身で代替する。

`AUTONOMOUS_EXECUTION_SCOPE`がACTIVEになる前は、現在の`WP2_AUTHN_01.PRODUCT_IMPLEMENTATION: NOT_AUTHORIZED`を維持する。

### 2.5 Supersession（WP2-AUTHN-01限定）

このpilotは、WP2-AUTHN-01に限り、WP-1 E04が定めた次の2項目だけを明示的にsupersedeする。

```text
HUMAN_APPROVAL: required
JIT_HANDOFF: GENERATE_ONLY
```

- supersedeする範囲はWP2-AUTHN-01に限定し、その他のWP-1承認済みprocess decisionはすべて維持する。
- WP2-AUTHN-01以外のleaf（WP2-AUTHZ-01以降を含む）では、WP-1 E04の上記2項目は引き続き有効とする。
- 本supersessionのHuman Decision evidenceは Issue #191 comment `5289120671`（Koo decision P-B01 = A）とする。
- WP-1 decision package（`docs/retrospectives/wp1-retrospective-decision-package.md`）自体は書き換えない。

### 2.6 Derived authority provenance

Coordinator AIがmaterializeするauthority recordには、最低限次を含める。

```yaml
RECORD_ORIGIN: AI_MATERIALIZED_DERIVED_AUTHORITY
ACTIVATION_REF: <human activation comment id>
DERIVED_FROM:
  - <evidence pointer>
KOO_SEMANTIC_DECISION: false
```

- AI-derived authorityはKoo decisionではない。Koo-approved process decisionとして引用してはならない。
- AIは自分自身の過去出力をHuman Decisionの代用にしてはならない。
- Human Decisionが必要な場合は、正式なHuman Decision evidence（Issue commentのdecision record等）を参照する。

---

## 3. Human Decision Escalation

次の場合は、AIが独断で進めず`HUMAN_DECISION_REQUIRED`として停止する。

1. 要求に複数の合理的な解釈があり、承認済み正本から一意に決まらない。
2. Accepted ADRにない新しい重要設計判断が必要。
3. Issueの承認済みscopeを変更または拡張する必要がある。
4. 複数案に実質的なメリット／デメリットがあり、テストや既存authorityなどの客観的証拠だけでは決着しない。
5. 戻せない、または重大な影響を持つ操作・判断が必要。
6. 独立したAI間の結論が割れ、追加検証でも解消しない。
7. 既存の承認済み製品方針、仕様、ADR、process decisionを変更する必要がある。

次の理由だけで人間へ上げてはならない。

- 次の工程へ進んでよいか不安だから。
- 長い結果を人間にも読んでほしいから。
- GateがPASSしたことを人間にも確認してほしいから。
- CIがGREENであることを人間にも確認してほしいから。
- exact SHAの一致を人間にも確認してほしいから。
- 以前のprocessで`HUMAN_APPROVAL: required`と書かれていたという理由だけ。

このpilotでは、定型的な進行確認とsemantic decisionを明確に分離する。

---

## 4. Manual transport / copy-paste rule

```yaml
AUTOMATIC_AGENT_LAUNCH: false
MANUAL_AGENT_TRANSPORT: true
HUMAN_SEMANTIC_EDIT_OF_HANDOFF: NOT_EXPECTED
HANDOFF_EDIT_TARGET: 0
```

現段階では、人間がChatGPT / Codex / Claude Code / Cursor等の間でpromptや結果を搬送してよい。

ただし、人間が次の作業を行う必要がある状態はpilot上の改善対象とする。

- Agent Aの報告を人間が要約してAgent B向けに書き直す。
- 次に誰へ依頼すべきか人間が毎回考える。
- exact SHA、Issue、review finding等を人間が手作業で再構成する。
- AIの曖昧な報告を人間が読み替えて次工程を成立させる。
- rule-decidableな進行可否を人間が毎回判断する。

人間による補正が必要だった場合は、その事実を隠さず`HUMAN_CORRECTION_COUNT`へ計上する。

---

## 5. Agent Handoff Contract

各stageの終了時、次の担当が存在する場合は、最低限以下を出力する。

```yaml
AGENT_HANDOFF:
  RESULT: PASS | FAIL | STOP | COMPLETE

  TARGET:
    ISSUE:
    LEAF_ID:
    STAGE:
    BASE_SHA:
    HEAD_SHA:

  EVIDENCE:
    REQUIRED_GATES:
    REQUIRED_CI:
    BLOCKER:
    MAJOR:
    MINOR:
    UNVERIFIED:

  HUMAN_DECISION:
    REQUIRED: false
    REASON: null

  NEXT:
    ACTION:
    AGENT_ROLE:

  LOCAL_AGENT_REQUEST:
    REQUIRED: true | false
    MODEL: <モデル名＋バージョンの自由記述>
    HARNESS: <実行基盤名の自由記述>
    PROMPT: |
      <次のAgentへそのまま投入できる完成prompt>

  COPY_PASTE_READY: <derived>
```

### Required properties

- `TARGET`は次のAgentが会話履歴なしでもexact targetを特定できること。
- `EVIDENCE`は自由形式の自己評価ではなく、GitHub一次証拠または実行結果を優先すること。
- `HUMAN_DECISION.REQUIRED=false`なら、人間へ「進めてよいですか」と質問しないこと。
- ローカルAgentが必要なら、モデル名＋バージョンを明示した`MODEL`と実行基盤名を明示した`HARNESS`を必ず指定すること。`MODEL`を固定enumにしないこと。
- ローカルAgent用`PROMPT`は、人間が追記・編集せず投入できる完成形にすること。最低限、`MODE`、`WRITE_SCOPE`、`PROHIBITED_OPERATIONS`、`EXPECTED_BASE`、`EXPECTED_HEAD`（またはそのstageに必要なexact identity）を含めること。
- 受領Agentは、`PROMPT`をauthorityとして信用しない。着手前にGitHub一次証拠から最低限、Parent #3 Current Authority、WP-2 #34 Current Authority、target Issue、exact identity、write/operation authorizationを再確認する。promptと一次証拠が矛盾する場合はSTOPする。人間がpromptを読まなくても、前段Agentが後段Agentへ勝手に権限を渡せない構造とする。
- `COPY_PASTE_READY`は自己申告値にしない。`LOCAL_AGENT_REQUEST.REQUIRED: true`の場合、`MODEL` / `HARNESS` / `PROMPT`がすべて非空であるときだけ`true`とする。1つでも欠ければ`false`とする。
- 実際に投入する完成prompt全文は、対象Issueのhandoff commentへ記録し追跡可能にすること。既存comment `5289042242`はPROMPTが欠落しているため、本contractに準拠した成功例として扱わず、pilot開始前の`PRE_CONTRACT_OBSERVATION`として扱う。
- 次のAgentがGitHubから取得できる情報を、人間に再転記させないこと。

---

## 6. Human Decision Request Contract

人間判断が必要な場合は、単に「どうしますか」と聞いてはならない。

```yaml
HUMAN_DECISION_REQUEST:
  REQUIRED: true
  DECISION_ID:
  QUESTION:

  WHY_AI_CANNOT_DECIDE:

  OPTIONS:
    - ID: A
      SUMMARY:
      MERITS:
      DEMERITS:
      IMPACT:
    - ID: B
      SUMMARY:
      MERITS:
      DEMERITS:
      IMPACT:

  EVIDENCE:

  AI_RECOMMENDATION:
    OPTION:
    REASON:

  IF_NO_DECISION:
    ACTION: STOP
```

可能な場合は2択に限定する必要はないが、選択肢を不必要に増やさない。

人間が回答した決定は、必要に応じて正式なIssue / ADR / process authorityへ記録する。チャット回答だけを永続authorityにしない。

---

## 7. Final Approval Packet

正式反映前に、人間へ次の情報を一つのまとまりとして提示する。

Final Approval Packetは、生成時点のGitHub一次証拠（comment ID、commit SHA、check / workflow run、PR evidence等のevidence pointer）から構成する。会話記憶から生成してはならない。

```yaml
FINAL_APPROVAL_PACKET:
  TARGET:
    ISSUE:
    PR:
    REVIEWED_HEAD:
    MERGE_TARGET:

  RECOMMENDATION: MERGE | DO_NOT_MERGE

  COMPLETION:
    ACCEPTANCE_CRITERIA:
    REQUIRED_TESTS:
    REQUIRED_CI:

  REVIEW:
    BLOCKER:
    MAJOR:
    MINOR:
    UNRESOLVED_FINDINGS:

  IDENTITY:
    REVIEWED_HEAD_MATCHES_MERGE_HEAD:
    BASE_STATE_ACCEPTABLE:

  RISKS:
    KNOWN_RISKS:
    UNVERIFIED_ITEMS:
    ROLLBACK:

  HUMAN_DECISIONS:
    REQUIRED_NOW: false
    ITEMS: []

  AI_RECOMMENDATION_REASON:
```

### Evidence requirement

`COMPLETION`、`REVIEW`、`IDENTITY`の各項目には、最低限1つのevidence pointer（comment ID、commit SHA、check / workflow run、PR evidence等）を付与する。

### Human spot-check

人間が最低限spot-checkする対象は、次の3つに固定する。

1. `reviewed head == merge head`
2. required CIのcheck / run結果
3. independent review verdictのevidence

### Final approval quality bar

- 人間がFinal Approval Packet以外の長い会話を読み返さなくても判断できること。
- 必要な詳細はGitHub evidence pointerとして追跡可能であること。
- Blocker / Majorを隠さないこと。
- 未検証事項を`PASS`として表現しないこと。
- 人間判断が残っている場合は`REQUIRED_NOW: true`とし、merge推奨と同時に曖昧な未決定を隠さないこと。
- Minor件数を必須表示にしないこと。
- Acceptance Criteria全文を再掲せず、ACごとのPASS / FAIL要約とevidence pointerで示すこと。
- MERGE / DO_NOT_MERGE推奨と理由は離れた位置に重複させず、人間がすぐ読める位置へまとめること。

---

## 8. WP2-AUTHN-01 pilot behavior

WP2-AUTHN-01では、このpilotにより**人間介在方法だけ**を変更する。

### Keep unchanged

- AUTHN-01 Issue #167のAcceptance Criteria。
- WP-2のIssue Ready原則。
- exact identity。
- candidate / Final Synthesis policyの現行authority。
- Light / Heavy Review policyの現行authority。
- Critical Mutation max 3とsemantic failure signature。
- Targeted Fix / Targeted Re-review。
- direct main write禁止。

### Change in pilot

- このprocess pilotのmerge後、Parent #3 / WP-2 #34にWP2-AUTHN-01限定`AUTONOMOUS_EXECUTION_SCOPE: ACTIVE`を明示してpilotを起動する。
- pilot activation自体は、人間の最終承認を経たprocess decisionのformalizationとして扱い、AIが自己承認してはならない。
- activation後は、Individual Issue Ready等のrule-decidableなPASSから次工程へ進む際、別のsemantic human approvalを自動的に追加しない。
- 必要なCurrent Authority / implementation authorization recordは省略せず、Coordinator AIが既定ルールに従ってmaterializeする。
- Agent launchの実操作は人間が行ってよいが、人間のsemantic approvalと同一視しない。
- 次Agent向けpromptは前stageがcopy-paste-readyで生成する。
- 人間判断が必要になった場合だけHuman Decision Request Contractを使う。
- 正式反映前だけFinal Approval Packetで人間の最終承認を受ける。

### Leaf status synchronization

AIがmaterializeしたimplementation authorization等が、target leaf Issue本文（例: #167）の古いstatusと食い違う場合がある。今回のpilotではtarget leaf Issue本文を書き換えない。

将来のpilot実行時には、次を必須とする。

- target leaf Issueに、明示的なsuperseding status / authority commentを残す。
- 次のhandoffには、そのsuperseding commentのIDを含める。
- Fresh Context Agentは、Issue bodyだけでなく、Issue body + superseding current status commentの両方を確認する。Issue bodyの古いstatusだけを見て誤って停止しない。

---

## 9. Pilot metrics

WP2-AUTHN-01終了時に最低限、次を集計する。

```yaml
AUTHN_HUMAN_MEDIATION_METRICS:
  TARGETS:
    HANDOFF_EDIT_COUNT: 0
    RULE_DECIDABLE_HUMAN_QUERY_COUNT: 0
    FINAL_APPROVAL_RESEARCH_REQUIRED: false

  ACTUAL:
    HANDOFF_EDIT_COUNT:
    RULE_DECIDABLE_HUMAN_QUERY_COUNT:
    HUMAN_DECISION_COUNT:
    HUMAN_CORRECTION_COUNT:
    FINAL_APPROVAL_RESEARCH_REQUIRED:
```

各stage終了時に、Issue #191 commentへ`ACTUAL`のdelta / evidenceを記録する。AUTHN-01完了時に集計する。

### Meaning

`HANDOFF_EDIT_COUNT`
: Agent間のhandoff文・投入promptを人間が意味的に編集した回数。単純なコピー操作は数えない。

`RULE_DECIDABLE_HUMAN_QUERY_COUNT`
: 既存ルール・証拠で一意に決められたのに、人間へ進行可否を質問した回数。

`HUMAN_DECISION_COUNT`
: Human Decision Escalation条件に該当し、実際に人間が意味判断を行った回数。

`HUMAN_CORRECTION_COUNT`
: 人間がAIの対象誤認、次工程誤認、handoff不足等を補正しなければ進めなかった回数。

`FINAL_APPROVAL_RESEARCH_REQUIRED`
: Final Approval Packetだけでは不足し、人間が追加のGitHub調査・会話再読を必要としたか。

目標値を達成しなかったこと自体をpilot failureとはしない。必要な人間介在が見つかった場合は、その理由を次の自動化設計への入力とする。

### Post-AUTHN-01 evaluation

AUTHN-01完了後、人間はpilotについて次のいずれかを判断する。

```text
CONTINUE
MODIFY
REVOKE
```

目標未達だけをもって自動的にpilot failureとはしない。

---

## 10. Future path

このpilotが成立した場合、次の順で発展させる。

```text
manual copy-paste handoff
-> ChatGPTからMCP等で個別Agentを直接起動
-> GitHub上のstate/evidenceを次Agentが直接取得
-> coordinator/monitor agentがstate transitionを検出
-> rule-decidable taskを自動dispatch
-> Human Decision Escalation時だけ人間へ通知
-> Final Approval Packetで正式反映判断
```

MCPや特定Agent製品をワークフローの中心には置かない。

中心に置くのは、次の4つである。

1. GitHub上のcurrent state。
2. exact identityと検証証拠。
3. 次へ進む条件と停止条件。
4. AIでは決めるべきでないHuman Decision Escalation条件。

これにより、将来実行Agentや接続方式が変わっても、process contract自体を維持できる。

現時点ではMCPやtemplate engineを実装しない。ただし、将来promptを、`TARGET`、`STAGE`、`AGENT_ROLE`、`MODE`、authority / evidence pointers等の構造化情報から機械的に組み立てられるようにする、という設計原則を記録する。自由記述prompt本文だけにワークフローの意味を閉じ込めない。

---

## 11. Stop / rollback

次の場合はpilotによる自動的なstage progressionを停止する。

- 本文書の§2.5で明示的にsupersedeした項目を除き、pilot contractが既存のKoo-approved process decisionと矛盾する。
- `AUTONOMOUS_EXECUTION_SCOPE`がACTIVEでない、対象leafが一致しない、またはscope外のauthorityをmaterializeしようとしている。
- AIが製品仕様・ADR・Issue scopeを「定型判断」として変更しようとする。
- Human Decision Escalationを回避するために、曖昧な事項をAIが独断決定する。
- handoff contractの不足により人間の意味的な再構成が必要になる。
- Final Approval Packetが重大リスクを十分に表現できない。

重大なprocess defectが確認された場合は、WP2-AUTHN-01の製品実装を止め、このpilotを修正または撤回してから再開する。
