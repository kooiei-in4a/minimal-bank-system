# Issue #41 FND-03 — AIコーディングモデル実装比較・最終評価

Issue #41 `[FND-03] 実PostgreSQL integration test基盤を確立する` を、複数の **Model + Agent/Harness** に同一条件で独立実装させ、実際のGitHub成果物を基に比較評価した結果をまとめる。

評価対象はモデル単体ではなく、**「モデル + 実行環境による1回の実装試行」**である。

モデルの一般的な評判や公開ベンチマークではなく、各candidateの実コード、テスト、CI、Issue適合性を一次証拠として採点した。

- 対象Issue: #41 FND-03
- 計画候補: 14
- 実装完了・採点対象: 13
- 実装未完了: 1
- 共通Base: `95a8e50e6b68025e3386fdd0672bd73bcbaa60a0`
- 実装品質スコア: 100点満点
- 処理時間: 品質とは分離して評価

---

## Executive Summary（要約）

今回の実装比較では、**GPT-5.6 Sol / Codex が96点で1位**となった。

2位は **GPT-5.6 Terra / Codex 94点**、3位は **GPT-5.6 Luna / Open Code 93点**、4位は **Claude Sonnet 5 / Claude Code 92点**だった。

上位4候補はいずれも重大な問題を残しておらず、Issue #41が要求する実PostgreSQL 18、database isolation、container lifecycle、CI実行などを高い水準で実現している。

その中でもGPT-5.6 Sol / Codexは、

- testごとのdatabase分離
- 明確なcontainer ownership
- cleanup failureの伝播
- 既存testとのparallel safety
- 実際のPostgreSQL並列実行の検証
- 過剰なframework追加を避けた変更量

のバランスが最も良かった。

### 今回の主要結果

| 観点 | 結果 |
|---|---|
| 🥇 実装品質1位 | **GPT-5.6 Sol / Codex — 96点** |
| 🥈 実装品質2位 | **GPT-5.6 Terra / Codex — 94点** |
| 🥉 実装品質3位 | **GPT-5.6 Luna / Open Code — 93点** |
| Majorなしで最速 | **Qwen3.7 Plus / Open Code — 87点 / 処理時間14** |
| 全体最速 | Grok 4.5 / Cursor、Composer 2.5 / Cursor — 処理時間9 |
| 品質と速度の総合バランス | **GPT-5.6 Sol / Codex — 96点 / 処理時間15** |
| 最終統合の主参考 | **GPT-5.6 Sol / Codex** |

### 実装未完了

**MiniMax M3 / Open Code**

- 結果: `stopped / no-change`
- 実装差分: なし
- 実装品質スコア: N/A
- ランキング: 対象外

---

# 1. 実装品質ランキング

主ランキングは**実装品質スコア**で決定している。

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

### 上位グループ — 92〜96点

Sol、Terra、Luna / Open Code、Sonnetの4候補。

Issue #41を実際にCloseできる水準にあり、差は主に**parallel verificationの強さ、変更量、fixture architecture**にある。

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

ランキングだけでは各候補の差が分かりにくいため、Issue #41で特に重要だった観点ごとに整理する。

## 2.1 要件達成・実行可能性

**Sol、Terra、Luna / Open Code、Sonnetが安定して高評価。**

全候補でPostgreSQL 18・指定digest・Testcontainers.PostgreSql 4.13.0の使用自体は概ね揃っており、単純なpackage選択では大きな差は付かなかった。

差が出たのは、その上に構築するfixture lifecycleやtest isolationだった。

**寸評:**  
「PostgreSQL containerを起動できる」だけではFND-03達成にはならず、**後続Issueが安心して使えるtest基盤になっているか**が順位を分けた。

---

## 2.2 Test isolation・ライフサイクル

**Sol、Terra、Luna / Open Code、Qwen、Sonnetが良好。**

testごとに独立databaseを確保する方式は、PostgreSQL固有機能を今後追加してもstate干渉を起こしにくい。

一方、

- DeepSeek V4 Flash
- MiMo-V2.5

ではtest class単位でdatabaseを共有する構造が残った。

**寸評:**  
FND-03では「今のtestが通る」よりも、**test追加後も順序依存にならないこと**が重要である。database単位のtest isolationが最も分かりやすく安全だった。

---

## 2.3 並列実行

**Solが最も強い。**

Solは単に複数Taskを開始するだけではなく、PostgreSQL server側の処理時間が実際に重なったことまで確認した。

Terra、Luna / Open Code、Qwenなどはdatabaseを同時操作できることは確認しているが、xUnit test自体のparallel schedulingまでは証明していない。

Luna / CodexとGrokは逆にparallelを有効化したことで、既存のprocess-global Console capture testとの競合リスクを作った。

**寸評:**  
今回最も差が出たカテゴリの1つ。**「並列にできる」ことと「repository全体を安全に並列化できる」ことは別問題**だった。

---

## 2.4 Cleanup・失敗時処理

**Sol、Terra、Luna / Open Codeが特に堅実。**

正常終了だけでなく、

- container startup failure
- connection failure
- database cleanup failure
- container stop / dispose failure

をどう扱うかで品質差が出た。

MiMo-V2.5はcontainer cleanup exceptionを明示的に握り潰しており、大きな減点となった。

Composerはcleanup失敗後にresourceを「disposed」と扱ってしまい、再cleanupできずdatabaseを残す可能性がある。

Grokはprocess-global containerの確実なshutdown owner自体が存在しない。

**寸評:**  
integration test infrastructureでは、**成功時より失敗時の挙動の方が設計品質を表しやすい**。今回のランキングにも大きく影響した。

---

## 2.5 コードの小ささ・保守性

**Luna / Open Codeが特に優秀。**

6 files / 約465行の追加で必要機能をまとめており、Issue #41に対する変更精度が高かった。

SolやTerraも400行前後で、既存xUnit機構の範囲内に収めている。

対照的にOpusは、

- 23 files
- 約1,405行
- custom xUnit TestFramework

まで導入した。

検証能力自体は非常に高いが、FND-03だけのためにtest execution frameworkまで拡張する必要性は低いと判断した。

**寸評:**  
AIコーディングでは「多く書けること」より、**必要な保証を少ない仕組みで実現できること**を高く評価した。

---

## 2.6 処理速度

最速は、

- Grok 4.5 / Cursor — 9
- Composer 2.5 / Cursor — 9

だった。

ただし両方ともMajorが残った。

Majorなしでは、

1. Qwen3.7 Plus / Open Code — 14
2. GPT-5.6 Sol / Codex — 15
3. GPT-5.6 Terra / Codex — 16

の順。

**寸評:**  
単純な速度ではCursor勢が強かったが、**品質を維持した速度ではQwen・Sol・Terraが優位**だった。

---

# 3. 採点内訳

実装品質スコアは以下の8カテゴリ、100点満点で評価した。

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

**カテゴリ全体の傾向:**  
上位候補は単純なAC達成だけでなく、**既存repositoryとの整合性とfailure pathまで含めて高得点**だった。下位候補はCI自体は成功していても、lifecycleやstate isolationといった基盤品質で差が付いた。

---

# 4. Issue #41 主要要件の達成状況

記号の意味:

- **✓**: 実装・test・CIから成立を確認
- **△**: 基本成立するが検証または設計に不足
- **✗**: 要件未達または修正必須の問題

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

### この表から分かること

単純なPostgreSQL起動やCI成功では候補間の差は小さい。

順位を大きく分けたのは、

1. **test間でstateを共有しないこと**
2. **parallel policyが実装と一致していること**
3. **cleanup failureを成功扱いしないこと**

の3点だった。

---

# 5. 品質と速度

品質と速度を混同しないため、処理時間は別軸として比較する。

参考値として以下を使用する。

```text
品質 / 時間 = 実装品質スコア ÷ 処理時間
```

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

### 最も品質が高い

**GPT-5.6 Sol / Codex — 96点 / 処理時間15**

修正必須の問題がなく、今回の主ランキング1位。

### 品質と速度のバランスが最も良い

**GPT-5.6 Sol / Codex**

Qwenより処理時間は1大きいが、品質差は9点あり、総合的にはSolが最も良い。

### Majorなしで最も速い

**Qwen3.7 Plus / Open Code — 87点 / 処理時間14**

軽微な検証不足はあるものの、短時間で実用水準に到達した。

### 最速だが注意が必要

**Composer 2.5 / Cursor — 80点 / 処理時間9**

品質 / 時間の単純比では最高だが、cleanup lifecycleにMajorがある。

**Grok 4.5 / Cursor — 68点 / 処理時間9**

同じく非常に速いが、container ownershipなど基盤として重要な問題が残った。

**このため、品質 / 時間だけを最終順位には使用しない。**

---

# 6. 実装方式の比較

候補の設計は大きく4種類に分かれた。

## 6.1 test classごとにcontainer、testごとにdatabase

代表:

- GPT-5.6 Sol / Codex
- GPT-5.6 Luna / Codex

### 特徴

- ownershipが分かりやすい
- testごとのdatabase isolationが強い
- xUnit標準fixtureだけでteardownできる
- test class間のparallel executionを構成しやすい

### 評価

**今回のFND-03では最も分かりやすく安全な方式。**

container起動回数は増えるが、基盤コードの単純さとの交換条件として妥当。

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

shared方式を採るならTerraの設計が最も参考になる。

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

FND-03の標準方式にはしない方がよい。

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

Opusの保証レベルは高いが、23 files・約1,405行とcustom test frameworkを必要とするため、Issue #41に対する変更としては過剰。

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

# 8. 修正必須だった主な問題

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

これはFND-03がapplication featureではなく、今後多数のintegration testが依存するfoundation Issueであることによる。

---

# 9. 今回の試行から観測できた傾向

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

# 10. 最終統合への推奨

この評価では実装・mergeは行わない。

Final synthesisを実施する際の**主参考はGPT-5.6 Sol / Codex**とする。

## 基本として採用する設計

- PostgreSQL 18.4
- 指定image digest固定
- Testcontainers.PostgreSql 4.13.0
- testごとのdatabase isolation
- connection pooling無効
- databaseの確実なcleanup
- cleanup failureの明示
- startup / connection failureの明示
- Console-sensitive testのみ直列化
- PostgreSQL testの実parallel verification
- CIでPostgreSQL integration testを明示実行

## 他候補から参考にする要素

### Claude Opus 5

参考価値があるもの:

- unreachable Docker endpointによるfailure injection
- unreachable PostgreSQL server test
- `CREATE DATABASE ... TEMPLATE template0`

採用しないもの:

- custom xUnit TestFramework
- assembly全体を管理する独自executor

### GPT-5.6 Terra

shared containerへ将来変更する場合、

- collection ownership
- per-test database lease
- Console-sensitive collection isolation

が参考になる。

### GPT-5.6 Luna / Open Code

container stop / disposeの複数failureをまとめて報告する考え方は参考価値がある。

---

## 採用しない設計

- process-global static container
- custom xUnit execution framework
- schemaを標準isolation単位とする設計
- test class単位でdatabaseを共有する設計
- cleanup failureを握り潰す処理
- repository全体を単純にserial化する回避策
- workflowの文字列表現などに強く依存するtest
- FND-03に不要な大量のhelper / abstraction

---

## FND-03のScope境界

Final synthesisでも以下は実装しない。

```text
application DbContext
application-side Npgsql configuration
EF Core migration
migration machinery
business schema
business table
feature integration test
Docker Compose runtime
production row lock
production advisory lock
Customer / Account / money implementation
authentication / authorization
health endpoint
```

FND-03の責務は、

> **後続Issueが実PostgreSQLを、安全・独立・再現可能にテストできる共通基盤を確立すること**

までとする。

---

# 付録A. 評価対象・CI証跡

ここはランキングを読むための本文ではなく、**評価対象を取り違えていないことを確認するための再現用情報**として末尾に置く。

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

共通Base:

```text
95a8e50e6b68025e3386fdd0672bd73bcbaa60a0
```

全13候補について、

- candidate branch
- PR Head
- benchmark common base
- 評価対象Head
- GitHub Actions実行対象SHA

の対応を確認している。

---

# 付録B. 評価基準

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

# 付録C. この評価の読み方

この結果から直接言えるのは、

> **Issue #41をこの条件で1回実装させた際、どのModel + Agent/Harnessがどのような成果物を生成したか**

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

本benchmarkは、それらを含めた**実際のAIコーディング実行能力**を比較するための記録として扱う。
