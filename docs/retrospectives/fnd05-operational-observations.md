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
