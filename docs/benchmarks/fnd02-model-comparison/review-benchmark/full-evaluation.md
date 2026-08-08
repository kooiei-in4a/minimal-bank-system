# Issue #40 — 17モデル独立レビュー性能評価

## 1. Executive Summary

評価対象は **17 Model + Agent/Harness** です。モデルの評判、文章量、指摘件数は評価に使わず、提出されたレビューについて、重大問題の検出、誤検知、一次証拠、Severity、仕様理解、テスト評価、Signal-to-Noise、最終判定の8軸で採点しました。 

**総合1位は `Gpt 5.6 Sol xHigh（Codex）`、100.0点です。**

このモデルだけが、Reference Reviewで確定した4件のmerge-blocking root causeをすべて検出し、重大なFalse Positiveを出さず、Severityと最終判定も一致しました。

| 項目                | 結果                                      |
| ----------------- | --------------------------------------- |
| 評価モデル数            | 17                                      |
| Best Overall      | **Gpt 5.6 Sol xHigh（Codex）**            |
| 重大問題検出            | **Codex Sol、Claude Opus 5が4/4**         |
| 最も低ノイズ            | **Codex Sol**                           |
| 最も深いframework検証   | **Codex Sol、Claude Opus 5、Browser Sol** |
| 最も重大な差            | **TestServerではなくKestrel境界まで検証したか**      |
| Reference Verdict | **REQUEST CHANGES / NOT MERGE READY**   |

主な傾向は明確です。

* 上位モデルは、既存23テストの成功をそのまま信頼せず、**テストが通っていないproduction server境界**を調べました。
* 中位モデルは、404または未承認`internal_error`のどちらかには到達しましたが、Kestrelとcancellationの問題を見逃しました。
* 下位モデルは、CI greenと実装者の説明を再確認しただけで、**未テスト経路をレビューできていません**。
* Qwen3.7 Plusは対象Headを取得できず、MiMo-V2.5とMiniMax M3はレビュー完了に至りませんでした。これはモデル単体ではなく、今回の**Model + Harness全体の性能結果**として扱っています。

---

# 2. Reference / Gold Review

## 2.1 対象の確定

PR #83は現在もDraft/Openで、対象Headは指定どおり
`2306c634abc40b4e5330c9492c8bcee8c0d6a5cc`、変更は10 files、`+848 / -1`です。

Issue #40は、共通error envelope、exception mapping extension point、correlation ID、TimeProvider、JSON technical logging、禁止ログ項目、API integration test hostを所有します。unmapped exceptionの安全な500変換と、secretをtechnical logへ記録しないことも明示的なAcceptance Criteriaです。

## 2.2 Reference Findings

| ID   | Severity    | Finding                                                            | Merge blocking |
| ---- | ----------- | ------------------------------------------------------------------ | :------------: |
| G-01 | **Blocker** | 未承認の外部公開code `internal_error`をAPI契約へ追加している                         |       YES      |
| G-02 | **Major**   | framework/status-onlyエラーが`{code,message}` envelopeにならない            |       YES      |
| G-03 | **Major**   | 非request-abortの`OperationCanceledException`も無条件にsafe fallbackを迂回する |       YES      |
| G-04 | **Major**   | middlewareから逃げた例外詳細がKestrel JSON logへ露出する                          |       YES      |
| G-05 | Minor       | OCE integration testが空500と例外伝播のどちらでも成功する                           |       NO       |
| G-06 | Minor       | mapper failureがtechnical log上で識別できず、後続mapperも評価されない                |       NO       |

### G-01 — 未承認の`internal_error`

コードはunmapped exceptionにHTTP 500 / `internal_error`を返します。しかし、承認済み仕様§16.3の固定コード一覧に`internal_error`はありません。仕様では`code`を機械判定の正本としています。

PR説明で「局所infrastructure fallback」と書いても、API利用者へ返される以上、外部契約です。`AGENTS.md`はPR説明やIssueコメントによる仕様の暗黙変更を禁止しています。

本評価では、単なるコード修正ではなく、**code名・意味・HTTP対応についてKooの決定が必要**であり、Issue #40自身の「error contractに未決のKoo判断がある場合は停止」に該当するためBlockerとしました。

SeverityをMajorとしたモデルにも検出点は与えていますが、Severity軸で小さく減点しています。

### G-02 — 404/405等が共通envelopeにならない

`Program.cs`が共通化しているのは、exception middlewareとModelState validationです。status-only responseを変換するmiddlewareはありません。

ASP.NET Coreは既定で、bodyを持たない400–599を空bodyのまま返します。したがって少なくともunmatched routeの404、method mismatchの405などは、仕様§16.1の最低限の`{code,message}`を満たしません。([Microsoft Learn][1])

仕様のAC-ERR-001は「業務エラー」と書かれているため若干の解釈余地はあります。しかし、

* §16.1は「すべてのエラー」と記述
* Issue #40は共通REST error envelopeを所有
* 後続APIがこの基盤を共通利用する
* framework error用の固定codeも未決定

であることから、merge前に実装するか、正本上で契約対象外と明示する必要があるMajorと判断しました。

### G-03 — `OperationCanceledException`の無条件rethrow

middlewareは`OperationCanceledException`を、`RequestAborted`の状態を確認せずすべてrethrowします。mapperが投げたOCEも同様です。

Kestrelは、connectionが実際にabort済みの場合だけOCEをrequest abortとして扱い、それ以外はapplication errorとして処理します。

したがって、内部timeoutやアプリケーションから投げられた非abort OCEは、

* safe 500 envelopeを通らない
* 空bodyの500または接続エラーになる
* allow-list technical logを通らない
* G-04のKestrel exception log経路へ進む

という問題があります。

### G-04 — Kestrelで例外message・stackがJSON logへ露出

response開始後の例外とOCEはmiddlewareからrethrowされます。既存テストは`WebApplicationFactory<Program>`を使っていますが、`UseKestrel()`を呼んでいないため既定のTestServerです。

.NET 10の`WebApplicationFactory`も、明示的に`UseKestrel()`した場合だけKestrelへ切り替わる実装です。

production Kestrelでは、逃げた例外がEvent 13 `ApplicationError`へ例外object付きで渡されます。

さらにJSON console formatterは`Exception.ToString()`を`Exception`フィールドへ出力します。

したがって、既存テストで非露出とされた

`RESPONSE_STARTED_EXCEPTION_SECRET_SENTINEL`

は、TestServerでは出なくてもKestrelではmessage・stackとともに出る経路があります。これはADR-0008のpassword、JWT、signing key、raw idempotency key等をtechnical logへ出さないという方針に直接反します。

### G-05 — OCEテストのfalse assurance

`OperationCanceledExceptionIsNotConvertedToGeneric500`は、

* responseが返ればbodyに`internal_error`がないことだけ確認
* exceptionになればchainにOCEがあることだけ確認

という構造です。

そのため、**空bodyのHTTP 500でも、OCEがclientまで伝播しても成功**します。status、request abort状態、期待するconnection behaviorを固定できていません。

### G-06 — mapper failureの診断性

mapper自身が失敗すると、その例外は記録されず即座にgeneric mappingへ戻ります。その後のtechnical logに出るexception typeはmapper exceptionではなく、最初の業務・application exceptionです。後続mapperも試されません。

現時点ではproduction mapperが0件なのでmerge blockerではありませんが、extension pointとしては診断性の弱い設計です。

## 2.3 Reference Verdict

**REQUEST CHANGES / NOT MERGE READY**

```text
Blocker: 1
Major:   3
Minor:   2
Nit:     0
```

CI成功と既存23テスト成功は有効な証拠ですが、G-03/G-04のproduction Kestrel境界を証明していません。

FND-03はIssue上FND-01 merge後にFND-02と並行可能なので、FND-03の着手自体は止める必要がありません。ただし、現在のHeadを承認済みFND-02 baseとして扱うことはできません。

---

# 3. 総合ランキング

`TP / FP / FN`は、G-01〜G-04の**4件のmerge-blocking root cause**を正規化した件数です。

* TPはSeverityが違ってもroot causeを認識していれば計上
* Severity誤りはD軸で減点
* Minor/NitのnoiseはB/G軸で減点
* 「指摘なし」のレビューはFP 0でも、FN 4となります

| Rank | Model                          | Harness     |     Score | TP | FP | FN | Verdict | Grade |
| ---: | ------------------------------ | ----------- | --------: | -: | -: | -: | ------- | :---: |
|    1 | **Gpt 5.6 Sol xHigh**          | Codex       | **100.0** |  4 |  0 |  0 | 正確      |   S   |
|    2 | **Claude Opus 5 xhigh**        | Claude Code |  **92.5** |  4 |  0 |  0 | 正確      |   A+  |
|    3 | **GPT-5.6 Luna**               | Open Code   |  **88.0** |  3 |  0 |  1 | 正確      |   A   |
|    4 | **Chatgpt Opus 5.6 Sol xhigh** | Browser     |  **87.5** |  2 |  0 |  2 | 正確      |   A   |
|    5 | **Gpt 5.6 luna xHigh**         | Codex       |  **82.0** |  2 |  0 |  2 | 正確      |   B+  |
|    6 | **DeepSeek V4 Flash**          | Open Code   |  **77.0** |  2 |  0 |  2 | 正確      |   B   |
|    7 | **Gpt 5.6 terra xHigh**        | Codex       |  **75.5** |  1 |  0 |  3 | 正確      |   B   |
|    8 | Claude Sonnet 5 xhigh          | Claude Code |      60.0 |  1 |  0 |  3 | 不正確     |   D   |
|    9 | Composer 2.5                   | Cursor      |      54.5 |  1 |  0 |  3 | 不正確     |   D   |
|   10 | Grok 4.5 high fast             | Cursor      |      54.0 |  0 |  0 |  4 | 不正確     |   D   |
|   11 | Chatgpt Opus 5.5 xhigh         | Browser     |      53.0 |  0 |  0 |  4 | 不正確     |   D   |
|   12 | DeepSeek V4 Pro                | Open Code   |      47.5 |  0 |  0 |  4 | 不正確     |   F   |
|   13 | chatgpt o2                     | Browser     |      35.0 |  1 |  1 |  3 | 結論のみ一致  |   F   |
|   14 | MiMo-V2.5-Pro                  | Open Code   |      19.0 |  0 |  0 |  4 | 不正確     |   F   |
|   15 | MiMo-V2.5                      | Open Code   |       7.0 |  0 |  1 |  4 | 未完了     |   F   |
|   16 | Qwen3.7 Plus                   | Open Code   |       2.0 |  0 |  1 |  4 | 対象誤認    |   F   |
|   17 | MiniMax M3                     | Open Code   |       0.0 |  0 |  0 |  4 | 未完了     |   F   |

---

# 4. 評価軸別スコア

| Model                      | A /25 | B /20 | C /15 | D /10 | E /10 | F /8 | G /7 | H /5 |     Total |
| -------------------------- | ----: | ----: | ----: | ----: | ----: | ---: | ---: | ---: | --------: |
| Gpt 5.6 Sol xHigh          |  25.0 |  20.0 |  15.0 |  10.0 |  10.0 |  8.0 |  7.0 |  5.0 | **100.0** |
| Claude Opus 5 xhigh        |  25.0 |  16.5 |  15.0 |   9.5 |   9.0 |  8.0 |  4.5 |  5.0 |  **92.5** |
| GPT-5.6 Luna               |  19.5 |  18.5 |  13.5 |   9.5 |   9.0 |  7.0 |  6.0 |  5.0 |  **88.0** |
| Chatgpt Opus 5.6 Sol xhigh |  13.5 |  20.0 |  15.0 |   9.5 |   9.5 |  8.0 |  7.0 |  5.0 |  **87.5** |
| Gpt 5.6 luna xHigh         |  14.0 |  19.5 |  12.0 |   8.5 |   9.5 |  6.5 |  7.0 |  5.0 |  **82.0** |
| DeepSeek V4 Flash          |  14.0 |  17.0 |  11.5 |   8.0 |   9.0 |  7.0 |  5.5 |  5.0 |  **77.0** |
| Gpt 5.6 terra xHigh        |   5.5 |  20.0 |  12.5 |  10.0 |   9.0 |  6.5 |  7.0 |  5.0 |  **75.5** |
| Claude Sonnet 5 xhigh      |   4.0 |  18.5 |  12.5 |   4.0 |   9.0 |  5.5 |  6.5 |  0.0 |  **60.0** |
| Composer 2.5               |   3.0 |  17.5 |  11.0 |   4.0 |   8.5 |  5.0 |  5.5 |  0.0 |  **54.5** |
| Grok 4.5 high fast         |   0.0 |  20.0 |  10.5 |   3.0 |   9.0 |  4.5 |  7.0 |  0.0 |  **54.0** |
| Chatgpt Opus 5.5 xhigh     |   0.0 |  20.0 |  10.0 |   3.0 |   9.0 |  4.0 |  7.0 |  0.0 |  **53.0** |
| DeepSeek V4 Pro            |   0.0 |  15.0 |  10.5 |   4.0 |   8.0 |  4.5 |  5.5 |  0.0 |  **47.5** |
| chatgpt o2                 |   4.0 |   7.0 |   7.0 |   3.0 |   5.0 |  3.0 |  2.0 |  4.0 |  **35.0** |
| MiMo-V2.5-Pro              |   0.0 |  10.0 |   1.0 |   2.0 |   1.0 |  0.0 |  5.0 |  0.0 |  **19.0** |
| MiMo-V2.5                  |   0.0 |   2.0 |   2.0 |   0.0 |   2.0 |  0.0 |  1.0 |  0.0 |   **7.0** |
| Qwen3.7 Plus               |   0.0 |   0.0 |   0.0 |   0.0 |   1.0 |  0.0 |  1.0 |  0.0 |   **2.0** |
| MiniMax M3                 |   0.0 |   0.0 |   0.0 |   0.0 |   0.0 |  0.0 |  0.0 |  0.0 |   **0.0** |

---

# 5. 各モデル詳細評価

## 1. Gpt 5.6 Sol xHigh（Codex）

**Score: 100.0 / 100 — S**
**Type: Deep Technical Reviewer / Precision Reviewer / Specification Reviewer**

**True Positives:** G-01、G-02、G-03、G-04の全件。

`internal_error`の正本違反、404/405等、非abort OCE、Kestrel exception logを、それぞれ独立したroot causeとして正しく分離しています。

実Kestrel probeで、

* 非abort OCEの空500
* response-started例外のsentinel露出
* unmatched route、method mismatch、通常のNotFound
* TestServerとKestrelの差

まで確認しています。

**False Positives / False Negatives:** なし。

SeverityはG-01をBlocker、残りをMajorとし、Referenceと一致しています。指摘は4件だけで、いずれもmergeを止める価値があります。

**総評:** 今回のAgent Bとして理想的です。重大問題のrecall、precision、framework検証、仕様統制、最終判定がすべて揃っています。

---

## 2. Claude Opus 5 xhigh（Claude Code）

**Score: 92.5 / 100 — A+**
**Type: Deep Technical Reviewer / Broad Reviewer / Over-strict Reviewer**

**True Positives:** G-01〜G-04の全件。

実Kestrelを起動して、OCE、response-started exception、空bodyの404を独自に再現しています。既存テストがTestServerのためfalse assuranceになっていることも正しく説明しています。

**False Negatives:** なし。

一方、以下はSignal-to-Noiseを下げました。

* 成功requestのtechnical logがないこと
* correlation policyの文書化
* `ApplicationTime` wrapperへの設計嗜好
* Parent Issueの古い記述
* PR template準拠
* 複数のNit

一部は有用な引継ぎ事項ですが、Issue #40のmerge blockerとして扱う必要はありません。

**総評:** hidden bug探索能力は非常に高い一方、最終レビューとしてはCodex Solよりトリアージ負荷が高いです。Agent Bの前段で使うadversarial reviewerとして特に強い結果です。

---

## 3. GPT-5.6 Luna（Open Code）

**Score: 88.0 / 100 — A**
**Type: Specification Reviewer / Deep Technical Reviewer**

**True Positives:** G-01、G-02、G-04。

未承認の`internal_error`をBlockerとして認識し、404の実挙動と、TestServerではKestrelのexception logを証明できない点を正しく指摘しています。

**False Negative:** G-03。

OCEのKestrel漏洩可能性はG-04の一部として確認していますが、非abort OCEがsafe error envelopeを迂回する独立したHTTP契約違反までは指摘していません。

READMEや仕様ヘッダーの状態不一致は参考情報ですが、本PRのmerge blockerではありません。

**総評:** 仕様統制とframework境界をバランスよく確認できています。Open Code群では最も信頼できるレビューです。

---

## 4. Chatgpt Opus 5.6 Sol xhigh（Browser）

**Score: 87.5 / 100 — A**
**Type: Deep Technical Reviewer / Precision Reviewer**

**True Positives:** G-02、G-04。

特にG-04について、WebApplicationFactory、TestServer、Kestrel Event 13、JSON formatterまで公式実装を追っており、技術検証の深さは上位です。

**False Negatives:** G-01、G-03。

OCEについて非client-abortケースを実Kestrelで確認すべきとは述べていますが、現在の実装がsafe envelopeを迂回する独立Majorとして確定していません。また、`internal_error`の正本未承認問題を見逃しています。

**False Positive:** なし。

**総評:** 非常に低ノイズで、Kestrel・logging・framework内部に強いレビューです。仕様統制を補う別レビュアーと組み合わせれば実務価値が高いモデルです。

---

## 5. Gpt 5.6 luna xHigh（Codex）

**Score: 82.0 / 100 — B+**
**Type: Specification Reviewer / Precision Reviewer**

**True Positives:** G-01、G-02。

framework errorと未承認codeという契約上の2問題を簡潔に検出し、Merge Readyではないと正しく判定しました。

**False Negatives:** G-03、G-04。

「キャンセル処理、response-started処理には重大な問題なし」としており、Kestrel境界と非abort OCEを見逃しています。

`internal_error`をMajorとした点は、root cause検出としては正しいものの、Koo判断を必要とする停止条件としてはSeverityが一段軽いです。

**総評:** 仕様中心のgate reviewには有効ですが、production runtimeの深い障害経路にはbackup reviewerが必要です。

---

## 6. DeepSeek V4 Flash（Open Code）

**Score: 77.0 / 100 — B**
**Type: Specification Reviewer / Broad Reviewer**

**True Positives:** G-01、G-02。

正本にない`internal_error`と、404/405/406/415等の非例外系error envelopeをMajorとして正しく指摘しました。

**False Negatives:** G-03、G-04。

既存Agent Bレビューを先に確認しているため、独立検出というより既存指摘への追認に近い部分があります。Kestrel log leakとOCEの独立問題には到達していません。

`BadHttpRequestException`の4xx→500変換は技術的に検討価値がありますが、提出内容では実再現またはframework sourceによる確定が弱く、Reference Minorには採用していません。

**総評:** 契約レビューとしては有用ですが、既存レビューにアンカリングされやすく、hidden runtime bugの探索力は限定的でした。

---

## 7. Gpt 5.6 terra xHigh（Codex）

**Score: 75.5 / 100 — B**
**Type: Precision Reviewer / Surface Reviewer**

**True Positive:** G-02。

未知routeを実行し、404空bodyを確認したうえで、仕様§16.1との不一致をMajorとしました。

**False Negatives:** G-01、G-03、G-04。

指摘は1件だけですが、その1件は完全に正確で、不要な問題は出していません。Severityと最終REQUEST CHANGESも正しいです。

**総評:** precisionは非常に高いもののrecallが不足しています。高速な一次gateには使えますが、単独Agent Bには不十分です。

---

## 8. Claude Sonnet 5 xhigh（Claude Code）

**Score: 60.0 / 100 — D**
**Type: Surface Reviewer / Specification Reviewer**

`internal_error`が仕様表に存在しないこと自体は認識しています。しかし、将来判断でよいとしてApproveし、現在の公開契約変更をmerge blockerにしませんでした。

**False Negatives:** G-02、G-03、G-04。

build/testを独立再実行し、diffとscopeを丁寧に確認している点は良好です。一方、TestServerで成功したsecurity testをproduction保証として受け入れました。

**総評:** 実装報告の整合確認には強いものの、未テスト経路を攻める独立レビューとしては不十分です。

---

## 9. Composer 2.5（Cursor）

**Score: 54.5 / 100 — D**
**Type: Broad Reviewer / Surface Reviewer**

`internal_error`未登録をMinorとして認識しましたが、PRをmerge可と判定しました。

**False Negatives:** G-02、G-03、G-04。

mapper failure、parallelization、重複設定など複数のMinor/Nitを挙げていますが、mergeを止めるべきruntime contractの問題を見逃しています。

**総評:** 広く差分を見る能力はありますが、Severityの優先順位が逆転しています。重要問題より保守上の小さな観察に注意が向いています。

---

## 10. Grok 4.5 high fast（Cursor）

**Score: 54.0 / 100 — D**
**Type: Surface Reviewer / Precision Reviewer**

**False Positives:** なし。
**False Negatives:** G-01〜G-04の全件。

scope、correlation、TimeProvider、CIを正確に確認していますが、`internal_error`を局所codeとして受容し、TestServerのsecret testを十分な証拠と判断しました。

**総評:** ノイズは少ないものの、重要な問題も出していません。セルフレビューや高速sanity checkには使えても、最終merge gateには不向きです。

---

## 11. Chatgpt Opus 5.5 xhigh（Browser）

**Score: 53.0 / 100 — D**
**Type: Surface Reviewer**

Issue、spec、ADR、diff、test、CIを確認したと報告していますが、findingsは0件です。

**False Negatives:** G-01〜G-04の全件。

最大の問題は、実装が意図どおり書かれていることと、**意図自体が正しいこと**を区別できていない点です。既存テストが通る範囲だけでAPPROVEしました。

**総評:** 文書・コードの整合確認はできていますが、独立第三者レビューとして必要な反証探索が不足しています。

---

## 12. DeepSeek V4 Pro（Open Code）

**Score: 47.5 / 100 — F**
**Type: Surface Reviewer / Overconfident Reviewer**

Result documentの各claimをコード行と照合する作業は詳細です。しかし、claimそのものの妥当性を検証していません。

**False Negatives:** G-01〜G-04。

提出したMinor 2件は、

* middleware間の定数参照
* hypotheticalな`OnStarting` header上書き

であり、Issue #40の正しさ・安全性に実質的な影響はありません。

**総評:** implementation report verifierとしては使えますが、Agent Bとしては重大なFalse Negativeと低価値findingが逆転しています。

---

## 13. chatgpt o2（Browser）

**Score: 35.0 / 100 — F**
**Type: Over-strict Reviewer / Unreliable Reviewer**

`internal_error`について仕様決定が必要という方向性はG-01に近いものです。しかし、

* §16.3に`internal_error`が予約済みという主張は事実と異なる
* correlation IDが大文字を拒否するという主張も事実と異なる
* `urn:uuid:`を受理すべき仕様要件は存在しない

という重大なfactual errorがあります。

さらに、log一行10MB、Domain共通時刻service、明示mapping table等、scope外または将来改善を大量に列挙しています。

**False Negatives:** G-02、G-03、G-04。
**False Positive:** correlation ID互換性をblocking扱い。

**総評:** REQUEST CHANGESという結果だけはReferenceと一致しますが、根拠の正確性が低く、修正すると不要な複雑化を招くレビューです。

---

## 14. MiMo-V2.5-Pro（Open Code）

**Score: 19.0 / 100 — F**
**Type: Surface Reviewer / Unreliable Reviewer**

提出内容は、全claimを行単位で確認した、問題なし、APPROVEという短い結論だけです。

証拠、確認経路、差分分析、finding normalizationがなく、G-01〜G-04をすべて見逃しています。

**総評:** 結論を検証できず、独立レビュー成果物として成立していません。

---

## 15. MiMo-V2.5（Open Code）

**Score: 7.0 / 100 — F**
**Type: Unreliable Reviewer / Incomplete Reviewer**

レビュー計画の提示で停止し、最終レビューを提出していません。

さらに、「main上の既存API実装と新Runtime実装が並存している」という前提を置いていますが、対象base/Headの把握が不正確です。この誤認を前提にユーザーへ質問して停止しました。

**総評:** 明示されたHeadを最後まで検証し、best-effortで結論を出すというAgent Bの基本動作を満たしていません。

---

## 16. Qwen3.7 Plus（Open Code）

**Score: 2.0 / 100 — F**
**Type: Unreliable Reviewer / Wrong-target Reviewer**

対象Headは`2306c634...`と明示されているにもかかわらず、local Head `5ac5e43`をレビューし、FND-02実装が存在しないと結論付けました。

これはtarget branchではなくbase側の状態です。そのため、

* Programがない
* API runtimeがない
* ApplicationTimeがない
* integration testがない

というfindingはすべて対象PRに対するFalse Positiveです。

**総評:** レビュー内容以前に、対象SHAの固定に失敗しています。独立レビューで最も致命的な種類のエラーです。

---

## 17. MiniMax M3（Open Code）

**Score: 0.0 / 100 — F**
**Type: Incomplete Reviewer**

3セッションで実行してもレビュー結果が完成していません。

提出されたfinding、証拠、Severity、Verdictがないため、全評価軸を0点としました。

---

# 6. Reviewer能力タイプ分類

| Model                         | 主分類                                            |
| ----------------------------- | ---------------------------------------------- |
| Gpt 5.6 Sol xHigh（Codex）      | **Deep Technical / Precision / Specification** |
| Claude Opus 5 xhigh           | **Deep Technical / Broad / Over-strict**       |
| GPT-5.6 Luna（Open Code）       | **Specification / Deep Technical**             |
| Chatgpt Opus 5.6 Sol（Browser） | **Deep Technical / Precision**                 |
| Gpt 5.6 luna（Codex）           | **Specification / Precision**                  |
| DeepSeek V4 Flash             | **Specification / Broad**                      |
| Gpt 5.6 terra（Codex）          | **Precision / Surface**                        |
| Claude Sonnet 5               | **Surface / Specification**                    |
| Composer 2.5                  | **Broad / Surface**                            |
| Grok 4.5 high fast            | **Surface / Precision**                        |
| Chatgpt Opus 5.5              | **Surface**                                    |
| DeepSeek V4 Pro               | **Surface / Overconfident**                    |
| chatgpt o2                    | **Over-strict / Unreliable**                   |
| MiMo-V2.5-Pro                 | **Surface / Unreliable**                       |
| MiMo-V2.5                     | **Incomplete / Unreliable**                    |
| Qwen3.7 Plus                  | **Wrong-target / Unreliable**                  |
| MiniMax M3                    | **Incomplete**                                 |

---

# 7. 実務用途別ランキング

## 7.1 最終merge gate担当

1. **Gpt 5.6 Sol xHigh（Codex）**
   4/4検出、誤検知なし、Severity・Verdict完全一致。

2. **Claude Opus 5 xhigh**
   recallは同等。追加findingの人間によるトリアージが必要。

3. **GPT-5.6 Luna（Open Code）**
   仕様統制とKestrelリスクを両方確認。OCEのみ補完が必要。

## 7.2 セキュリティ・障害系レビュー

1. **Gpt 5.6 Sol xHigh（Codex）**
2. **Claude Opus 5 xhigh**
3. **Chatgpt Opus 5.6 Sol xhigh（Browser）**

3モデルともTestServerとKestrelの違いへ到達しています。

## 7.3 仕様・ADR整合レビュー

1. **Gpt 5.6 Sol xHigh（Codex）**
2. **GPT-5.6 Luna（Open Code）**
3. **Gpt 5.6 luna xHigh（Codex）**

特に`internal_error`を「コード内の局所実装」ではなく、外部公開契約として扱えた点を評価しています。

## 7.4 False Positiveが少ないモデル

有効なfindingを少なくとも1件出したモデルに限定すると、

1. **Gpt 5.6 Sol xHigh（Codex）**
2. **Chatgpt Opus 5.6 Sol xhigh（Browser）**
3. **Gpt 5.6 terra xHigh（Codex）**

「指摘なし」のモデルも形式上FP 0ですが、FN 4なのでprecision reviewerとは評価していません。

## 7.5 難しいhidden bug発見

1. **Gpt 5.6 Sol xHigh（Codex）**
2. **Claude Opus 5 xhigh**
3. **Chatgpt Opus 5.6 Sol xhigh（Browser）**

決定的だったのは、実Kestrel実行または公式framework sourceまで追跡したことです。

## 7.6 MVP開発で使いやすいモデル

1. **Gpt 5.6 Sol xHigh（Codex）**
2. **Chatgpt Opus 5.6 Sol xhigh（Browser）**
3. **GPT-5.6 Luna（Open Code）**

MVP向けでも「レビューが甘い」ことは利点になりません。重大問題を漏らさず、不要な変更を要求しないことが重要です。

---

# 8. モデル間で差が出た理由

## 8.1 CI greenを何の証拠として扱ったか

下位モデルは、

> 23/23 tests pass
> actual JSON console capture pass
> sentinel non-exposure pass

を、そのままproduction保証として扱いました。

上位モデルは、

> そのテストはどのserverを通っているか

まで確認しました。

今回、既存integration testはproduction `Program`を通っていますが、**production serverであるKestrelは通っていません**。ここを区別できるかが最大の差でした。

## 8.2 実装されたpathではなく、逃げるpathを確認したか

表面的なレビューは、

* middlewareがexception objectをloggerへ渡していない
* secret sentinel testがある

ことを確認して終わりました。

深いレビューは、

* middlewareがcatchしないOCE
* response開始後のrethrow
* Kestrelの最終exception handling
* JSON formatterのexception serialization

まで追いました。

## 8.3 正本の優先順位を守ったか

`internal_error`について、複数モデルはPRの「局所code」という説明を受け入れました。

しかしレビューで確認すべきなのは、実装者の意図ではなく、

* approved specificationに存在するか
* Accepted ADRで認可されているか
* Kooの明示判断があるか

です。

外部へ返る`code`を「局所」と呼んでも、クライアントから見れば公開契約です。

## 8.4 テストの存在ではなく、assertionの意味を読んだか

OCEテストは存在しますが、空500でも例外伝播でも成功します。

上位モデルはテスト名や件数ではなく、assertionの論理まで確認しました。下位モデルは「OCE testがある」ことをcoverageとして数えています。

## 8.5 Target Headを固定できたか

Qwen3.7 Plusは明示されたHeadではなくbase相当のlocal stateをレビューしました。

独立レビューでは、最初に以下を固定できなければ、その後の詳細分析に価値はありません。

```text
Repository
Base SHA
Head SHA
PR
Diff
CI target SHA
```

## 8.6 厳しさと正確さを区別できたか

chatgpt o2はREQUEST CHANGESに到達しましたが、相関IDの`urn:uuid:`対応など、仕様にない要件をblocking扱いしました。

Claude Opus 5は重要問題を全件検出しましたが、Minor/Nitも多く、Codex SolよりSignal-to-Noiseが低下しました。

最も優れたレビューは、単に厳しいのではなく、

> **必要な問題には厳しく、不要な問題は出さない**

レビューです。

---

# 9. 最終結論

```text
Best Overall Reviewer:
  Gpt 5.6 Sol xHigh (Codex)

Best Deep Bug Finder:
  Gpt 5.6 Sol xHigh (Codex)
  Runner-up: Claude Opus 5 xhigh (Claude Code)

Best Precision Reviewer:
  Gpt 5.6 Sol xHigh (Codex)
  Runner-up: Chatgpt Opus 5.6 Sol xhigh (Browser)

Best Spec Reviewer:
  GPT-5.6 Luna (Open Code)
  OverallではCodex Solが同等以上

Best MVP Reviewer:
  Gpt 5.6 Sol xHigh (Codex)

Most Important Differentiator:
  TestServerとproduction Kestrelを区別し、
  green testが証明していないserver境界を独立検証できたか

Recommended Agent B Model:
  Gpt 5.6 Sol xHigh (Codex)

Recommended Backup Reviewer:
  Chatgpt Opus 5.6 Sol xhigh (Browser)
```

## 1モデルだけをAgent Bとして選ぶ場合

**`Gpt 5.6 Sol xHigh（Codex）`**

今回の提出結果では、重大問題のrecall、False Positive抑制、Severity、証拠、最終判定のすべてで最も安定しています。

## 2モデルを組み合わせる場合

**`Gpt 5.6 Sol xHigh（Codex）` + `Claude Opus 5 xhigh（Claude Code）`**

役割を次のように分けるのが適切です。

* **Codex Sol:** 最終gate、findingの正規化、Severity、merge判断
* **Claude Opus:** adversarial exploration、実環境probe、hidden failure path探索

Claude Opusの広い探索力で候補を集め、Codex Solのprecisionで修正必須かを確定する構成です。単純な多数決ではなく、**Claudeが深く掘り、Codexが最終的にノイズを除去する**組合せが最も実務的です。

[1]: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0 "Handle errors in ASP.NET Core | Microsoft Learn"
