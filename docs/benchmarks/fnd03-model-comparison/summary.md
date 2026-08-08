# Issue #41 FND-03 — AIコーディングモデル実装比較・要約

Issue #41 `[FND-03] 実PostgreSQL integration test基盤を確立する` を、複数の **Model + Agent/Harness** に同一条件で独立実装させ、GitHub上の実コード・テスト・CIを基に比較評価した。

評価対象はモデル単体ではなく、**Model + Agent/Harness の1回の実装試行**である。

- 計画候補: 14
- 実装完了・採点対象: 13
- 実装未完了: 1
- 実装品質: 100点満点
- 処理時間: 品質とは別評価

---

## 総合結果

**1位は GPT-5.6 Sol / Codex の96点。**

2位 GPT-5.6 Terra / Codex、3位 GPT-5.6 Luna / Open Code、4位 Claude Sonnet 5 / Claude Codeまでが92点以上となり、重大な問題を残さずIssue #41を高い水準で達成した。

| 順位 | モデル | 実行環境 | 実装品質 | 処理時間 | 重大 | 短評 |
|---:|---|---|---:|---:|---:|---|
| **1** | **GPT-5.6 Sol** | **Codex** | **96** | **15** | **0** | 分離・並列・失敗処理のバランスが最も良い |
| **2** | **GPT-5.6 Terra** | **Codex** | **94** | **16** | **0** | shared container方式として堅実 |
| **3** | **GPT-5.6 Luna** | **Open Code** | **93** | **30** | **0** | 小さい差分で高品質 |
| **4** | **Claude Sonnet 5** | **Claude Code** | **92** | **19** | **0** | 責務分離が明快で検証も充実 |
| **5** | **Qwen3.7 Plus** | **Open Code** | **87** | **14** | **0** | Majorなしでは最速 |
| 6= | DeepSeek V4 Pro | Open Code | 83 | 20 | 1 | parallel要件の検証不足 |
| 6= | GPT-5.6 Luna | Codex | 83 | 19 | 1 | 既存Console testとの並列競合 |
| 6= | Claude Opus 5 | Claude Code | 83 | 32 | 1 | 検証は強いが複雑すぎる |
| 9 | MiMo-V2.5-Pro | Open Code | 82 | 24 | 1 | schema分離が共通基盤として弱い |
| 10= | DeepSeek V4 Flash | Open Code | 80 | 64 | 1 | test間でDB stateを共有 |
| 10= | Composer 2.5 | Cursor | 80 | 9 | 1 | 高速だがcleanup管理に問題 |
| 12 | Grok 4.5 | Cursor | 68 | 9 | 2 | teardownとparallel safetyに問題 |
| 13 | MiMo-V2.5 | Open Code | 55 | 26 | 3 | lifecycle・分離・並列に複数問題 |

> **処理時間について:** 単位が明示されていないため、数値をそのまま相対比較に使用している。秒・分などの単位は推定していない。

MiniMax M3 / Open Code は `stopped / no-change` のため採点・ランキング対象外。

---

## 今回の評価で差が付いたポイント

全候補でPostgreSQL 18や指定image digestの利用自体には大きな差がなかった。

順位を分けたのは主に次の3点。

### 1. Test isolation

上位候補は**testごとに独立database**を使用し、test追加や実行順序によってstateが干渉しにくい構成だった。

一方、DeepSeek V4 FlashやMiMo-V2.5ではtest class内でdatabaseを共有する問題が残った。

### 2. 並列実行

GPT-5.6 Sol / Codexは、単に並列Taskを作るだけでなく、**PostgreSQL上で実際に処理が重なっていることまで検証**した。

一方、並列化を有効にしたことで既存のConsole capture testと競合する候補もあった。

### 3. Cleanup・失敗時処理

integration test基盤では正常系だけでなく、

- container startup failure
- connection failure
- database cleanup failure
- container stop / dispose failure

を正しくtest failureとして扱えるかが重要だった。

MiMo-V2.5の例外握り潰しや、Composerのcleanup失敗後のresource管理などが大きな減点要因となった。

---

## 品質と速度

最速は、

- Grok 4.5 / Cursor — 9
- Composer 2.5 / Cursor — 9

だったが、両方ともMajorが残った。

**Majorなしで最速**は、

**Qwen3.7 Plus / Open Code — 87点 / 処理時間14**

だった。

ただし品質差まで含めると、

**GPT-5.6 Sol / Codex — 96点 / 処理時間15**

が今回最もバランスの良い結果だった。

---

## 実装方式の傾向

### test classごとにcontainer + testごとにdatabase

代表: GPT-5.6 Sol / Codex

ownershipが明確で、並列化と確実なteardownを両立しやすい。

**今回最も高評価。**

### shared container + testごとにdatabase

代表: GPT-5.6 Terra / Codex、GPT-5.6 Luna / Open Code

container起動コストを抑えながら強いisolationを確保できる。

**効率と安全性のバランスが良い。**

### schema isolation

代表: MiMo-V2.5-Pro / Open Code

通常のtable testには使えるが、database-wideなPostgreSQL固有機能を扱う共通基盤としては弱い。

### process全体でcontainer共有

代表: Grok 4.5 / Cursor、Claude Opus 5 / Claude Code

効率化できる一方、teardown ownershipが難しくなる。

Opusはcustom xUnit frameworkまで導入して解決を試みたが、FND-03としては過剰だった。

---

## 同一モデル・異なる実行環境

GPT-5.6 Lunaでは、

| 実行環境 | 実装品質 | 処理時間 |
|---|---:|---:|
| **Open Code** | **93** | 30 |
| Codex | 83 | **19** |

Codexの方が速かったが、既存Console testとのparallel競合を残した。

今回の1試行では、**Open Code版の方が高品質**だった。

これはモデルやHarnessの一般性能を意味するものではない。

---

## 最終統合への推奨

Final synthesisの主参考は、

**GPT-5.6 Sol / Codex**

とする。

維持したい要素は次の通り。

- PostgreSQL 18.4
- 指定digest固定
- Testcontainers.PostgreSql 4.13.0
- testごとのdatabase isolation
- connection pooling無効
- 確実なdatabase cleanup
- cleanup / startup / connection failureの明示
- Console-sensitive testのみ直列化
- 実際のparallel execution検証
- CIでPostgreSQL integration testを明示実行

一方、

- process-global static container
- custom xUnit framework
- test class単位のdatabase共有
- cleanup failureの握り潰し
- schema isolationの標準採用
- repository全体の単純な直列化

は採用しない。

---

## 結論

今回のFND-03では、単にPostgreSQL testを動かせることより、

**「後続Issueが安全に使い続けられるtest infrastructureになっているか」**

が評価を分けた。

今回の1実行比較では、

1. **GPT-5.6 Sol / Codex**
2. **GPT-5.6 Terra / Codex**
3. **GPT-5.6 Luna / Open Code**
4. **Claude Sonnet 5 / Claude Code**

が上位となった。

特にGPT-5.6 Sol / Codexは、品質・速度・変更規模のバランスが最も良く、Final synthesisの主参考として最適と判断した。

詳細な採点根拠、Acceptance Criteria判定、設計比較、CI証跡は [`implementation-evaluation.md`](./implementation-evaluation.md) を参照する。
