# FND-05 Retrospective

## Status

RETROSPECTIVE: IN PROGRESS

FND-05 product implementation, candidate archive, and pre-retrospective repository cleanup are complete.

This document is the working record for the FND-05 retrospective.

No FND-06 process change is authorized by this document alone.

```yaml
RETROSPECTIVE:
  STATUS: IN_PROGRESS

RETROSPECTIVE_SYNTHESIS_BODY:
  STATUS: DRAFT_RECORDED

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

今回の振り返りで前提とする主要な確定事実は次のとおり。

- Target Issue: #43 `[FND-05] Docker Compose実行基盤を確立する`
- Final Synthesis PR: #153
- Final Product Head before merge: `9e704f53911be3fdf0d09538424d3bcd9012f96a`
- Final Merge Commit: `c0b0103381ae2fc3d00a638decea36b71bda7c1f`
- PR #153: MERGED
- Issue #43: CLOSED / COMPLETED
- direct-head CI: PASS
- PR CI: PASS
- post-merge main CI: PASS
- H1 Heavy Review: APPROVE / Blocker 0 / Major 0
- H2 Heavy Review: initially CHANGES_REQUIRED / Major 2
- Conditional Judge: H2の2件をUPHELD
- Targeted Fix後のrequired targeted re-review: FIXED / COMPLETE
- full H1/H2 rerun: NOT REQUIRED

### Review identity / CI evidence

- H1 Heavy Review: `GPT-5.6 Sol` / `Codex` / APPROVE / Blocker 0 / Major 0
- H2 Heavy Review: `Claude Opus 5` / `Claude Code` / initially CHANGES_REQUIRED / Major 2
- Conditional Judge: `Composer 2.5` / `Cursor` / H2 findings upheld 2件
- direct-head CI: build and test run `31515332416`、FND-05 compose verification run `31515332435`
- PR CI: build and test run `31515336318`、FND-05 compose verification run `31515336270`
- post-merge main CI: build and test run `31520459290`

製品としては、PostgreSQL、one-shot Migrator、APIのCompose実行経路、migration成功後のAPI起動、migration失敗時のAPI非起動、digest pinning、secret外部注入、named volume、reproducible lifecycle、static / runtime / mutation verificationまでmainへ統合できた。

一方、process側では、後段stageの結果が存在していてもcanonicalな`docs/benchmarks/fnd05-model-comparison/run.json`へ最終集約されず、main上のregistryがpre-run相当の状態を残す問題が観測された。この点は製品品質とは別のprocess defectとして扱う。

---

## 3. What Worked Well

FND-05は、製品実装としては成功した。Issue #43のclose conditionに必要なCompose実行基盤をmainへ統合し、direct-head、PR、post-merge mainの各CIがPASSした。特に、migration成功後だけAPIを開始し、migration失敗時にはAPIを開始しないという重要なfailure behaviorを、runtime evidenceとmutation verificationを含めて確認できた。

### Candidateを比較してから選ぶ流れは有効だった

Candidateを共通基準で評価するImplementation Evaluationと、最終的に何を採用するかを決めるSelection / Adjudicationを分離したことは有効だった。

単純に「一番点数が高いcandidateをそのまま採用する」のではなく、各candidateの良い要素をauthority-firstで選び、Final Synthesisで一本の実装へまとめることができた。これにより、candidate scoreとmerge readinessを混同せずに済んだ。

### Final Synthesisを独立stageとして置いた価値があった

Candidateのどれかをmerge / cherry-pickするのではなく、Selectionで選ばれた要素をFresh ContextのFinal Synthesisへ渡したことで、最終実装を一度整理し直せた。

FND-05の最終製品Headは`9e704f5...`として固定され、PR #153でmainへ統合された。候補実装そのものではなく、最終的に選び直した構成を製品正本へできたことは維持すべき点である。

### Heavy Reviewを異なる観点で2本実施したことは実際に品質へ寄与した

FND-05ではHeavy Reviewer H1がBlocker 0 / Major 0でAPPROVEした一方、H2は2件のMajorを発見した。

- exit-0 maskingが、意図したreal Migrator failureの後に発生したことを証明できていなかった
- mutation killがshipped volume-policy oracleではなくinline self-checkへ依存していた

Conditional Judgeはこの2件を両方UPHELDした。したがって、「1本のHeavy ReviewでMajor 0なら十分」とは言えない実例になった。

同じ観点のreviewを2回繰り返したのではなく、architecture / contractと、adversarial failure / false assuranceという異なるperspectiveを割り当てたことが意味を持った。

### Targeted Fix / Targeted Re-reviewは効率が良かった

H2の2件に対しては、対象を`tests/fnd05/verify-mutations.sh`と`tests/fnd05/static-gate.sh`へ限定してTargeted Fixを行い、そのchanged surfaceだけをfinding ownerと別perspective verifierで再確認した。

結果として両findingはFIXEDとなり、新しいBlocker / Majorはなく、full H1/H2 rerunは不要と判断できた。

これは「重大な問題が1件見つかるたびに全reviewを最初からやり直す」のではなく、変更範囲とroot causeを限定できる場合はTargeted Re-reviewで十分という実証になった。

### exact identityとevidenceを重視したことは有効だった

Final Product Head、merge commit、actual checkout、critical artifact hash、Git blob identity、producer commitなどを区別して扱ったことは、どの成果物をreviewしたのかを明確にするうえで有効だった。

特にFND-05のようにcandidate、selection、final synthesis、複数review、targeted fixが連続するprocessでは、「その内容を見たつもり」ではなく「どのexact Head / artifactを見たか」を固定する必要がある。

この厳密さ自体は削らず、人間による取得・転記作業を減らす方向がよい。

### Observation Ledgerでrun中の改善とcurrent runを分離できた

実行中に見つかったprocess改善案をその場でcurrent runへ混ぜず、Observationとして残してretrospectiveで判断する運用は有効だった。

これにより、FND-05の実験条件を途中で変えずに、O-01〜O-07を後からADOPT / DEFER / REJECTで判断できた。改善を止めるのではなく、「今のrunを変えること」と「次回へ学びを残すこと」を分離できた点を維持する。

---

## 4. What Did Not Work Well

FND-05の主な問題は、品質確認そのものではなく、その周辺のoperator作業が重くなりすぎたことである。

### canonical `run.json`が最終状態へ追いつかなかった

もっとも明確なprocess defectは、後段stageが完了しているにもかかわらず、canonical registryとされた`docs/benchmarks/fnd05-model-comparison/run.json`へ最終結果が集約されなかったことである。

main上の`run.json`は、FND-05製品がmerge・Issue closeまで完了した後も、statusやcandidate / review stateにpre-run相当の値を残していた。

つまり、人間は最終状態を知るために複数branch、artifact、PR、review結果を横断して確認する必要があった。これは「証拠を厳密に残した」こととは別問題であり、single authoritative registryとしては不十分だった。

### SHA / identityの確認方法は正しかったが、人間の転記が多すぎた

exact Headやartifact hashの確認は必要だったが、同じSHAやidentityをprompt、Issue、artifact、`run.json`、handoffへ何度も手入力する場面が多かった。

確認を厳密にするほどoperatorがcopy / pasteする箇所も増え、転記ミスを防ぐための確認が、別の転記ミスを生む可能性を持つ状態になっていた。

問題はidentity verificationではなく、identityの取得・再利用方法である。

### handoffを毎回人間が組み立てる負荷が大きかった

Model / Harness / Effort / Context、Target Head、artifact identity、STOP条件、返却evidence、next actionをstageごとに人間が組み立てていた。

これらの多くはauthoritative metadataから機械的に取得できる。意味判断が必要な部分と、転記するだけの部分を分けられていなかった。

### Light Reviewへ機械的な確認が混ざっていた

checkout identity、hash、required-field completeness、file placementなど、短時間でYES / NO判定できる項目まで人間のReviewerへ渡していた。

これらはsemantic reviewではなくmechanical verificationである。人間Reviewerにはcontract correctness、oracle correctness、negative case、false assuranceなどの意味判断へ集中してもらう方がよい。

### `CI GREEN`や`Mutation KILLED`だけではfalse assuranceを防げなかった

FND-05で最も重要な技術的な学びの一つである。

H2が見つけた2件は、testやmutationの結果だけを表面的に見ると「検証できている」と誤認しやすい内容だった。

mutation後にREDになったとしても、狙ったfailure behaviorによってREDになったとは限らない。どの理由で失敗したかまで確認しないと、oracle correctnessを証明できない。

このため、すべてのmutationを重いframeworkへするのではなく、critical mutationについてだけbaseline GREEN → mutation → expected reasonでRED → restore GREENを確認する必要がある。

### 固定3候補とLight Review 2本は制度として硬すぎた

FND-05では固定3候補とLight Review 2本を使ったが、今後すべてのIssueで同じ数を要求する根拠はない。

重要なのは数そのものよりperspective diversityである。Candidateはriskと目的に応じて2〜3候補を選べる制度へし、Light Reviewはmechanical checkを外したうえでsemantic review 1本へ簡略化する方がよい。

ただしFND-06では他のprocess変更が多いため、candidate数削減の実験までは同時に行わない。

### branch / archive管理も手作業が多かった

review-control branchやcandidate branchを証拠保持のため長く残すと、どれがcurrent authorityか判断しづらくなる。

一方で、単純にbranchを削除すると復元性を失う。最終manifestとrecovery tagを作ってから不要branchを整理する仕組みが必要だが、FND-06へ新しい変更要因としては追加しない。

---

## 5. Quality Gain vs Process Cost

FND-05では、品質を上げた工程と、人間の作業量だけを増やした部分を分ける必要がある。

| 項目 | 品質への寄与 | Process Cost | 判断 |
| --- | --- | --- | --- |
| Implementation Evaluation | 高い | 中 | KEEP |
| Selection / Adjudication | 高い | 中 | KEEP |
| Final Synthesis | 高い | 中〜高 | KEEP。handoffは自動化方向 |
| Light Review ×2 | 中 | 高 | SIMPLIFY |
| Heavy Review ×2 | 非常に高い | 高 | KEEP |
| Conditional Judge | disagreement時は高い | 常時実行すると高い | KEEP / default OFF |
| Targeted Fix / Targeted Re-review | 高い | 低〜中 | KEEP |
| exact identity / hash verification | 高い | 手作業では高い | KEEPしつつ自動化 |
| manual handoff assembly | 直接の品質寄与は低い | 高い | SIMPLIFY / generate |
| manual SHA duplication | 直接の品質寄与は低い | 高い | REMOVE |
| branchによる長期evidence保持 | 一部有効 | 高い | 後でSIMPLIFY |

FND-05では、Light Review 2本（`light_l1 -> locked`、`light_l2 -> locked`）のfindingを受けて`light_fix -> locked`が実施され、`final-synthesis/light-findings-fix-result.md`が作成・lockされた後、修正後のHeadがHeavy Reviewへhandoffされたため、Light Reviewには実際の品質寄与があった。一方、mechanical checkまでReviewerに持たせていたため、次回はsemantic review 1本へ簡略化する。

### 高いコストでも残すもの

Heavy Review ×2はコストが高いが、FND-05でH1 Major 0の後にH2がMajor 2を発見しており、品質寄与が実証された。そのため現時点では削減しない。

Selection / AdjudicationとFinal Synthesisも、candidateを丸ごと採用せず最終製品をauthority-firstで組み直す役割があり、残す価値がある。

### 品質を落とさず軽くできるもの

Light Reviewは、mechanical checkをFast Mechanical Gateへ移せばsemantic reviewを1本へ減らせる。

SHA / artifact identity確認も確認自体は残し、authoritative metadataから一度取得して複数stageで再利用すればoperator costを減らせる。

handoffも同様に、生成だけを自動化し、人間が確認してから投入する形なら安全性を大きく変えずに負担を減らせる。

### 削るべきなのは品質stageより重複作業

FND-05から得られた中心的な判断は、ReviewやEvidenceを単純に減らすことではない。

削るべきなのは、同じSHAの手入力、同じmetadataの再転記、機械で判定できる項目を人間Reviewerが繰り返し読むこと、不要になったbranchを人間が個別に管理することなどである。

品質を守る工程は残し、mechanical / repetitive workを自動化または簡略化する方向が妥当である。

---

## 6. Candidate / Review Lessons

### Candidate数よりperspectiveの違いが重要

「3候補あるから安全」ではなく、候補ごとに何を違う角度から考えさせるかが重要である。

今後は固定3候補制を廃止し、riskと必要なperspectiveに応じて2〜3候補を選べるようにする。ただしFND-06ではcandidate数削減そのものは実験しない。

### Candidate scoreとmerge readinessは別物

Implementation Evaluationは候補を比較するために必要だが、高得点candidateをそのままmergeしてよいとは限らない。

Selection / Adjudicationで要素を選び、Final Synthesisで最終実装を作る役割は分離したまま維持する。

### 「GREEN」「KILLED」は意味まで保証しない

CIがGREENでも、mutationがKILLEDでも、それだけではoracleが正しい理由で反応したとは限らない。

重要なfailure behaviorでは、expected failure signatureまで確認する。FND-06ではcritical mutation 1〜3件程度へ限定してこの確認を試す。

### Heavy Reviewerは人数だけでなくperspective diversityを固定する

FND-05ではH1とH2の結果が分かれたため、Heavy Reviewを2本維持する根拠がある。

ただし同じ観点を2回聞くのではなく、architecture / contractとadversarial failure / false assuranceのようにreview responsibilityを分ける。

### 第3Reviewerは常設しなくてよい

Conditional Judgeは、Blocker / Major、root cause、fix direction、merge readinessなどでmeaningful disagreementがある場合だけ起動する。

FND-05ではH2 findingをJudgeがUPHELDし、Targeted Fixへ進む判断に役立った。一方、常時3本目のfull reviewとして実行する必要はない。

### 小さな修正はTargeted Re-reviewで十分な場合がある

changed surfaceが限定され、root causeが明確で、exact-head CIがGREENであり、finding ownerと別perspective verifierが確認できる場合は、full Heavy rerunを標準にしない。

ただしproduction architecture、security boundary、cross-cutting変更などへ広がった場合はfull re-reviewへ戻る。

### identityの正本はAgent自己申告ではなく外部metadataを優先する

Model / Harness identity、actual checkout、artifact identityなどは、取得できる範囲でHarness / platform metadataやmachine-readable registryを正本とする。

Agent self-reportだけをauthoritative evidenceにしない。

### run中に見つけた改善は次回へ送る

良い改善案でもcurrent runへ途中投入すると、比較条件が変わる。

Observation Ledgerへ記録してretrospectiveで採否を決める方法を維持する。

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

---

## 8. Keep / Simplify / Remove

FND-05の結果から、次回以降のprocessを以下のように整理する。

### KEEP

- Implementation Evaluation
  - candidateを共通基準で比較するstageとして維持する
- Selection / Adjudication
  - winner-take-allではなく、authority-firstで要素を選ぶ独立stageとして維持する
- Final Synthesis
  - 選択済み要素から最終実装を作るstageとして維持する
- Heavy Review ×2
  - perspective diversityを必須にして維持する
- Conditional Judge
  - default OFFのまま、meaningful disagreement時だけ使う
- Targeted Fix / Targeted Re-review
  - changed surfaceを限定できる場合の標準的な回復経路として維持する
- critical identity / artifact verification
  - exact Head、critical artifact hash、producer identity等は削らない
- Observation Ledger
  - current runを変えずに改善案を次回へ送る仕組みとして維持する

### SIMPLIFY

- Candidate Policy
  - 「常に3候補」を廃止し、riskとperspectiveに応じた2〜3候補制へする
  - ただしFND-06ではcandidate数削減実験は行わない
- Light Review
  - Light Review ×2から、Fast Mechanical Gate + Semantic Light Review ×1へ簡略化する
- Mechanical verification
  - checkout identity、run.json completeness、critical artifact identity/hashなどをFast Mechanical Gateへ移す
- `run.json`
  - stage結果をfinal consolidationし、required-stage completenessをwarning-levelから確認する
- Execution handoff
  - metadataからgenerate-onlyで作り、人間が確認してから投入する
- Identity / SHA handling
  - 同じidentity source / logicをD-03 / D-04で再利用し、別systemを増やさない
- Mutation verification
  - 全mutationの重いframeworkではなくcritical mutation 1〜3件程度から始める
- Core Spec / JIT Stage Spec
  - Core Spec 1件、JIT Stage Spec 1件のminimal pilotに限定する
- Branch / Archive cleanup
  - 将来はmanifest + recovery tagへ寄せるが、FND-06では手動運用を続ける

### REMOVE / DO NOT STANDARDIZE

- 固定3候補を絶対ルールにすること
- 同じSHAをprompt / Issue / artifact / `run.json`へ何度も手入力すること
- generated metadataを別Markdownへ再転記すること
- Agent self-reportだけをModel / Harness identityのauthorityにすること
- fast / deterministicなYES / NO確認を人間Reviewerが毎回読むこと
- 小さなTargeted Fixでも無条件にfull Heavy Reviewを最初からやり直すこと
- 全mutationへ一律のmeta-verification frameworkを要求すること
- JIT Stage Specを全stageへ義務化すること
- Core Specの重いversion / dependency / drift管理を最初から導入すること
- recovery verification前にbranchを自動削除すること
- generated handoffからAgent実行、Ready化、merge、next stageまで自動で進めること

ここでREMOVEとするのは、品質を守るstageそのものではなく、重複した手作業や過剰な標準化である。

---

## 9. Candidate Improvements for FND-06

FND-06では改善を一度に大量導入せず、Section Dで選定したD-01〜D-05だけをpilot対象とする。

### D-01 — `run.json` Final Consolidation

- final run.json consolidation
- required-stage completeness check
- 最初はwarning-only
- merge blockerにはしない

目的は、実際のstage完了状態とcanonical registryがずれる問題を減らすことである。

### D-02 — Mutation Meta-Verifier

- critical mutationだけを対象にする
- 目安は1〜3件
- baseline GREEN
- mutation適用
- shipped oracleがexpected reasonでRED
- mutation restore
- baseline GREENへ復帰

「REDになった」ではなく「狙った理由でREDになった」を確認する。

### D-03 — Generated Execution Handoff

- generate-only
- Model / Harness / Effort / Context / Target Head / STOP条件 / required return evidence等をauthoritative metadataから可能な範囲で生成する
- Kooが確認する
- 人間が対象Harnessへ投入する
- Agent自動起動、auto next-stage、Ready化、mergeはしない

### D-04 — Fast Mechanical Gate

初期pilotは3チェックだけにする。

1. checkout identity
2. `run.json` required-stage completeness
3. critical artifact identity/hash

warning-onlyで開始し、false warningやoperator correction、実行時間を観測する。

### D-05 — Core Spec / JIT Stage Spec Minimal Pilot

- Core Spec: 1件
  - initial implementation前に作る
  - 「今回何を作るか」を簡潔に固定する
- JIT Stage Spec: 1件
  - Final Synthesis → Semantic Light Review handoffだけを対象にする
  - Final Synthesis完了、Target Head / input artifact確定後、Light Review開始直前に作る

Acceptance Criteria、重要architecture / security / persistence / failure / critical oracle requirementはJITへ遅延させずCore Spec側で最初から伝える。

### FND-06へ追加しないもの

- D-06 minimal EOL contract
  - 方向性はADOPTのままだがFND-06からはDEFER
- D-07 Identity / SHA automation standalone pilot
  - 方向性はADOPTのまま、D-03 / D-04へ統合し独立pilotにはしない
- D-08 Branch / Archive cleanup automation
  - 方向性はADOPTのまま、FND-06ではmanual cleanupを継続する
- candidate数削減実験
  - 固定3候補制の廃止方針は維持するが、FND-06でcandidate数削減まで同時に試さない

FND-06の目的は、一度にprocess全体を作り直すことではない。品質を守るreview funnelは大きく変えず、人間が繰り返していたmechanical / transcription workだけを小さく減らせるか確認する。

---

## 10. Decisions

このsectionは、Kooが確定した開発フロー判断と、後続判断のために整理した証拠・自動化のdecision packageを記録する。

Section Bはprocess変更の実装承認ではない。Section C / Dの判断およびretrospective本文の記録だけでは、AGENTS.md、CI、script、test、run registryその他のprocess codeへ反映する承認にならない。

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

RETROSPECTIVE_SYNTHESIS_BODY:
  STATUS: DRAFT_RECORDED

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

What Worked Well、What Did Not Work Well、Quality Gain vs Process Cost、Candidate / Review Lessons、Keep / Simplify / Remove、Candidate Improvements for FND-06のsynthesis本文はdraftとして記録した。

次は、このretrospective本文がIndependent Review A / B、Narrow Synthesis、GitHub一次証拠、KooのSection A〜D判断と矛盾していないかを最終cross-checkする。

cross-check後も自動的にretrospectiveをCOMPLETE、PR #154をReady、merge、またはFND-06開始へ進めない。Kooの明示判断を待つ。

retrospectiveは`IN_PROGRESS`、PR #154はDraft、FND-06は`NOT_STARTED`、process変更は`NOT_AUTHORIZED`のまま維持する。
