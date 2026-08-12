# FND-05 Operational Observations

この文書はFND-05実行中に得られた運用上の気づきを、後続の振り返り・通常開発プロセス改善の入力として記録する。

**Non-normative:** 本文書は現在進行中のFND-05 benchmarkのProduct authority、D-01〜D-08、locked prompt、Evaluation / Selection artifact、scoring、mutation contract、stage gateを変更しない。FND-05の現行runへ遡及適用しない。

## O-01 — 実行プロンプトは「投入」だけでなくhandoff全体を指示する

### Observation

AIエージェントへ長い実行プロンプトを渡す際、プロンプト本文だけを提示すると、人間側に次の判断が残る。

- どのModelへ投入するか
- どのHarnessを使うか
- Effortはいくつか
- Fresh Contextか継続sessionか
- 実行後にどこで停止するか
- 出力結果のどの項目をCoordinatorへ返すか
- 次工程を自律的に開始してよいか

FND-05のSelection / Adjudication後の運用では、実行プロンプトと一緒にこれらを明示した方が、stage間handoffが分かりやすく、誤ったモデル投入、session再利用、結果の要約によるidentity欠落、次工程の先行開始を防ぎやすかった。

### Recommended handoff shape

今後の実行用プロンプトは、原則として次を1セットで提示する。

```text
1. 投入先
   - MODEL
   - HARNESS
   - EFFORT
   - CONTEXT: Fresh / Continue
   - ROLE
   - LOCKED / Coordinator Recommended の区別

2. コピペ可能な完全版プロンプト

3. 投入後のアクション
   - STOP条件まで実行
   - 禁止されているmerge / Ready化 / 次工程開始を行わない

4. Coordinatorへ共有する結果
   - 最終応答全文
   - branch / Head / commit / PR
   - artifact path / SHA256
   - CI run / actual checkout SHA
   - verdict / findings / unverified / STOP reason

5. 結果共有後の次工程
   - Coordinatorが一次証拠と突合する
   - PASSするまで次stageを開始しない
```

### Why it matters

この形式はprompt本文の品質とは別の、**execution handoff contract**として機能する。

特にFND-05のように、Evaluation → Selection → Final Synthesis → Light → Heavyとstage artifact identityを受け渡すプロセスでは、次の誤りを抑制できる。

- lockedされたModel / Harnessと異なる実行先へ投入する
- Fresh Context必須stageを既存sessionで継続する
- artifact SHAやHead SHAを省略した要約だけがCoordinatorへ戻る
- Agentが自分のstage完了後に次stageへ進む
- Coordinatorの一次証拠validation前にdownstream作業を開始する

### FND-05 treatment

FND-05の現行runでは、この気づきを**途中でbenchmark ruleへ追加しない**。

現在lock済みのD-01〜D-08、prompt revisions、Evaluation / Selection結果、Final Synthesis Author identity、review funnelはそのまま維持する。

FND-05終了後のretrospectiveで、通常開発およびFND-06以降へ採用するかを判断する。

### Candidate future rule

将来正式化する場合は、次のようなprocess-level rule候補とする。

> 実行可能なstage promptを人間へhandoffする際は、prompt本文だけでなく、投入先identity、context policy、実行後STOP、必須返却evidence、Coordinator validation後のnext actionを同時に提示する。

これはProduct behaviorのMUSTではなく、AI-assisted development processのhandoff ruleとして扱う。

---

## O-02 — Model identityはAgent自己申告ではなく外部execution metadataを正本にする

### Observation

FND-05 Final Synthesis開始時、Codex側ではD-08どおり`GPT-5.6 Terra / Codex / xHigh`を選択していたにもかかわらず、実行中のAgentが自分のModel identityを不一致と判断し、`STOP_AND_RELOCK`した。

このSTOPはlocked instructionに忠実なfail-closed動作だった一方、停止判定の入力に**Agent自身のmodel自己認識**を用いたことでfalse STOPになった。

### Implication

Model / Harness / Effortのexecution identityは、Agentが内部から推測・自己申告する対象ではなく、次のような**外部execution metadata**を正本にする方が安定する。

```text
Harness / UI / API側で選択されたModel
Harness identity
Effort設定
Fresh / Continue context policy
必要ならoperator attestation
```

Agent側はこれをlocked inputとしてconsumeし、自分自身のmodel名を再推論してgate判定しない。

### Recommended identity evidence order

将来正式化する場合、execution identityの証拠順序は例えば次とする。

```text
1. Harness / platform execution metadata
2. machine-readable run metadata / invocation record
3. operator-attested selection when platform metadataをartifact化できない場合
4. Agent self-reportは補助情報のみ
```

Agent self-report単独で、外部identityと矛盾するSTOP / re-lockを発火させない。

### Why it matters

この区別により、次を抑制できる。

- 正しいModelを選択済みなのにAgentの自己認識で停止する
- Model alias / product label /内部名称差による誤判定
- Harnessが保持する実行identityよりAgent自己申告を優先する
- benchmarkのModel attributionを会話文だけに依存する

一方、外部execution metadataがlocked identityと本当に不一致である場合は、従来どおりfail-closedで停止する。

### FND-05 treatment

FND-05ではD-08自体を変更しない。

今回のfalse STOPを理由にTerraから別Modelへ再lockせず、`GPT-5.6 Terra / Codex / xHigh / Fresh Context`のlockを維持する。再実行時はCodex側で確認済みの外部identityをexecution identity evidenceとして扱う。

現行prompt revisionへ遡及的な仕様変更は加えず、実行時handoff上のidentity attestationとして扱う。

### Candidate future rule

> Model / Harness / Effortをgate条件にする場合、identityの正本はHarnessまたは外部run metadataとし、Agent自身のmodel自己認識をauthoritative evidenceにしない。

---

## O-03 — 実行中の「気づき」をnon-normative Observation Ledgerへ即時記録する

### Observation

FND-05では、実行中に見つかったprocess上の改善点を、その場でbenchmark ruleへ混ぜず、独立した`Operational Observations`へ記録した。

O-01のhandoff contract、O-02のexecution identity sourceはいずれも、FND-05の現行locked条件を変更すべき内容ではない一方、run終了後のretrospectiveやFND-06以降へ引き継ぐ価値が高い。

このため、**「気づきを残すための専用レイヤー」自体が有効なprocess mechanism**と考えられる。

### Problem it solves

実験・開発runの途中で改善点を見つけた場合、選択肢が「今すぐルールを変える」か「後で覚えていれば振り返る」だけだと、次の問題が起きる。

- run途中のrule変更でbenchmark fairnessを壊す
- 後日のretrospectiveで細部や発見理由を忘れる
- 結果を見た後に過去の気づきを再構成し、hindsight biasが入る
- 単なる思いつきと、実際に発生した運用問題が混ざる
- 改善案が会話ログだけに残り、次runへ継承されない

Observation Ledgerは、**current runを変えずに、発見時点の知見を保存する中間層**として機能する。

### Recommended observation record

各Observationは、少なくとも次を持つとよい。

```text
ID / title
Observation: 何が起きたか
Trigger / evidence: 何を見て気づいたか
Implication: なぜ重要か
Current-run treatment: 今回はどう扱うか
Candidate future rule: 将来どうformalizeできるか
Adoption status: OBSERVED / REVIEWED / ADOPTED / REJECTED / DEFERRED
```

必要なら発見stage、関連artifact / PR / run、日時も付ける。

### Governance principle

Observationの記録とprocess ruleの採用を分離する。

```text
OBSERVE
  ↓
non-normative ledgerへ即時記録
  ↓
current runはlocked条件を維持
  ↓
retrospectiveでreview
  ↓
ADOPT / REJECT / DEFER
  ↓
採用する場合だけ次runのpre-run contractへ反映
```

これにより、「気づいたから今すぐルール化する」という過剰反応と、「後で検討しようとして消える」という取りこぼしの両方を避けられる。

### FND-05 treatment

この`docs/retrospectives/fnd05-operational-observations.md`自体を、FND-05におけるObservation Ledgerの試行とする。

本文書は独立docs-only branchで保持し、FND-05のProduct authority、D-lock、stage artifact、candidate、Final Synthesis branchへ影響させない。

FND-05終了時のretrospectiveで、O-01〜O-03をそれぞれ採用・棄却・保留に分類する。

### Candidate future rule

> AI-assisted development runでは、実行中に発見したprocess improvementをcurrent locked runへ直接混ぜず、non-normative Observation Ledgerへ発見時点で記録する。run終了後のretrospectiveで採否を決め、採用項目だけを次runのpre-run contractへ昇格する。

---

## O-04 — stage handoff hashはexact Git blobから再計算する

### Observation

Final Synthesis handoffで、pre-execution `run.json@3344c9025c2f0b2cf1dc1baa685fb872fcb44120`について、人間向けPreparation報告とFinal Synthesis lockに異なるSHA-256が記録された。

Reconciliationではworktreeの現ファイルではなく、exact commitのGit blobを`git show <commit>:<path> | sha256sum`で再計算し、Preparation報告値が正しいことを確定できた。

### Implication

stage handoffでhashを二重記録するだけでは、転記ミスや別revisionのhash混入を防げない。downstream開始前に、**logical filenameではなくexact commit上のraw blobを再hashするverification step**を置く方が安全である。

### Recommended check

```text
artifact / registry identity handoff
  ↓
exact commit SHAを固定
  ↓
git show <commit>:<path> でraw blob取得
  ↓
SHA-256再計算
  ↓
reported value / run.json lockと照合
  ↓
一致したidentityだけdownstreamへ渡す
```

PowerShell等のtext pipelineで改行・encoding変換が入り得る場合は、raw blobを一時ファイルへ保存してhashする。

### FND-05 treatment

FND-05ではFinal Synthesis実装自体をやり直さず、誤ったsource artifact hashだけをmetadata correction対象とする。

### Candidate future rule

> Immutable stage handoffのhashは、downstream開始直前にexact Git commit上のraw blobから独立再計算し、人間向け報告値とregistry lockの両方へ照合する。

---

## O-05 — artifact生成commitとlock commitを分離する

### Observation

FND-05のstage artifact contractは`artifact_path`、`content_sha256`、`target_head_sha`、`producer_commit_sha`等を要求する。Final Synthesisでは実装commit、artifact追加commit、run registry lockの責務が近接し、`producer_commit_sha`がartifactを実際に含むcommitと一致しているかをdownstream前に再確認する必要が生じた。

### Implication

artifact本文と、そのartifactのhash・producer commitを同一commit内で完全に自己参照させることはできない。したがって、stage artifactは原則として**二段階commit**にするとidentityが明確になる。

```text
Commit A — artifact production
  - reviewed / produced artifactを追加
  - target Headを確定

Commit B — external lock
  - run.jsonへartifact path / SHA256 / target_head_shaを記録
  - producer_commit_sha = Commit A
```

この形なら、downstreamは`producer_commit_sha`をcheckoutしてartifactが実在し、そのblob hashが`content_sha256`と一致することを直接確認できる。

### Why it matters

- producer commitからartifactを取得できないidentity driftを防ぐ
- target code Headとartifact producer commitを区別できる
- lock commit自身のself-reference問題を避ける
- downstream reviewerがexact refから再現可能になる
- metadata-only correctionが製品Headの意味を曖昧にしにくい

### FND-05 treatment

FND-05 current runではProduct codeやmutation結果を変更せず、Final Synthesis artifact identityとS0 handoffをmetadata-onlyで正規化してからLight Reviewへ進む。

### Candidate future rule

> Stage artifactは原則としてartifact production commitとregistry lock commitを分離し、`producer_commit_sha`はartifact blobを実際に含むcommitを指す。`target_head_sha`はreview対象のexact Headとして別に管理する。

---

## O-06 — FND-06以降でJust-in-Time SpecとCI Rule Checkを段階的に試す

### Status

`OBSERVED / FND-06+ EXPERIMENT CANDIDATE`

これはFND-05で得た**改善仮説**であり、確定済みの恒久ルールではない。FND-05の現在のcontract、prompt、candidate、evaluation、Selection、Final Synthesis、review flowには追加しない。

### Observation — Just-in-Time Spec

AI主体でIssueを進める場合、すべての詳細Specを長期間保守するより、**そのIssueに必要な詳細だけを開始時点で作り、Issue完了後に残す価値がある部分だけを恒久的な正本へ昇格する**方が合う可能性がある。

候補となる流れは次のとおり。

```text
Issue開始
  ↓
そのIssueに必要な範囲だけ軽量Specを作る
  ↓
ADR / 承認済み仕様 / 既存ルールと照合
  ↓
AIが実装
  ↓
test / CI / reviewで検証
  ↓
Issue完了
  ↓
残す価値がある内容だけ適切な正本へ昇格
```

FND-05で使った次の要素は、このJust-in-Time Specに近いものとして後で評価する。

- Implementation and Test Design Contract
- Acceptance Criteria
- Project Rule Catalog
- prohibited patterns
- required evidence
- mandatory mutations

Issue固有の細かな説明を永久に保守することは前提にしない。

### Observation — AI ReviewからCI / automated testへ移せるルールがある

AI Reviewerが毎回確認している項目の中には、判断力よりも**機械的なYES / NO判定**に向くものがある。

例えば次のような項目は、FND-06以降で少数からCI / automated testへ移す候補とする。

```text
secret / credentialがrepositoryへ混入していない
container imageがdigest固定されている
package versionが固定されている
禁止されたproject dependencyがない
Docker Compose configurationが妥当
API startupがmigrationを勝手に実行していない
禁止されたpath / patternが存在しない
必須testが存在する
必須runtime evidenceを取得できる
```

目的はCIを増やすことではない。

> AIに毎回「忘れず守ってください」と指示しなくてもよい項目を増やす。

ことが目的である。

一方、次のような内容は単純なYES / NOにしにくいため、当面AI Reviewerの担当として残す。

```text
設計がADRの意図に合っているか
責任分離が妥当か
failure handlingが十分安全か
hidden dependencyがないか
testがfalse assuranceになっていないか
```

### Rule promotion model

開発中に見つかったルールを、最初からすべて恒久ルールにはしない。

候補となる昇格の流れは次のとおり。

```text
問題を発見
  ↓
AI Review / 人間が確認
  ↓
次のJIT Specで明示
  ↓
複数Issueで有効性を確認
  ↓
繰り返し必要と判明
  ↓
適切な正本へ昇格
```

昇格先の考え方は次のとおり。

```text
重要な設計判断
  → ADR

長期間守る製品ルール
  → Product Specification / Invariant

AI作業者が共通で守る開発ルール
  → AGENTS.md / Project Rule Catalog

機械判定できるルール
  → CI / automated test

Issue固有の詳細
  → JIT Specとして役目を終える
```

これにより、ルールや文書を無制限に増やさないことを狙う。

### Phased rollout candidate

一度に大きく変えない。

```text
FND-05
  現行方式のまま完了
  ↓
Retrospective
  JIT Spec候補 / CI化候補を抽出
  ↓
FND-06
  Issue単位の軽量JIT Specを小さく試す
  明らかに機械判定できるルールを少数だけCI化
  ↓
WP-2以降
  効果を確認しながら対象を広げる
```

FND-06で試した結果が悪ければ、採用範囲を縮小・撤回できるようにする。

### Future observation axes

現時点では新しいKPIやGateを追加しないが、Loop Engineeringが成熟しているかを見る材料として、将来は次の分担の変化を観測する候補とする。

```text
人間が判断するもの
AI Reviewerが判断するもの
CIが判断するもの
Automated Testが判断するもの
runtime evidenceで自動確認できるもの
```

成熟に伴い、**人間やAI Reviewerが毎回確認する単純項目が減り、CI / test / runtime evidenceで自動判定できる割合が増えるか**を見たい。

ただしFND-06開始時点でKPI化することは前提にしない。まずは観測可能な形にできるかを検討する。

### FND-05 treatment

FND-05にはJust-in-Time Specの新制度も、新しいCI ruleも追加しない。FND-05の実験条件を途中で変えないことを優先する。

### FND-06 follow-up

FND-06開始前に次を再検討する。

1. Issue単位の軽量JIT Specをどの最小形式で試すか
2. FND-05でAI Reviewerが繰り返し確認した項目から、機械判定しやすいものを1〜数件だけ選べるか
3. JIT SpecからADR / Specification / AGENTS.md / CI / testへ昇格する判断基準をどこまで軽く定義するか
4. 人間 / AI Review / CI / automated test / runtime evidenceの役割分担を、追加負担なしで観測できるか

### Core hypothesis

> FND-05を通じて、Issueごとに必要な詳細仕様をその時点で生成し、完了後は必要な部分だけをADR・恒久仕様・CI・test等へ昇格させる「Just-in-Time Spec」の考え方が、AI主体の開発と相性が良い可能性があると考えた。
>
> また、AI Reviewerが繰り返し確認するルールのうち、機械判定可能なものは徐々にCI / automated testへ移し、AIの注意力に依存する範囲を減らすことが、半自動化へ向けた重要な改善候補である。
>
> FND-05では実験条件を変更せず、FND-06以降で小さく導入し、有効性を確認しながら対象を広げる。

---

## O-07 — Windows / WSL Git EOL contract

Source observation:

`docs/retrospectives/fnd05-operational-observation-o07-git-eol.md`

Status: `OBSERVED / FND-06+ IMPROVEMENT CANDIDATE`

この文書はFND-05実行中に確認した運用上の気づきを記録する補足メモである。

**Non-normative:** FND-05の現在のcontract、prompt、candidate、evaluation、Selection、Final Synthesis、review flow、Git設定を変更しない。FND-06以降の改善候補として扱う。

## Observation

FND-05のPre-Light handoff準備で、開始時はcleanだったworktreeに83件のtracked dirty fileが見えた。

調査の結果、83件は製品内容の変更ではなく、LF / CRLFの違いだけだった。

確認できた状態は次のとおり。

```text
system core.autocrlf = true
local worktree core.autocrlf = false
core.eol = unset
core.safecrlf = unset
.editorconfig end_of_line = lf
.gitattributes = not present
```

以前Git for Windows側の`core.autocrlf=true`でCRLFとして配置されたファイルが、現在のworktreeの`core.autocrlf=false`では差分として見えていた。

83件の代表diffは全行のLF→CRLF差だけで、`git diff --ignore-space-at-eol`でも実内容差は確認されなかった。

## Static Gate was not the cause

疑わしかった次のcommandをexact Headから作成した隔離worktreeで単独実行した。

```bash
git -c core.autocrlf=true diff --check
```

結果としてworktreeはdirtyにならなかった。

さらに同じ隔離worktreeでFND-05 Static Gate全体を実行してもPASSし、worktreeはdirtyにならなかった。

したがって今回の原因はStatic Gateの副作用ではなく、Windows / WSL間のGit設定と既存worktree実体の不一致と判断した。

## Key lesson

`.editorconfig`の`end_of_line = lf`はEditorやformatterへの方針であり、Git checkout時の改行変換ルールにはならない。

Windows / WSLをまたいで同じrepositoryを扱う場合、ユーザーやworktreeごとの`core.autocrlf`だけに依存すると、同じcommitでもworktree状態の見え方が変わる可能性がある。

## FND-05 treatment

FND-05ではbenchmark条件を途中で変えないため、次は行わない。

- `.gitattributes`追加
- repositoryの恒久EOL contract変更
- `core.autocrlf`方針変更
- Static Gate変更

83件のEOL-only差分は、一次証拠で対象を固定したうえで明示的にrestoreし、製品内容に変更がないことを確認した。

## FND-06+ improvement candidate

FND-06開始前またはFND-06の小規模改善として、次を検討する。

1. `.gitattributes`でGit側のtext / EOL contractを明示する必要があるか。
2. Windows GitとWSL Gitで同じrepository/worktreeを共有しない運用にできるか。
3. Gate実行前後で`git status --porcelain`、`git config --show-origin --get core.autocrlf`、`git ls-files --eol`を確認する軽量preflightが有効か。
4. CI / validatorはread-onlyであることを、隔離worktreeで必要に応じて確認できる形にするか。

目的はGit設定を増やすことではなく、**実内容の変更と環境由来のEOL差分を混同しないこと**である。

## Candidate future rule

> Windows / WSL混在環境では、EditorConfigだけをGitのEOL契約とみなさない。Git側のEOL contractを明示するか、実行環境を分離し、重要なGate前後では同じGit環境でworktree状態とEOL状態を確認する。

これはFND-05で確定した恒久ルールではなく、FND-06以降で採否を判断する改善候補とする。
