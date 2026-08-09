# Issue #41 FND-03 — AIコーディングモデル実装比較・要約

STATUS: **SUPERSEDED / HISTORICAL**

This evaluation was produced before the subsequent independent-review benchmark identified the Testcontainers container-cleanup Major.

Do not use the 98/100 provisional score as the final merge-readiness verdict.

日本語注記: 本書の`98 / 100`は当時の評価結果として保存するが、現在の最終merge-readiness verdictではない。

Issue #41 `[FND-03] 実PostgreSQL integration test基盤を確立する` を、複数の **Model + Agent/Harness** に同一条件で独立実装させ、GitHub上の実コード・テスト・CIを基に比較評価した。

その後、13候補の比較結果を材料に `agent/issue-41-fnd-03-final-code` でFinal Synthesisを実装したため、本書ではその結果も**候補ランキングとは別枠の参考評価**として追加する。

- 計画候補: 14
- 実装完了・採点対象: 13
- 実装未完了: 1
- Final Synthesis: 1（ランキング対象外）
- 実装品質: 100点満点
- 処理時間: 品質とは別評価

---

## 総合結果

### Candidate benchmark

**候補1位は GPT-5.6 Sol / Codex の96点。**

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

## Final Synthesis の評価

Final Synthesisは複数candidateの比較結果を見た後に作成したcurated implementationであり、**14番目のcandidateとしてランキングへ混ぜない**。

同じ100点基準で参考採点すると、結果は次の通り。

| 項目 | Final Synthesis |
|---|---|
| Branch | `agent/issue-41-fnd-03-final-code` |
| PR | #104 |
| Head | `91e3fca181558cd1523390347f4f2f80d6014d26` |
| 実装品質 | **98 / 100** |
| 処理時間 | **24** |
| 品質 / 時間 | **4.08** |
| 変更量 | 10 files / `+607 / -9` |
| Blocker / Major | **0 / 0** |
| Primary CI | `31277771209` — SUCCESS |

### 採点内訳

| 要件 /25 | 正しさ /15 | 範囲 /15 | 設計 /10 | 検証 /10 | 保守性 /10 | 最小性 /10 | リスク /5 | 合計 |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 25 | 15 | 15 | 10 | 10 | 9 | 9 | 5 | **98** |

**候補版の最高96点を品質面では上回った。**

ただしFinal Synthesisは、候補の良い点と失敗パターンを事前に知った状態で作られているため、これはモデル能力ランキングではない。

---

## Final Synthesisで改善された点

主軸は1位のGPT-5.6 Sol / Codexだが、そのままコピーせず、他候補から有効な要素を選択した。

### 1. Cleanup failure後もresourceを回収できる

Composer候補ではcleanup失敗後にresourceがdisposed扱いになり、再cleanupできない問題があった。

Final Synthesisではdatabase leaseを**DROP成功後だけdisposedへ遷移**させる。

さらにcleanup failure testで、

1. failureを明示的に発生
2. databaseが残っていることを確認
3. 同じleaseでcleanupをretry
4. 最終的にdatabaseが消えたことを確認

まで固定した。

### 2. Startup failureとcleanup failureを両方保持

GPT-5.6 Luna / Open Codeなどのfailure reportingを取り込み、container startup失敗後のpartial cleanupも考慮した。

startup側の例外だけを残してcleanup failureを失う構造にはしていない。

### 3. Database isolationをさらに明確化

Sol候補のtest単位databaseを維持し、Claude Opus候補の `CREATE DATABASE ... TEMPLATE template0` を採用した。

- one database per test
- 一意database名
- `Pooling=false`
- `template0`
- `DROP DATABASE ... WITH (FORCE)`

という境界が明確になった。

### 4. Failure injectionを強化

Final Synthesisでは、通常の成功経路だけでなく、

- unreachable Docker endpoint
- unreachable PostgreSQL endpoint
- database cleanup failure
- cleanup retry

を局所的に検証する。

DockerやPostgreSQLが利用できない場合にskipやfallbackへ逃げないことも維持した。

### 5. Parallel化の副作用を実CIで検出・修正

assembly parallelizationを有効化した初期Headでは、既存 `ConsoleCapture.Content` のread/write raceがLinux CIで実際に発生した。

Final HeadではConsole-sensitive testを`DisableParallelization=true` collectionへ隔離した上で、synchronized writerのread / dispose側も同じlockで保護した。

この点は、Luna / Codex候補で検出された「parallel化すると既存Console testを壊し得る」という問題を、Final Synthesisで実際に踏み、CI証拠に基づいて修正したものでもある。

---

## Candidate上位との比較

| 実装 | 品質 | 処理時間 | 品質 / 時間 | Files | 差分 | Major |
|---|---:|---:|---:|---:|---:|---:|
| **Final Synthesis** | **98** | 24 | 4.08 | 10 | `+607/-9` | **0** |
| GPT-5.6 Sol / Codex | 96 | 15 | **6.40** | 10 | `+398/-2` | 0 |
| GPT-5.6 Terra / Codex | 94 | 16 | 5.88 | 9 | `+369/-5` | 0 |
| GPT-5.6 Luna / Open Code | 93 | 30 | 3.10 | 6 | `+465/-0` | 0 |
| Claude Sonnet 5 / Claude Code | 92 | 19 | 4.84 | 13 | `+414/-1` | 0 |
| Qwen3.7 Plus / Open Code | 87 | 14 | 6.21 | 10 | 約400行規模 | 0 |

### 品質

Final Synthesisが最も高い。

候補比較で見つかったfailure pathやparallel safetyの弱点を事前に潰せたことが効いている。

### 速度

処理時間24で、Sol 15・Terra 16・Qwen 14より遅い。

候補比較の知見を統合したことに加え、parallel化で実際に発生したCI raceを修正した分だけ処理量が増えている。

### 変更量

Sol候補より約200行多い。

ただしOpus候補のようなcustom xUnit frameworkやprocess-global container管理は導入しておらず、増加分は主にfailure-path hardening、fixture test、README、Console race修正である。

**結論として、Final Synthesisは「最小差分の勝者」ではなく、「候補比較で得た知見を使って品質をもう一段上げた最終版」と評価するのが適切。**

---

## 今回の評価で差が付いたポイント

全候補でPostgreSQL 18や指定image digestの利用自体には大きな差がなかった。

順位を分けたのは主に次の3点。

### 1. Test isolation

上位候補は**testごとに独立database**を使用し、test追加や実行順序によってstateが干渉しにくい構成だった。

一方、DeepSeek V4 FlashやMiMo-V2.5ではtest class内でdatabaseを共有する問題が残った。

Final Synthesisはtest単位databaseを採用し、さらに`template0`と`Pooling=false`で境界を強化した。

### 2. 並列実行

GPT-5.6 Sol / Codexは、単に並列Taskを作るだけでなく、**PostgreSQL上で実際に処理が重なっていることまで検証**した。

Final Synthesisも同じserver-side interval overlapを維持する一方、xUnit schedulerの並列実行を証明したとは主張しない。

また、全面parallel化による既存Console captureのraceを実CIで検出・修正した。

### 3. Cleanup・失敗時処理

integration test基盤では正常系だけでなく、

- container startup failure
- connection failure
- database cleanup failure
- container dispose failure

を正しくtest failureとして扱えるかが重要だった。

Final Synthesisでは、candidate比較で見つかった「例外握り潰し」「cleanup失敗後にretry不能」「failed-start cleanup failureの消失」という失敗パターンを避けている。

---

## 品質と速度

Candidate benchmarkでは、

- 全体最速: Grok 4.5 / Cursor、Composer 2.5 / Cursor — 9
- Majorなしで最速: Qwen3.7 Plus / Open Code — 87点 / 14
- 品質と速度のcandidate最良バランス: GPT-5.6 Sol / Codex — 96点 / 15

だった。

Final Synthesisは **98点 / 24 / 品質・時間4.08**。

品質は最も高いが、速度効率ではSolやQwenを下回る。

したがって今回の結果は、

> **candidate選定ではSolが最も効率的で、Final Synthesisでは追加時間を使ってfailure safetyとrepository適合性を上積みした**

と整理できる。

---

## 実装方式の傾向

### test classごとにcontainer + testごとにdatabase

代表: GPT-5.6 Sol / Codex、Final Synthesis

ownershipが明確で、並列化と確実なteardownを両立しやすい。

Final Synthesisでもこの方式を維持した。

### shared container + testごとにdatabase

代表: GPT-5.6 Terra / Codex、GPT-5.6 Luna / Open Code

container起動コストを抑えながら強いisolationを確保できる。

効率面では魅力があるが、Final Synthesisではoptimizationよりownershipの単純さを優先して採用しなかった。

### schema isolation

代表: MiMo-V2.5-Pro / Open Code

通常のtable testには使えるが、database-wideなPostgreSQL固有機能を扱う共通基盤としては弱い。

### process全体でcontainer共有

代表: Grok 4.5 / Cursor、Claude Opus 5 / Claude Code

効率化できる一方、teardown ownershipが難しくなる。

Final Synthesisではprocess-global containerもcustom xUnit frameworkも採用していない。

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

## 結論

Candidate benchmarkの順位は変わらない。

1. **GPT-5.6 Sol / Codex — 96**
2. **GPT-5.6 Terra / Codex — 94**
3. **GPT-5.6 Luna / Open Code — 93**
4. **Claude Sonnet 5 / Claude Code — 92**
5. **Qwen3.7 Plus / Open Code — 87**

その比較結果を使って作成したFinal Synthesisは、同じ採点基準で**98点**となった。

Final Synthesisは、

- Solの単純なownership
- Terraのrepository-level parallel分離
- Luna / Open Codeのfailure aggregation
- Sonnetのresource cleanup検証
- Opusのfailure injectionと`template0`

を必要な範囲だけ取り込み、candidateで確認された主要な失敗パターンを避けている。

一方、処理時間24・`+607/-9`となり、Sol候補より速度と最小性では後退した。

したがって最終的な観測は、

> **13候補比較によって最も良い単独実装を選ぶだけでなく、比較で得た失敗知見を使うことで、品質を96点から98点へ上積みできた。ただし、その改善には追加の実装時間とコード量が必要だった。**

となる。

なおFinal SynthesisはまだPR #104のDraft / Open状態であり、Issue #41のCloseには独立Agent BレビューでBlocker / Major 0を確認した上で、merge済みPRを証拠として揃える必要がある。

詳細な採点根拠、Acceptance Criteria判定、設計比較、CI証跡は [`implementation-evaluation.md`](./implementation-evaluation.md) を参照する。
