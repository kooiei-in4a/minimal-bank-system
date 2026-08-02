# spec-fix-001 14モデル採点報告

## 1. 追加評価の結論

- 追加対象: GPT-5.6 Luna XHigh、GPT-5.6 Luna Middle。
- 完全性ゲート: 2モデルともPASS。各モデルで修正報告書と仕様全文を取得。
- GPT-5.6 Luna XHigh: **98.0 / Excellent / Hard failなし**。
- GPT-5.6 Luna Middle: **62.0 / Invalid**。F-003の未承認判断を仕様本文で確定した。
- 14モデル全体では、有効提出はGPT-5.6 Luna XHighとChatGPT-5.6 Sol Highの2件。
- `raw score`はHard fail適用前の診断点。公式判定はHard failを優先する。

## 2. 採点方法

既存の`file-first-v3`評価ルーブリックを変更せず使用した。

1. 完全性ゲート
2. Hard failゲート
3. 100点採点
   - Finding coverage 24
   - Correctness 20
   - Regression safety 14
   - Scope discipline 10
   - Approval discipline 10
   - Traceability 8
   - Acceptance testability 7
   - Precision 4
   - Output compliance 3
4. 修正報告書の自己申告より、実際の修正後仕様書を優先した。

## 3. 総合結果

|順位|モデル|参考点|公式判定|Hard fail|
|---:|---|---:|---|---|
|1|GPT-5.6 Luna XHigh|98.0|Excellent|なし|
|2|ChatGPT-5.6 Sol High|97.0|Excellent|なし|
|3|ChatGPT-5.6 Sol Fast|89.5|Invalid|F-003未承認判断の確定|
|4|ChatGPT-5.6 Sol Middle|88.0|Invalid|F-003未承認判断の確定|
|5|DeepSeek V4 Flash High|83.0|Invalid|F-003未承認判断の確定|
|6|Claude Opus 4.6 High|82.5|Invalid|F-003未承認判断の確定|
|6|GLM-5.2 High|82.5|Invalid|F-003未承認判断の確定|
|8|Claude Opus 5 High|82.0|Invalid|F-003未承認判断の確定|
|9|Claude Sonnet 5 High|81.5|Invalid|F-003未承認判断の確定|
|10|DeepSeek V4 Pro High|79.0|Invalid|F-003未承認判断の確定|
|11|Gemini 3.1 Pro|74.0|Invalid|F-003未承認判断の確定|
|12|Gemini Thinking|66.5|Invalid|F-003未承認判断の確定|
|13|Gemini Flash|65.5|Invalid|F-003未承認判断の確定|
|14|GPT-5.6 Luna Middle|62.0|Invalid|F-003未承認判断の確定|

## 4. 配点内訳

F=Finding coverage、C=Correctness、R=Regression safety、S=Scope、A=Approval、T=Traceability、AT=Acceptance testability、P=Precision、O=Output compliance。

|モデル|F/24|C/20|R/14|S/10|A/10|T/8|AT/7|P/4|O/3|合計|
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
|GPT-5.6 Luna XHigh|24.0|20.0|14.0|9.0|10.0|8.0|7.0|3.0|3.0|98.0|
|ChatGPT-5.6 Sol High|24|20|14|8.5|10|8|7|2.5|3|97.0|
|ChatGPT-5.6 Sol Fast|23|16|14|8.5|7|8|7|3|3|89.5|
|ChatGPT-5.6 Sol Middle|22.5|16|14|8|7|8|6.5|3|3|88.0|
|DeepSeek V4 Flash High|22|15|13.5|8.5|6.5|6|6|3|2.5|83.0|
|Claude Opus 4.6 High|22|14.5|13.5|8|6|6|6|3.5|3|82.5|
|GLM-5.2 High|21.5|15|13.5|8|6.5|7|5|3|3|82.5|
|Claude Opus 5 High|22.5|14|13|7|6|8|6|2.5|3|82.0|
|Claude Sonnet 5 High|22|14|13.5|8|6|6|6|3|3|81.5|
|DeepSeek V4 Pro High|20.5|14|13.5|8.5|6|6|5.5|3|2|79.0|
|Gemini 3.1 Pro|19.5|12|12.5|8|5|7|4|3|3|74.0|
|Gemini Thinking|19.5|11.5|10|7|5|5|4.5|1.5|2.5|66.5|
|Gemini Flash|17|10.5|12.5|7.5|4.5|5.5|3|2.5|2.5|65.5|
|GPT-5.6 Luna Middle|13.0|11.0|13.5|8.0|5.5|3.0|3.0|2.5|2.5|62.0|

## 5. GPT-5.6 Luna XHigh

F-003について、同一冪等キーへ異なるpayloadを送った場合の結果を拒否、成功、既存結果返却のいずれにも確定していない。仕様本文、AC、§22.1が一貫して承認待ちとなっている。

直接修正対象は高い水準で処理されている。

- 解約後の5参照種別と状態制約優先
- 主要異常系・境界・権限・同時実行の原因別AC
- Transaction、Audit Log、障害ログの責務分離
- 出金・振込の行ロック維持と入金方式非固定
- 24件のREQ、B-01〜B-06、D-01〜D-17の追跡
- ADR-CANDIDATE-011〜014だけを追加
- F-003、F-004、F-008の決定軸、影響、承認後ACの整理

主な減点は、必要な補完に伴いAC数が56件まで増えており、わずかに冗長な点である。

## 6. GPT-5.6 Luna Middle

修正報告書ではF-003を`BLOCKED_BY_APPROVAL`としているが、仕様本文では同一キー・異内容を入金、出金、振込で拒否すると確定したまま残している。これはKoo承認待ちのsame-key different-payload結果をモデルが代行決定したことになる。

そのほか、必須ACの不足、未定義ACへの参照、障害ログ契約の不足、REST API主宣言の未達、報告書と実変更の不一致が残る。

## 7. 判断

1. 最優秀は**GPT-5.6 Luna XHigh（98.0）**。
2. 次点は**ChatGPT-5.6 Sol High（97.0）**。
3. この2件だけがHard failなしの有効提出。
4. F-003、F-004、F-008は未決のため、最優秀結果でもSpecification ReadyをPASSにはできない。

## 8. 概算実行時間・実行方法

実行時間はオペレーターによる概算であり、採点には含めない。実行方法、UI、サービス側の混雑、ファイル生成方式が異なるため、純粋なモデル推論速度としては扱わない。

| モデル | 概算時間 | 実行方法 | 参考点 | 公式判定 |
|---|---:|---|---:|---|
| GPT-5.6 Luna XHigh | 12 | Codex App | 98.0 | Excellent |
| ChatGPT-5.6 Sol High | 9 | Browser実行 | 97.0 | Excellent |
| ChatGPT-5.6 Sol Fast | 3 | Browser実行 | 89.5 | Invalid |
| ChatGPT-5.6 Sol Middle | 4 | Browser実行 | 88.0 | Invalid |
| DeepSeek V4 Flash High | 7 | Open Code | 83.0 | Invalid |
| Claude Opus 4.6 High | 12 | Claude Desktop | 82.5 | Invalid |
| GLM-5.2 High | 7 | Open Code | 82.5 | Invalid |
| Claude Opus 5 High | 8 | Claude Desktop | 82.0 | Invalid |
| Claude Sonnet 5 High | 11 | Claude Desktop | 81.5 | Invalid |
| DeepSeek V4 Pro High | 8 | Open Code | 79.0 | Invalid |
| Gemini 3.1 Pro | 3 | Browser実行 | 74.0 | Invalid |
| Gemini Thinking | 3 | Browser実行 | 66.5 | Invalid |
| Gemini Flash | 2 | Browser実行 | 65.5 | Invalid |
| GPT-5.6 Luna Middle | 7 | Codex App | 62.0 | Invalid |

## Repository retention

- 28提出成果物は`source-artifacts.csv`でbytesとSHA-256を固定した。
- Gitへ全文保存するのは最優秀モデルの修正報告書だけとし、生成仕様書を正本と誤認させない。
- 最優秀修正後仕様書の反映は、Issue #7 / PR #9への明示的な差分適用、Issue #10の独立再レビュー、Koo承認を別工程で行う。
