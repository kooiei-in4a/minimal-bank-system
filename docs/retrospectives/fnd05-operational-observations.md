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
