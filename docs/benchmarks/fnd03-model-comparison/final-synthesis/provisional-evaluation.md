# Issue #41 FND-03 — AIコーディングモデル実装比較・最終評価

STATUS: **SUPERSEDED / HISTORICAL**

This evaluation was produced before the subsequent independent-review benchmark identified the Testcontainers container-cleanup Major.

Do not use the 98/100 provisional score as the final merge-readiness verdict.

日本語注記: 本書の`98 / 100`は当時の評価結果として保存するが、現在の最終merge-readiness verdictではない。

Issue #41 `[FND-03] 実PostgreSQL integration test基盤を確立する` を、複数の **Model + Agent/Harness** に同一条件で独立実装させ、実際のGitHub成果物を基に比較評価した結果をまとめる。

評価対象はモデル単体ではなく、**「モデル + 実行環境による1回の実装試行」**である。

モデルの一般的な評判や公開ベンチマークではなく、各candidateの実コード、テスト、CI、Issue適合性を一次証拠として採点した。

13 candidateの比較完了後、比較結果を利用したFinal Synthesis `agent/issue-41-fnd-03-final-code` が実装されたため、本書ではその成果物も**候補ランキングとは別枠の参考評価**として追加する。

- 対象Issue: #41 FND-03
- 計画候補: 14
- 実装完了・採点対象candidate: 13
- 実装未完了candidate: 1
- Final Synthesis: 1（candidateランキング対象外）
- Candidate共通Base: `95a8e50e6b68025e3386fdd0672bd73bcbaa60a0`
- Final Synthesis Base: `7946cc55e49c0c6e21ad7b86c20a8435b4976269`
- 実装品質スコア: 100点満点
- 処理時間: 品質とは分離して評価

---

## Executive Summary（要約）

### Candidate benchmark

13 candidateの主ランキングでは、**GPT-5.6 Sol / Codex が96点で1位**となった。

2位は **GPT-5.6 Terra / Codex 94点**、3位は **GPT-5.6 Luna / Open Code 93点**、4位は **Claude Sonnet 5 / Claude Code 92点**だった。

上位4候補はいずれも重大な問題を残しておらず、Issue #41が要求する実PostgreSQL 18、database isolation、container lifecycle、CI実行などを高い水準で実現している。

### Final Synthesis

13候補の比較結果を利用して作成したFinal Synthesisは、同一の100点基準で**98点**となった。

```text
Branch: agent/issue-41-fnd-03-final-code
PR: #104
Head: 91e3fca181558cd1523390347f4f2f80d6014d26
処理時間: 24
Changed files: 10
Diff: +607 / -9
Primary CI: 31277771209 — SUCCESS
```

Final Synthesisは、候補1位のSol / Codexを主軸にしつつ、他候補から以下を選択的に取り込んだ。

- `CREATE DATABASE ... TEMPLATE template0`
- cleanup failure後もdatabase leaseをretry可能にするlifecycle
- startup primary failureとpartial cleanup failureの集約
- unreachable Docker / PostgreSQL endpointによるfailure injection
- process-global Console testの明示的な直列化
- real PostgreSQL work interval overlapによるconcurrency verification

さらに、assembly parallelizationを有効化した初期HeadでLinux CI上の`ConsoleCapture` raceが実際に露出し、その競合もFinal Headで修正された。

品質面ではcandidate最高96点を上回る一方、処理時間は24、差分は`+607/-9`となり、Sol候補の15 / `+398/-2`より実装コストは増えている。

したがって、Final Synthesisは、

> **候補比較で得た知見を使って品質をもう一段引き上げたが、その分だけ時間とコード量を追加投入した最終版**

と評価する。

Final Synthesisはcurated implementationであり、**14番目のModel + Agent/Harness candidateとしてランキングへ混ぜない**。

### 今回の主要結果

| 観点 | 結果 |
|---|---|
| 🥇 Candidate実装品質1位 | **GPT-5.6 Sol / Codex — 96点** |
| 🥈 Candidate実装品質2位 | **GPT-5.6 Terra / Codex — 94点** |
| 🥉 Candidate実装品質3位 | **GPT-5.6 Luna / Open Code — 93点** |
| Majorなしで最速 | **Qwen3.7 Plus / Open Code — 87点 / 処理時間14** |
| Candidate全体最速 | Grok 4.5 / Cursor、Composer 2.5 / Cursor — 処理時間9 |
| Candidate品質・速度バランス | **GPT-5.6 Sol / Codex — 96点 / 処理時間15** |
| Final Synthesis参考評価 | **98点 / 処理時間24** |
| Final Synthesis品質 / 時間 | **4.08** |

### 実装未完了

**MiniMax M3 / Open Code**

- 結果: `stopped / no-change`
- 実装差分: なし
- 実装品質スコア: N/A
- ランキング: 対象外

---

# 1. Candidate実装品質ランキング

主ランキングは**candidateの実装品質スコア**で決定している。

処理時間は品質スコアには加算せず、別軸として併記した。

| 順位 | モデル | 実行環境 | 実装品質 | 処理時間 | 重大 | 軽微 | 短評 |
|---:|---|---|---:|---:|---:|---:|---|
| **1** | **GPT-5.6 Sol** | **Codex** | **96** | **15** | **0** | **0** | 最も完成度が高い。分離・並列・失敗処理のバランスが良い |
| **2** | **GPT-5.6 Terra** | **Codex** | **94** | **16** | **0** | 1 | shared container方式として堅実。並列検証だけ一段弱い |
| **3** | **GPT-5.6 Luna** | **Open Code** | **93** | **30** | **0** | 1 | 小さい差分で高品質。既存repositoryへの適合性が高い |
| **4** | **Claude Sonnet 5** | **Claude Code** | **92** | **19** | **0** | 1 | 専用test projectで責務が明快。検証も充実 |
| **5** | **Qwen3.7 Plus** | **Open Code** | **87** | **14** | **0** | 2 | 短時間でMajorなし。並列・cleanup検証はやや弱い |
| **6=** | DeepSeek V4 Pro | Open Code | **83** | **20** | 1 | 1 | 基盤は成立するが、parallel要件の検証が不足 |
| **6=** | GPT-5.6 Luna | Codex | **83** | **19** | 1 | 0 | PG基盤は良いが、既存Console testとの並列競合を残した |
| **6=** | Claude Opus 5 | Claude Code | **83** | **32** | 1 | 0 | 検証は非常に強いが、複雑化とfailed-start cleanupに問題 |
| **9** | MiMo-V2.5-Pro | Open Code | **82** | **24** | 1 | 1 | schema分離は成立するが、共通PG基盤としては弱い |
| **10=** | DeepSeek V4 Flash | Open Code | **80** | **64** | 1 | 1 | failure testは良いが、複数testが同一DBを共有 |
| **10=** | Composer 2.5 | Cursor | **80** | **9** | 1 | 1 | 非常に速いが、cleanup失敗後のresource管理に欠陥 |
| **12** | Grok 4.5 | Cursor | **68** | **9** | 2 | 1 | 最速だがcontainer teardownとparallel safetyに問題 |
| **13** | MiMo-V2.5 | Open Code | **55** | **26** | 3 | 1 | lifecycle・isolation・parallelの核心部分に複数問題 |

> **処理時間について:** 単位が明示されていないため、数値をそのまま相対比較に使用している。秒・分などの単位は推定していない。

Final Synthesisはこの表へ追加しない。比較結果を利用した後段のcurated implementationであり、candidateと情報条件が異なるためである。

### 上位グループ — 92〜96点

Sol、Terra、Luna / Open Code、Sonnetの4候補。

Issue #41をClose可能な水準にあり、差は主に**parallel verificationの強さ、変更量、fixture architecture**にある。

### 良好グループ — 87点

Qwen3.7 Plus。

上位4候補ほど検証は強くないがMajorはなく、処理時間14という速さも含めると実務上かなり良好な結果だった。

### Majorあり — 80〜83点

DeepSeek V4 Pro、Luna / Codex、Opus、MiMo-Pro、Flash、Composer。

通常経路は概ね成立しているが、parallel、cleanup、test isolationなどIssue #41の核心部分に修正必須の問題が残る。

### 大幅な修正が必要 — 68点以下

Grok、MiMo-V2.5。

CIが成功していても、FND-03の基盤として安全に採用できない設計上の問題が複数確認された。

---

# 2. カテゴリ別評価

ランキングだけでは各実装の差が分かりにくいため、Issue #41で特に重要だった観点ごとに整理する。

## 2.1 要件達成・実行可能性

**CandidateではSol、Terra、Luna / Open Code、Sonnetが安定して高評価。Final Synthesisはそれらを上回る。**

全候補でPostgreSQL 18・指定digest・Testcontainers.PostgreSql 4.13.0の使用自体は概ね揃っており、単純なpackage選択では大きな差は付かなかった。

差が出たのは、その上に構築するfixture lifecycleやtest isolationだった。

Final SynthesisではPostgreSQL 18.4の`server_version_num=180004`、指定image reference / digest、test単位database、CI上のreal PostgreSQL categoryを同一Headで確認できる。

**寸評:**
「PostgreSQL containerを起動できる」だけではFND-03達成にはならず、**後続Issueが安心して使えるtest基盤になっているか**が順位を分けた。Final Synthesisはこの点で最も完成度が高い。

---

## 2.2 Test isolation・ライフサイクル

**CandidateではSol、Terra、Luna / Open Code、Qwen、Sonnetが良好。Final SynthesisはSol方式を基礎にさらにfailure lifecycleを強化した。**

testごとに独立databaseを確保する方式は、PostgreSQL固有機能を今後追加してもstate干渉を起こしにくい。

一方、DeepSeek V4 FlashとMiMo-V2.5ではtest class単位でdatabaseを共有する構造が残った。

Final Synthesisは、

- xUnit class fixtureがcontainerを所有
- 各test instanceが専用databaseを所有
- `template0`からdatabase作成
- `Pooling=false`
- `DROP DATABASE ... WITH (FORCE)`
- drop成功後だけleaseをdisposedへ遷移

という責任境界を採用している。

**寸評:**
FND-03では「今のtestが通る」よりも、**test追加後も順序依存にならず、失敗後にもresource ownershipを失わないこと**が重要である。Final Synthesisはcandidate比較で見つかった弱点を最も直接的に潰している。

---

## 2.3 並列実行

**CandidateではSolが最も強く、Final Synthesisはその方式を維持しつつrepository-level safetyを実CIで補強した。**

Solは単に複数Taskを開始するだけではなく、PostgreSQL server側の処理時間が実際に重なったことまで確認した。

Final Synthesisも`statement_timestamp()` / `pg_sleep(1)` / `clock_timestamp()`を使ったserver-side interval overlapを確認する。

一方、Final Synthesisはこのtestを**xUnit schedulerそのものの並列証明とは主張していない**。READMEではparallel-safe scopeとserialized scopeを分離し、process-global `Console.Out` / `Console.Error`を使う`ApiRuntimeContractTests`だけを`DisableParallelization=true` collectionへ隔離している。

初期Headではassembly parallelizationによってLinux CI上で`ConsoleCapture.Content`のread/write raceが実際に発生したが、Final Headではsynchronized writerのread / dispose側も同じlockで保護し、PR Head一致CIが成功している。

**寸評:**
今回最も差が出たカテゴリの1つ。**「並列にできる」ことと「repository全体を安全に並列化できる」ことは別問題**であり、Final Synthesisでは候補段階の設計推論だけでなく、実CI failureまで使って境界を詰めた。

---

## 2.4 Cleanup・失敗時処理

**Final Synthesisが最も強いカテゴリ。**

正常終了だけでなく、

- container startup failure
- PostgreSQL connection failure
- database cleanup failure
- startup途中のpartial cleanup failure
- container dispose failure

をどう扱うかで品質差が出た。

MiMo-V2.5はcontainer cleanup exceptionを明示的に握り潰した。

Composerはcleanup失敗後にresourceをdisposed扱いし、再cleanupできずdatabaseを残す可能性があった。

Grokはprocess-global containerの確実なshutdown owner自体が存在しなかった。

Opusは強いfailure verificationを持つ一方、failed-start cleanup failureを一部握り潰し、custom xUnit frameworkまで導入した。

Final Synthesisでは、

- database cleanup成功後だけdisposed
- cleanup failure後も同一leaseでretry可能
- failure test後に最終database削除を確認
- startup primary failureとpartial cleanup failureをAggregateExceptionで保持
- connection failureとconnection dispose failureも可能な限り両方保持
- container dispose失敗時にhandleを保持

としている。

**寸評:**
integration test infrastructureでは、**成功時より失敗時の挙動の方が設計品質を表しやすい**。13候補比較の価値がFinal Synthesisへ最も直接反映された部分である。

---

## 2.5 コードの小ささ・保守性

**CandidateではLuna / Open Codeが最小性で特に優秀。Final Synthesisは品質優先で少し大きくなった。**

Luna / Open Codeは6 files / 約465行の追加で必要機能をまとめていた。

SolやTerraも400行前後で、既存xUnit機構の範囲内に収めた。

対照的にOpusは23 files / 約1,405行とcustom xUnit TestFrameworkまで導入した。

Final Synthesisは10 files / `+607/-9`。

Sol候補より約200行増えたが、増加分は主に、

- retryable cleanup lifecycle
- failure injection
- README policy
- Console parallel safety fix

であり、custom test runnerやprocess-global container frameworkは導入していない。

**寸評:**
Final Synthesisは最小差分ではない。ただしOpus型の過剰設計へは進まず、候補比較で実際に価値が確認できたhardeningに増加分を限定している。

---

## 2.6 処理速度

Candidate最速は、

- Grok 4.5 / Cursor — 9
- Composer 2.5 / Cursor — 9

だった。

ただし両方ともMajorが残った。

Majorなしでは、

1. Qwen3.7 Plus / Open Code — 14
2. GPT-5.6 Sol / Codex — 15
3. GPT-5.6 Terra / Codex — 16

の順。

Final Synthesisは**24**。

**寸評:**
Final Synthesisは品質を98点まで上げた一方、速度では上位candidateに劣る。候補比較・failure hardening・CI race修正まで含めたことによる追加コストが明確に出た。

---

# 3. 採点内訳

実装品質スコアは以下の8カテゴリ、100点満点で評価した。

## 3.1 Candidate

| モデル | 実行環境 | 要件 /25 | 正しさ /15 | 範囲 /15 | 設計 /10 | 検証 /10 | 保守性 /10 | 最小性 /10 | リスク /5 | 合計 |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| **GPT-5.6 Sol** | **Codex** | 25 | 15 | 15 | 9 | 9 | 9 | 9 | 5 | **96** |
| **GPT-5.6 Terra** | **Codex** | 24 | 14 | 15 | 9 | 9 | 9 | 9 | 5 | **94** |
| **GPT-5.6 Luna** | **Open Code** | 23 | 14 | 15 | 9 | 9 | 8 | 10 | 5 | **93** |
| **Claude Sonnet 5** | **Claude Code** | 24 | 14 | 15 | 9 | 9 | 9 | 8 | 4 | **92** |
| Qwen3.7 Plus | Open Code | 22 | 13 | 15 | 8 | 8 | 8 | 9 | 4 | **87** |
| DeepSeek V4 Pro | Open Code | 20 | 13 | 15 | 8 | 6 | 8 | 9 | 4 | **83** |
| GPT-5.6 Luna | Codex | 22 | 12 | 14 | 7 | 8 | 8 | 8 | 4 | **83** |
| Claude Opus 5 | Claude Code | 22 | 13 | 15 | 8 | 10 | 7 | 5 | 3 | **83** |
| MiMo-V2.5-Pro | Open Code | 20 | 13 | 15 | 7 | 7 | 8 | 8 | 4 | **82** |
| DeepSeek V4 Flash | Open Code | 19 | 12 | 15 | 7 | 8 | 7 | 7 | 5 | **80** |
| Composer 2.5 | Cursor | 20 | 12 | 15 | 8 | 7 | 7 | 8 | 3 | **80** |
| Grok 4.5 | Cursor | 18 | 10 | 14 | 5 | 7 | 6 | 6 | 2 | **68** |
| MiMo-V2.5 | Open Code | 13 | 8 | 13 | 4 | 4 | 5 | 7 | 1 | **55** |

## 3.2 Final Synthesis — 参考採点

| 実装 | 要件 /25 | 正しさ /15 | 範囲 /15 | 設計 /10 | 検証 /10 | 保守性 /10 | 最小性 /10 | リスク /5 | 合計 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| **Final Synthesis / GPT-5.6 Sol / Codex** | **25** | **15** | **15** | **10** | **10** | **9** | **9** | **5** | **98** |

### Final Synthesis採点理由

**A. Issue達成度 25/25**
Issue #41の10 ACを実装・test・CIから満たす。PostgreSQL 18.4、digest pin、test単位database、failure reporting、CI real PostgreSQL、scope境界が揃う。

**B. 正しさ・実行可能性 15/15**
Final Head `91e3fca...` に対するPR event CI `31277771209`でRestore / Build / non-PostgreSQL / real PostgreSQLがすべてSUCCESS。

**C. Scope遵守 15/15**
test-only Npgsql / Testcontainersに限定し、DbContext、migration、business schema、Docker Compose等を追加していない。`ConsoleCapture`修正はFND-03で有効化したparallelizationが実際に露出させた回帰への局所修正であり、scope driftとは評価しない。

**D. Repository適合性 10/10**
FND-02由来のprocess-global Console captureを確認し、対象classのみ`DisableParallelization=true` collectionへ隔離した。初回CIで顕在化したraceも同一同期境界へ修正しており、candidate段階よりrepository適合性が高い。

**E. テスト・検証品質 10/10**
lifecycle、isolation、cleanup failure、cleanup retry、real PostgreSQL concurrency、Docker startup failure、PostgreSQL connection failure、CI real PostgreSQLまで検証している。

**F. コード品質・保守性 9/10**
custom runnerやprocess-global fixtureを避け、ownershipは明確。ただしfixture本体302行、合計`+607`と候補上位よりコード量が増えた。

**G. 変更精度・最小性 9/10**
必要なhardeningに限定しているが、Sol候補`+398/-2`より大きい。品質向上とのトレードオフとして1点減点。

**H. エラー・リスク管理 5/5**
cleanup exceptionを握り潰さず、failed cleanup後のretry、failed-start cleanup aggregation、Console raceの回帰修正まで行っている。

### 採点カテゴリの意味

| カテゴリ | 見ているもの |
|---|---|
| 要件達成度 | Issue #41のAcceptance Criteriaを満たしているか |
| 正しさ・実行可能性 | 実際にbuild / test / CIで動作するか |
| Scope遵守 | FND-04以降の責務を先取りしていないか |
| Repository適合性 | 既存構造・test policyと自然に統合されているか |
| テスト・検証品質 | lifecycle・failure・parallelなどを十分検証しているか |
| コード品質・保守性 | 読みやすく単純で保守しやすいか |
| 変更精度・最小性 | 必要なものだけを変更しているか |
| リスク管理 | flaky化、cleanup漏れ、既存test破壊などがないか |

---

# 4. Issue #41 主要要件の達成状況

記号の意味:

- **✓**: 実装・test・CIから成立を確認
- **△**: 基本成立するが検証または設計に不足
- **✗**: 要件未達または修正必須の問題

## 4.1 Candidate

| モデル / 実行環境 | 実PG18 | digest固定 | lifecycle | test分離 | 並列 | cleanup失敗 | 起動・接続失敗 | CI実PG | Scope |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| DeepSeek V4 Pro / Open Code | ✓ | ✓ | ✓ | ✓ | ✗ | △ | ✓ | ✓ | ✓ |
| Qwen3.7 Plus / Open Code | ✓ | ✓ | ✓ | ✓ | △ | △ | ✓ | ✓ | ✓ |
| GPT-5.6 Luna / Open Code | ✓ | ✓ | ✓ | ✓ | △ | ✓ | ✓ | ✓ | ✓ |
| DeepSeek V4 Flash / Open Code | ✓ | ✓ | ✓ | △ | △ | ✓ | ✓ | ✓ | ✓ |
| MiMo-V2.5 / Open Code | ✓ | ✓ | △ | ✗ | ✗ | ✗ | ✓ | ✓ | ✓ |
| MiMo-V2.5-Pro / Open Code | ✓ | ✓ | ✓ | △ | △ | △ | ✓ | ✓ | ✓ |
| GPT-5.6 Luna / Codex | ✓ | ✓ | ✓ | ✓ | △ | ✓ | ✓ | ✓ | ✓ |
| GPT-5.6 Terra / Codex | ✓ | ✓ | ✓ | ✓ | △ | ✓ | ✓ | ✓ | ✓ |
| **GPT-5.6 Sol / Codex** | **✓** | **✓** | **✓** | **✓** | **✓** | **✓** | **✓** | **✓** | **✓** |
| Grok 4.5 / Cursor | ✓ | ✓ | ✗ | ✓ | △ | ✗ | ✓ | ✓ | ✓ |
| Composer 2.5 / Cursor | ✓ | ✓ | ✓ | ✓ | △ | △ | ✓ | ✓ | ✓ |
| Claude Sonnet 5 / Claude Code | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | △ | ✓ | ✓ |
| Claude Opus 5 / Claude Code | ✓ | ✓ | ✓ | ✓ | ✓ | △ | ✓ | ✓ | ✓ |

## 4.2 Final Synthesis

| 実装 | 実PG18 | digest固定 | lifecycle | test分離 | 並列 | cleanup失敗 | 起動・接続失敗 | CI実PG | Scope |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Final Synthesis** | **✓** | **✓** | **✓** | **✓** | **✓** | **✓** | **✓** | **✓** | **✓** |

Final Synthesisでは、候補の単純なAC充足に加えて、cleanup failure後のretryabilityとparallel化による既存test回帰まで実証・修正している。

### この比較から分かること

Candidate順位を大きく分けたのは、

1. **test間でstateを共有しないこと**
2. **parallel policyが実装と一致していること**
3. **cleanup failureを成功扱いしないこと**

の3点だった。

Final Synthesisではこの3点をすべて強化している。

---

# 5. 品質と速度

品質と速度を混同しないため、処理時間は別軸として比較する。

参考値として以下を使用する。

```text
品質 / 時間 = 実装品質スコア ÷ 処理時間
```

## 5.1 Candidate

| モデル | 実行環境 | 実装品質 | 処理時間 | 品質 / 時間 |
|---|---|---:|---:|---:|
| GPT-5.6 Sol | Codex | 96 | 15 | **6.40** |
| GPT-5.6 Terra | Codex | 94 | 16 | **5.88** |
| GPT-5.6 Luna | Open Code | 93 | 30 | 3.10 |
| Claude Sonnet 5 | Claude Code | 92 | 19 | 4.84 |
| Qwen3.7 Plus | Open Code | 87 | 14 | **6.21** |
| DeepSeek V4 Pro | Open Code | 83 | 20 | 4.15 |
| GPT-5.6 Luna | Codex | 83 | 19 | 4.37 |
| Claude Opus 5 | Claude Code | 83 | 32 | 2.59 |
| MiMo-V2.5-Pro | Open Code | 82 | 24 | 3.42 |
| DeepSeek V4 Flash | Open Code | 80 | 64 | 1.25 |
| Composer 2.5 | Cursor | 80 | 9 | **8.89** |
| Grok 4.5 | Cursor | 68 | 9 | **7.56** |
| MiMo-V2.5 | Open Code | 55 | 26 | 2.12 |

## 5.2 Final Synthesis — 参考比較

```text
98 / 24 = 4.08
```

| 実装 | 品質 | 処理時間 | 品質 / 時間 |
|---|---:|---:|---:|
| **Final Synthesis** | **98** | **24** | **4.08** |
| GPT-5.6 Sol / Codex | 96 | 15 | **6.40** |
| GPT-5.6 Terra / Codex | 94 | 16 | 5.88 |
| Claude Sonnet 5 / Claude Code | 92 | 19 | 4.84 |
| GPT-5.6 Luna / Open Code | 93 | 30 | 3.10 |

### 品質

Final Synthesisが参考評価98点で最も高い。

ただしcandidateと同条件の独立試行ではないため、主ランキング1位を置き換えない。

### 速度・効率

Final SynthesisはSolより9大きい処理時間を使用し、品質 / 時間は6.40から4.08へ低下した。

つまり、Final Synthesisで得た+2点は無料ではなく、追加の探索・hardening・CI修正コストを伴っている。

### Candidateで最も品質と速度のバランスが良い

**GPT-5.6 Sol / Codex — 96点 / 処理時間15**

### Majorなしで最も速いCandidate

**Qwen3.7 Plus / Open Code — 87点 / 処理時間14**

### 最速だが注意が必要

**Composer 2.5 / Cursor — 80点 / 処理時間9**
**Grok 4.5 / Cursor — 68点 / 処理時間9**

両方ともMajorがあるため、品質 / 時間だけを最終判断へ使用できない。

---

# 6. 実装方式の比較

候補の設計は大きく4種類に分かれた。

## 6.1 test classごとにcontainer、testごとにdatabase

代表:

- GPT-5.6 Sol / Codex
- GPT-5.6 Luna / Codex
- **Final Synthesis**

### 特徴

- ownershipが分かりやすい
- testごとのdatabase isolationが強い
- xUnit標準fixtureだけでteardownできる
- test class間のparallel executionを構成しやすい

### Candidate評価

**FND-03では最も分かりやすく安全な方式。**

container起動回数は増えるが、基盤コードの単純さとの交換条件として妥当。

### Final Synthesisでの採用

Final Synthesisはこの方式を維持し、shared-container最適化は見送った。

その上で、

- `template0`
- retryable database cleanup
- failure aggregation
- explicit failure injection

を加えた。

---

## 6.2 1 containerを共有し、testごとにdatabase

代表:

- GPT-5.6 Terra / Codex
- GPT-5.6 Luna / Open Code
- Qwen3.7 Plus / Open Code
- Claude Sonnet 5 / Claude Code
- Composer 2.5 / Cursor

### 特徴

- container起動コストが小さい
- database単位の強いisolationを得られる
- migration・lockingなど後続testにも使いやすい

### 評価

**効率とisolationのバランスが良い方式。**

ただしshared containerの最終cleanup ownerと、xUnit collectionによるparallel policyを明確にする必要がある。

Final Synthesisでは性能最適化よりownershipの単純さを優先し、採用しなかった。

---

## 6.3 1 database内でschemaを分離

代表:

- MiMo-V2.5-Pro / Open Code

### 特徴

tableやrowは分離できるが、

- advisory lock
- database setting
- extension
- role
- database-wide object

などは共有される。

### 評価

**通常のCRUD testには使えるが、PostgreSQL固有機能の共通test基盤としてはdatabase isolationより弱い。**

Final Synthesisでも採用していない。

---

## 6.4 process / assembly全体でcontainerを共有

代表:

- Grok 4.5 / Cursor
- Claude Opus 5 / Claude Code

### 特徴

container起動を最小化できる反面、確実な最終teardownが難しくなる。

Grokはdeterministicなshutdown ownerが不足した。

Opusはこれを解決するためcustom xUnit TestFrameworkまで導入した。

### 評価

**FND-03には複雑すぎる。**

Final Synthesisではprocess-global containerもcustom xUnit frameworkも明示的に採用しなかった。

---

# 7. 同一モデルで見る実行環境の差

GPT-5.6 LunaはOpen CodeとCodexの両方で実行している。

| 項目 | Open Code | Codex |
|---|---:|---:|
| 実装品質 | **93** | 83 |
| 処理時間 | 30 | **19** |
| 変更file数 | **6** | 10 |
| 差分 | +465 / -0 | +341 / -3 |
| 重大指摘 | **0** | 1 |
| 軽微指摘 | 1 | **0** |
| DB isolation | test単位 | test単位 |
| 既存Console testとの整合 | **安全** | 競合リスクあり |

### 寸評

Codex版はOpen Code版より速かった。

一方でassembly-level parallelizationを有効化した際に、既存のprocess-global Console captureとの競合を十分に防げなかった。

Open Code版は並列性について保守的だったが、その分repository全体を安全に維持した。

**今回の1試行では、Luna / Open Codeの方が高品質だった。**

これはGPT-5.6 LunaそのものやOpen Code / Codexの一般性能を示す結果ではない。

---

# 8. Candidateで修正必須だった主な問題

軽微な差ではなく、ランキングへ強く影響した問題をまとめる。

| モデル / 実行環境 | 主な問題 |
|---|---|
| GPT-5.6 Sol / Codex | **修正必須問題なし** |
| GPT-5.6 Terra / Codex | 修正必須問題なし |
| GPT-5.6 Luna / Open Code | 修正必須問題なし |
| Claude Sonnet 5 / Claude Code | 修正必須問題なし |
| Qwen3.7 Plus / Open Code | 修正必須問題なし |
| DeepSeek V4 Pro / Open Code | 実際のparallel test schedulingを成立・実証できていない |
| GPT-5.6 Luna / Codex | parallel有効化により既存Console capture testとの競合が可能 |
| Claude Opus 5 / Claude Code | failed-start時のcleanup failureを一部握り潰す |
| MiMo-V2.5-Pro / Open Code | parallel policyとdatabase-wide isolationが将来PG testに弱い |
| DeepSeek V4 Flash / Open Code | 同一test classの複数testがdatabase stateを共有 |
| Composer 2.5 / Cursor | cleanup失敗後に再cleanupできずdatabaseが残り得る |
| Grok 4.5 / Cursor | container teardown ownerなし。Console parallel safetyにも問題 |
| MiMo-V2.5 / Open Code | cleanup例外握り潰し、DB共有、parallel verification不成立 |

### 寸評

今回のMajorは、機能実装そのものよりも**テスト基盤のownershipとfailure path**に集中した。

Final Synthesisでは、これらの既知失敗パターンをそのまま持ち込まないことが品質上昇の主因となった。

---

# 9. Candidate試行から観測できた傾向

以下はモデル一般の性能ではなく、今回の**単一execution attempt**で観測された傾向である。

## Codex

SolとTerraが1位・2位。

高品質なcandidateではrepository全体のparallel policyまで考慮できていた。

一方Luna / Codexは既存Console captureとの競合を残しており、同じHarnessでも結果差は大きい。

**寸評:** 高品質candidateの上限は今回最も高かった。

---

## Open Code

93点から55点まで結果の幅が大きい。

Lunaは非常に小さな変更で高品質。

Qwenも処理時間14でMajorなしという良好な結果だった。

一方MiMo系やDeepSeek Flashではlifecycle・isolationの理解に差が出た。

**寸評:** モデルによるばらつきが大きいが、上位候補は十分強い。

---

## Claude Code

Sonnetは比較的小さい実装で92点。

Opusは非常に強いverificationを実装したが、custom xUnit frameworkまで導入して複雑化した。

**寸評:** 今回は「より多く考えて作る」ことが必ずしも高得点にはつながらなかった。

---

## Cursor

GrokとComposerはいずれも処理時間9で最速。

一方、両方ともcleanup lifecycleにMajorが残った。

**寸評:** 速度は突出したが、failure pathの詰めで品質を落とした。

---

# 10. Final Synthesis 実装評価

## 10.1 Target identity

```text
Branch: agent/issue-41-fnd-03-final-code
PR: #104
Base: 7946cc55e49c0c6e21ad7b86c20a8435b4976269
Head: 91e3fca181558cd1523390347f4f2f80d6014d26
Commits: 2
Changed files: 10
Diff: +607 / -9
処理時間: 24
```

Final Synthesisはbenchmark common baseではなく、candidate比較結果をmainへ取り込んだ後の`7946cc55...`から作成されている。

これはFinal synthesisの役割上正しい。candidate同士の公平比較とは別工程だからである。

## 10.2 CI evidence

Final Head `91e3fca181558cd1523390347f4f2f80d6014d26` に対するGitHub Actions:

```text
Build and Test #167
Run: 31277771209
Status: completed
Conclusion: success
```

Job `build-test` では以下がすべてSUCCESS。

1. Restore
2. Build
3. Test (non-PostgreSQL)
4. Test (real PostgreSQL)

PR HeadとCI target SHAは一致している。

## 10.3 Final architecture

### Container ownership

`PostgreSqlFixtureTests`が`IClassFixture<PostgreSqlContainerFixture>`としてcontainerを所有する。

process-global static containerやcustom assembly executorは使用しない。

### Database ownership

`PostgreSqlDatabaseTestBase`の各test instanceが`InitializeAsync`でdatabase leaseを作成し、`DisposeAsync`で削除する。

xUnitのtest class instance lifecycleと組み合わせることで**one database per test**になる。

### Isolation

- unique GUID database name
- `CREATE DATABASE ... TEMPLATE template0`
- `Pooling=false`
- test database間でprobe tableが不可視であることを確認

### Cleanup

- `DROP DATABASE ... WITH (FORCE)`
- fixture prefix以外のdatabase dropを拒否
- drop成功後だけ`disposed = true`
- cleanup gateで同一leaseの重複cleanupを直列化
- failed cleanup後もretry可能

### Container failure handling

startup / connection確認失敗時はcandidate containerをDisposeし、Dispose側も失敗した場合はprimary failureとcleanup failureをAggregateExceptionへ保持する。

通常Dispose失敗時もcontainer fieldをclearしないため、ownershipを失わない。

### Parallel policy

assembly parallelizationは有効。

ただしprocess-global Consoleを置換する`ApiRuntimeContractTests`は`DisableParallelization=true` collectionへ配置する。

PostgreSQL concurrencyは別databaseのserver-side work interval overlapで検証する。

READMEは、このtestをxUnit scheduler自体の証明とは主張しない。

## 10.4 Initial CI failureの意味

Final Synthesisでは初回push CIで、assembly parallelization有効化による`ConsoleCapture.Content`のraceが実際に露出した。

これは単なる実装失敗ではなく、candidate比較で懸念されていた**repository-wide parallel safetyが実環境で現実の問題になることを確認した証拠**でもある。

Final Headでは、

- Console-sensitive collection隔離
- synchronized writerのFlush / `ToString()` readを同じlockで保護
- Disposeも同じlockで保護

へ修正した。

その後のFinal Head CI `31277771209`は成功している。

評価上は、初回失敗そのものを減点するのではなく、**failureを検出し、root causeを局所修正し、exact final Headで再検証したこと**を正しさ・Repository適合性の加点材料とした。

## 10.5 Final Synthesis findings

この実装評価では、Issue #41 merge前に修正必須と判断するBlocker / Majorは確認しなかった。

```text
Blocker: 0
Major:   0
Minor:   0
```

ただし、これはFormal Agent B reviewの代替ではない。

### Trade-off — Findingではない

Final SynthesisはSol候補よりコード量が増えている。

```text
Sol candidate:     10 files / +398 / -2 / time 15 / score 96
Final Synthesis:   10 files / +607 / -9 / time 24 / score 98
```

約200行と処理時間9を追加し、failure safetyとrepository integrationを強化した。

この増加はIssue #41内で説明可能でありMajor / Minorにはしないが、最小性では満点にしなかった。

---

# 11. CandidateからFinal Synthesisへ何が改善されたか

## 11.1 GPT-5.6 Sol / Codexから維持したもの

- test class単位container ownership
- test単位database
- `Pooling=false`
- forced database cleanup
- exact PostgreSQL version確認
- digest reference確認
- real PostgreSQL work interval overlap
- Console-sensitive test隔離
- CI category分離

Solがcandidate 1位だった理由をそのまま基礎にしている。

## 11.2 GPT-5.6 Terra / Codexから補ったもの

- repository-level parallel / serialization境界の明文化
- process-global state用collectionを明確にする考え方

## 11.3 GPT-5.6 Luna / Open Codeから補ったもの

- primary failureとcleanup failureを可能な限り両方残すfailure aggregation

## 11.4 Claude Sonnet 5 / Claude Codeから補ったもの

- failure test後にも最終resource回収を確認する考え方
- infrastructure test責務の明示

## 11.5 Claude Opus 5 / Claude Codeから補ったもの

- unreachable Docker endpoint failure injection
- unreachable PostgreSQL endpoint failure injection
- `CREATE DATABASE ... TEMPLATE template0`

一方で、Opus候補のcustom `XunitTestFramework` / assembly executorは採用していない。

## 11.6 Candidate失敗パターンを明示的に排除

Final Synthesisは以下を避けた。

- cleanup exceptionの握り潰し
- cleanup失敗前のdisposed遷移
- test class単位database state共有
- deterministic teardownのないstatic container
- schema isolationの標準採用
- process-global stateを無視したparallel化
- custom xUnit frameworkによる過剰設計
- repository全体の単純serialization
- FND-04 scope先取り

---

# 12. Final SynthesisとCandidate上位の比較

| 実装 | 品質 | 時間 | Q/T | Files | Diff | 主な特徴 |
|---|---:|---:|---:|---:|---:|---|
| **Final Synthesis** | **98** | 24 | 4.08 | 10 | `+607/-9` | candidate知見を統合しfailure safetyを強化 |
| GPT-5.6 Sol / Codex | 96 | 15 | **6.40** | 10 | `+398/-2` | 単独candidateとして最良バランス |
| GPT-5.6 Terra / Codex | 94 | 16 | 5.88 | 9 | `+369/-5` | shared container方式として堅実 |
| GPT-5.6 Luna / Open Code | 93 | 30 | 3.10 | 6 | `+465/-0` | 最小性とrepository安全性が強い |
| Claude Sonnet 5 / Claude Code | 92 | 19 | 4.84 | 13 | `+414/-1` | dedicated infrastructure testが明快 |

### 品質差

Final SynthesisはSol候補より+2点。

改善は主に、

- cleanup retryability
- failed-start cleanup aggregation
- explicit Docker / PostgreSQL failure injection
- `template0`
- 実CIで確認したConsole race修正

から来ている。

### 速度差

Final SynthesisはSol候補より処理時間+9。

単独実装candidateの速さではSolが明確に優れる。

### コード量

Final SynthesisはSol候補より約200行大きい。

そのため、Final Synthesisは「より優れた単独coding attempt」ではなく、**候補比較後に追加コストを使って欠点を潰したsynthesis**として読む必要がある。

---

# 13. Final Synthesis後に必要な工程

Final Synthesisの実装評価は完了したが、Issue #41のClose条件はまだ満たし切っていない。

Issue #41はEvidence required for Closeとして、

- merge済みPR
- CI run / 結果
- fixture使用方法
- isolation / cleanup test結果
- Agent B独立レビュー結果
- Blocker / Major 0

を要求している。

現在PR #104はDraft / Openである。

したがって次工程は、

1. Final Headを固定
2. 複数Model + Agent/Harnessによる独立review benchmarkを実施
3. raw Markdown + structured JSONをmodel別artifactとして保存
4. Formal Agent B reviewを別途実施
5. Blocker / Major 0確認
6. Koo merge判断
7. PR #104 merge
8. Issue #41 Close evidence整理

となる。

Final Synthesis実装評価98点は、Formal Agent B approvalやmerge authorizationの代替ではない。

---

# 付録A. Candidate評価対象・CI証跡

ここはランキングを読むための本文ではなく、**評価対象を取り違えていないことを確認するための再現用情報**として置く。

| モデル | 実行環境 | PR | 評価Head | CI Run | SHA一致 | 処理時間 |
|---|---|---:|---|---:|:---:|---:|
| DeepSeek V4 Pro | Open Code | #90 | `a4eb670` | `31247879865` | ✓ | 20 |
| Qwen3.7 Plus | Open Code | #95 | `cc62c1a` | `31249149739` | ✓ | 14 |
| GPT-5.6 Luna | Open Code | #100 | `cab3b4d` | `31253186214` | ✓ | 30 |
| DeepSeek V4 Flash | Open Code | #102 | `8815445` | `31254568899` | ✓ | 64 |
| MiMo-V2.5 | Open Code | #99 | `95de194` | `31250976866` | ✓ | 26 |
| MiMo-V2.5-Pro | Open Code | #101 | `18faff4` | `31254225390` | ✓ | 24 |
| GPT-5.6 Luna | Codex | #96 | `65aa774` | `31249779697` | ✓ | 19 |
| GPT-5.6 Terra | Codex | #98 | `6df0ab3` | `31250427729` | ✓ | 16 |
| GPT-5.6 Sol | Codex | #93 | `bbf1109` | `31248876009` | ✓ | 15 |
| Grok 4.5 | Cursor | #88 | `34c2a5a` | `31247435667` | ✓ | 9 |
| Composer 2.5 | Cursor | #89 | `0322dd0` | `31247820587` | ✓ | 9 |
| Claude Sonnet 5 | Claude Code | #94 | `917db64` | `31249142978` | ✓ | 19 |
| Claude Opus 5 | Claude Code | #97 | `aec5845` | `31250284186` | ✓ | 32 |

Candidate共通Base:

```text
95a8e50e6b68025e3386fdd0672bd73bcbaa60a0
```

全13候補について、candidate branch、PR Head、benchmark common base、評価対象Head、GitHub Actions実行対象SHAの対応を確認している。

---

# 付録B. Final Synthesis証跡

| 項目 | 値 |
|---|---|
| Branch | `agent/issue-41-fnd-03-final-code` |
| PR | #104 |
| Base | `7946cc55e49c0c6e21ad7b86c20a8435b4976269` |
| Head | `91e3fca181558cd1523390347f4f2f80d6014d26` |
| Commits | 2 |
| Changed files | 10 |
| Diff | `+607 / -9` |
| 処理時間 | 24 |
| Primary CI | `31277771209` |
| CI result | SUCCESS |
| CI Head一致 | ✓ |

Primary CI jobで、Restore / Build / Test (non-PostgreSQL) / Test (real PostgreSQL)がすべて成功している。

---

# 付録C. 評価基準

| 評価項目 | 配点 |
|---|---:|
| Issue達成度 | 25 |
| 正しさ・実行可能性 | 15 |
| Scope遵守・指示追従 | 15 |
| 設計・Repository適合性 | 10 |
| テスト・検証品質 | 10 |
| コード品質・保守性 | 10 |
| 変更精度・最小性 | 10 |
| エラー・リスク管理 | 5 |
| **合計** | **100** |

処理時間は実装品質スコアには含めない。

---

# 付録D. この評価の読み方

Candidate benchmarkから直接言えるのは、

> **Issue #41を同一条件で1回実装させた際、どのModel + Agent/Harnessがどのような成果物を生成したか**

である。

したがって、

- GPT-5.6 Solというモデルが常に1位
- Codexが常にOpen Codeより優秀
- Cursorは品質が低い
- Claude Opusは常に過剰設計

といった一般化は行わない。

モデル能力だけでなく、

```text
Model
+
Agent / Harness
+
Effort設定
+
Repository context
+
1回のagent execution
```

が最終成果物へ影響している。

Final Synthesisはさらに、

```text
13 candidateの比較結果
+
採用・不採用判断
+
追加hardening
+
CI feedback
```

を利用している。

そのため98点をcandidateランキングへ加えて「GPT-5.6 Sol / Codexが98点だった」と扱うことはしない。

今回の結果から得られる追加知見は、

> **複数candidateを比較して失敗パターンを明示した後にcurated synthesisを行うと、単独candidate最高96点から98点へ品質を上げられた。一方で処理時間15→24、差分+398→+607と追加コストも発生した。**

という、Final synthesis工程そのものの効果とコストである。
