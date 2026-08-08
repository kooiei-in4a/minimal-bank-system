# Issue #40 FND-02 — 14モデル実装比較・最終評価

処理時間は単位が明示されていないため、以下では**「処理時間の記録値」**として数値をそのまま使用します。

## 1. Candidate一覧

|  # | Model             | Agent       | Head SHA                                   |        PR | Files |      +/- | Tests | CI                    | Time |
| -: | ----------------- | ----------- | ------------------------------------------ | --------: | ----: | -------: | ----: | --------------------- | ---: |
|  1 | DeepSeek V4 Pro   | Open Code   | `869e75d8b314311af732618a281f4453a14f8e25` | #67 Draft |    15 |  +638/-3 |    18 | SUCCESS `31214309925` |   39 |
|  2 | Qwen3.7 Plus      | Open Code   | `e2233f64e8f5902eec3060a4b4b04922bb8657be` | #70 Draft |    15 |  +659/-4 |    16 | SUCCESS `31216844889` |   30 |
|  3 | GPT-5.6 Luna      | Open Code   | `86eeacac1147890b8e7f01555a3bb37e5c1433e1` | #72 Draft |    14 |  +521/-4 |    16 | SUCCESS `31220034464` |   29 |
|  4 | DeepSeek V4 Flash | Open Code   | `9181417fb806b574d4ba664af5595ab0b77fcb9f` | #81 Draft |    22 |  +783/-4 |    22 | SUCCESS `31228219484` |   24 |
|  5 | MiMo-V2.5         | Open Code   | `413a955bcb71b34f1160a0979907f4fbe6297b31` | #74 Draft |    14 |  +830/-1 |    24 | SUCCESS `31221412552` |   24 |
|  6 | MiMo-V2.5-Pro     | Open Code   | `bc27e5e0ac6b95a122f55cd0c7eda3295e4515ae` | #78 Draft |    13 |  +628/-1 |    31 | SUCCESS `31222600990` |   35 |
|  7 | MiniMax M3        | Open Code   | `4e6a7b32b6c9f4532f7ec61dfb8217cdff7a368d` | #76 Draft |    16 |  +668/-1 |    17 | SUCCESS `31222258404` |   11 |
|  8 | GPT-5.6 Luna      | Codex       | `b7cb2a541ed557d163110edc0543dfc94a175d68` | #66 Draft |    14 |  +544/-1 |    12 | SUCCESS `31212793320` |   27 |
|  9 | GPT-5.6 Terra     | Codex       | `c5e5f782750ca4cde9a1138f7cb1893357dc444a` | #68 Draft |    12 |  +409/-1 |    10 | SUCCESS `31215597642` |   14 |
| 10 | GPT-5.6 Sol       | Codex       | `e9457cbc0d0de76054685877fb62e58ffed07bb3` | #71 Draft |    11 |  +534/-1 |    11 | SUCCESS `31219084120` |   17 |
| 11 | Grok 4.5          | Cursor      | `70f736c18f259c3bda1072620469fb7014c939fa` | #65 Draft |    21 |  +936/-4 |    19 | SUCCESS `31212480399` |   14 |
| 12 | Composer 2.5      | Cursor      | `aaf6ae84b2ae833b8a17cbb39609f5a0a31278f4` | #69 Draft |    29 |  +981/-1 |    15 | SUCCESS `31216486765` |   24 |
| 13 | Claude Sonnet 5   | Claude Code | `395e1e85ca6867acec10a111a7a9e1110e258e3b` | #80 Draft |    23 |  +544/-4 |    14 | SUCCESS `31222956728` |   24 |
| 14 | Claude Opus 5     | Claude Code | `f40c6046355583c35e6f6346a47798d87165ba80` | #79 Draft |    30 | +1390/-0 |    44 | SUCCESS `31222766908` |   31 |

### Branch・CI・比較条件

* 実装が存在する13候補は、すべてcommon baseをmerge baseとする`ahead 1 / behind 0`で、候補間commitの混入は確認されませんでした。
* 13件のPRはすべてOpen / Draft、commit数は各1件です。
* 13件すべてについて、candidate Headと一致するGitHub Actionsの`Build and Test` runを確認し、Restore・Build・Testはすべて成功しています。
* `dsv4flash`はbranchがcommon baseと完全に同一で、commit数0、変更0、PRなしです。したがって「8で実装した候補」ではなく、**実装を生成できなかった候補**です。
* Issue #39のcandidate PR、final synthesis branch、無関係なPRは採点資料として参照していません。

評価は、Issue #40が要求するerror envelope、correlation ID、TimeProvider、JSON technical logging、秘密値非記録、request-level integration testを基準としています。
また、実コード優先、必要十分な変更、Scope先取り減点、品質と速度の分離という指定方法論に従っています。

---

# 2. 採点表

| Rank | Model             | Agent       | A/25 | B/15 | C/15 | D/10 | E/10 | F/10 | G/10 | H/5 | Coding /100 |
| ---: | ----------------- | ----------- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --: | ----------: |
|    1 | **GPT-5.6 Sol**   | **Codex**   |   25 |   15 |   15 |    9 |   10 |    9 |    9 |   4 |      **96** |
|    2 | **GPT-5.6 Terra** | **Codex**   |   24 |   15 |   15 |    9 |    9 |    9 |   10 |   4 |      **95** |
|    3 | **GPT-5.6 Luna**  | **Codex**   |   24 |   15 |   15 |    9 |    9 |    9 |    9 |   4 |      **94** |
|    4 | **Grok 4.5**      | **Cursor**  |   24 |   15 |   15 |    9 |   10 |    8 |    7 |   4 |      **92** |
|    5 | GPT-5.6 Luna      | Open Code   |   21 |   14 |   12 |    8 |    9 |    8 |    8 |   4 |      **84** |
|    6 | Claude Sonnet 5   | Claude Code |   20 |   14 |   15 |    8 |    8 |    8 |    6 |   1 |      **80** |
|    7 | DeepSeek V4 Flash | Open Code   |   20 |   15 |   14 |    8 |    7 |    8 |    7 |   1 |      **80** |
|    8 | Claude Opus 5     | Claude Code |   18 |   11 |   14 |    7 |    9 |    7 |    5 |   1 |      **72** |
|    9 | Composer 2.5      | Cursor      |   19 |   13 |   13 |    7 |    7 |    7 |    3 |   1 |      **70** |
|   10 | Qwen3.7 Plus      | Open Code   |   17 |   12 |   10 |    6 |    6 |    6 |    8 |   1 |      **66** |
|   11 | MiniMax M3        | Open Code   |   15 |   10 |   14 |    6 |    6 |    6 |    6 |   1 |      **64** |
|   12 | MiMo-V2.5-Pro     | Open Code   |   14 |   10 |    9 |    5 |    4 |    6 |    8 |   0 |      **56** |
|   13 | MiMo-V2.5         | Open Code   |   12 |    9 |   14 |    5 |    5 |    6 |    4 |   0 |      **55** |
|   14 | DeepSeek V4 Pro   | Open Code   |   13 |    9 |    7 |    3 |    3 |    5 |    5 |   1 |      **46** |

採点内訳は指定された100点配分を使用しています。

---

# 3. Acceptance Criteria判定

`✓`は実コードとテストで成立、`△`は部分成立または検証不足、`✗`は欠落・明確な欠陥です。

| Candidate                     | AC-1 Envelope  | AC-2 Unmapped  | AC-3 Correlation  | AC-4 Caller ID  | AC-5 Time  | AC-6 JSON log  | AC-7 Prohibited fields  | AC-8 Response secret  | AC-9 No business  | AC-10 API integration  |
| ----------------------------- | :------------: | :------------: | :---------------: | :-------------: | :--------: | :------------: | :---------------------: | :-------------------: | :---------------: | :--------------------: |
| DeepSeek V4 Pro / Open Code   |       ✓       |       ✓       |         △        |        ✓       |     ✓     |       △       |            ✗           |           ✓          |         △        |           ✗           |
| Qwen3.7 Plus / Open Code      |       ✓       |       ✓       |         △        |        ✓       |     ✓     |       △       |            ✗           |           △          |         △        |           △           |
| GPT-5.6 Luna / Open Code      |       ✓       |       △       |         ✓        |        ✓       |     ✓     |       ✓       |            ✓           |           ✓          |         △        |           ✓           |
| DeepSeek V4 Flash / Open Code |       ✓       |       ✓       |         ✓        |        ✓       |     ✓     |       △       |            ✗           |           ✓          |         △        |           ✓           |
| MiMo-V2.5 / Open Code         |       ✓       |       ✓       |         △        |        ✗       |     ✓     |       △       |            ✗           |           ✓          |         ✓        |           △           |
| MiMo-V2.5-Pro / Open Code     |       ✓       |       ✓       |         △        |        ✓       |     ✓     |       △       |            ✗           |           ✓          |         △        |           ✗           |
| MiniMax M3 / Open Code        |       ✓       |       ✓       |         △        |        ✓       |     ✓     |       △       |            ✗           |           ✓          |         ✓        |           △           |
| GPT-5.6 Luna / Codex          |       ✓       |       ✓       |         ✓        |        ✓       |     ✓     |       ✓       |            ✓           |           ✓          |         ✓        |           ✓           |
| GPT-5.6 Terra / Codex         |       ✓       |       ✓       |         ✓        |        ✓       |     ✓     |       ✓       |            ✓           |           ✓          |         ✓        |           ✓           |
| GPT-5.6 Sol / Codex           |       ✓       |       ✓       |         ✓        |        ✓       |     ✓     |       ✓       |            ✓           |           ✓          |         ✓        |           ✓           |
| Grok 4.5 / Cursor             |       ✓       |       ✓       |         ✓        |        ✓       |     ✓     |       ✓       |            ✓           |           ✓          |         ✓        |           ✓           |
| Composer 2.5 / Cursor         |       ✓       |       ✓       |         ✓        |        ✓       |     ✓     |       △       |            ✗           |           ✓          |         ✓        |           ✓           |
| Claude Sonnet 5 / Claude Code |       ✓       |       ✓       |         ✓        |        ✓       |     ✓     |       ✓       |            ✗           |           ✓          |         ✓        |           ✓           |
| Claude Opus 5 / Claude Code   |       ✓       |       ✓       |         ✓        |        ✓       |     ✓     |       ✓       |            ✗           |           ✓          |         ✓        |           △           |

AC-7で差が大きくなりました。単に「request headerやbodyを明示的にログへ渡していない」だけでなく、**例外オブジェクトそのものをloggerへ渡しているか**を確認しています。例外messageにpassword、JWT、connection string等が含まれていれば、JSON consoleの`Exception`欄から漏えいするためです。

---

# 4. 実務効率

公式式は、処理時間8の`dsv4flash`を最速100として計算しています。

| Rank | Model             | Agent       | Coding | Time |      Q/T |  Speed | Practical |
| ---: | ----------------- | ----------- | -----: | ---: | -------: | -----: | --------: |
|    1 | **GPT-5.6 Terra** | **Codex**   |     95 |   14 | **6.79** |  57.14 | **91.21** |
|    2 | **GPT-5.6 Sol**   | **Codex**   |     96 |   17 |     5.65 |  47.06 | **91.11** |
|    3 | **Grok 4.5**      | **Cursor**  |     92 |   14 |     6.57 |  57.14 | **88.51** |
|    4 | GPT-5.6 Luna      | Codex       |     94 |   27 |     3.48 |  29.63 | **87.56** |
|    5 | GPT-5.6 Luna      | Open Code   |     84 |   29 |     2.90 |  27.59 | **78.36** |
|    6 | Claude Sonnet 5   | Claude Code |     80 |   24 |     3.33 |  33.33 | **75.33** |
|    7 | DeepSeek V4 Flash | Open Code   |     80 |   24 |     3.33 |  33.33 | **75.33** |
|    8 | Claude Opus 5     | Claude Code |     72 |   31 |     2.32 |  25.81 | **67.38** |
|    9 | Composer 2.5      | Cursor      |     70 |   24 |     2.92 |  33.33 | **66.33** |
|   10 | MiniMax M3        | Open Code   |     64 |   11 |     5.82 |  72.73 | **64.87** |
|   11 | Qwen3.7 Plus      | Open Code   |     66 |   30 |     2.20 |  26.67 | **62.07** |
|   12 | MiMo-V2.5         | Open Code   |     55 |   24 |     2.29 |  33.33 | **52.83** |
|   13 | MiMo-V2.5-Pro     | Open Code   |     56 |   35 |     1.60 |  22.86 | **52.69** |
|   14 | DeepSeek V4 Pro   | Open Code   |     46 |   39 |     1.18 |  20.51 | **43.45** |

### 速度評価上の注意

`dsv4flash`は実装を行っていないため、Speed Score 100に実務的な意味はありません。その値を基準にすることで、他候補のSpeed Scoreが一律に低くなっています。

実装成立候補だけに限定すれば最速はMiniMax M3の11ですが、指定式を変更せず、表では全14候補を母集団にしています。上位の順位自体は変わらず、Terra、Sol、Grokが実務効率上の上位です。

---

# 5. Candidate詳細

## Candidate 1 — DeepSeek V4 Pro / Open Code

**Head:** `869e75d8...`
**PR:** #67 Draft
**CI:** SUCCESS — run `31214309925`

**Score:** A 13 / B 9 / C 7 / D 3 / E 3 / F 5 / G 5 / H 1 = **46**

**Strengths:** mapper interface、correlation middleware、TimeProvider登録、JSON console設定という基本要素は存在します。

**Weaknesses:** WebApplicationFactoryによるrequest-level HTTP検証を断念し、middleware直接呼出しへ後退しています。API error型をDomainへ置き、test用controllerをproduction assemblyへ常設しています。

**Findings:**
Critical: なし。
Major: HTTP統合テスト欠落。`log-sensitive` endpointがpassword、JWT、signing key、idempotency key、connection stringをログへ流す経路をproduction側に持つ。正規表現redactorは構造化ログの全形式を安全に覆いません。
Minor: response startedの扱いが弱い。未使用のMvc.Testing packageが残る。
Nit: なし。

**Best contribution:** 初期的なexception mapper形状、TimeProvider注入方針。

**Do not carry forward:** `RedactingTextWriter`、Domain層のAPI契約型、productionの検証controller。

**Post-Implementation Notes:** HTTP統合未実施、redactorの限界、本番無効化されないtest endpointを自ら明記しています。

**Self-awareness:** HIGH
**Verdict:** WEAK

実際のcontrollerにはcredential-like値を出力するendpointがあり、単なる仮説ではありません。
実装者自身もHTTP統合欠落とredaction限界を認識しています。

---

## Candidate 2 — Qwen3.7 Plus / Open Code

**Head:** `e2233f64...`
**PR:** #70 Draft
**CI:** SUCCESS — run `31216844889`

**Score:** A 17 / B 12 / C 10 / D 6 / E 6 / F 6 / G 8 / H 1 = **66**

**Strengths:** 15 filesに収まった比較的簡潔な構成です。caller IDの文字種・長さ制限、TimeProvider差替え、共通envelopeは実装されています。

**Weaknesses:** productionのProgramを通さず、テスト側でmiddleware pipelineを再構築しています。したがって本番配線の回帰検出力が弱いです。

**Findings:**
Critical: なし。
Major: exception objectをloggerへ渡す。mapped exceptionの非500応答に`exception.Message`を使用するため、mapper利用時に内部詳細がresponseへ出る余地がある。`api/test/*`が全環境で公開される。
Minor: static `AsyncLocal` accessorをrequest終了時にclearしない。
Nit: なし。

**Best contribution:** bounded allow-list型correlation ID validation。

**Do not carry forward:** exception object logging、`exception.Message`を応答に使うmapper、production test controller、static accessor。

**Post-Implementation Notes:** runtime scrubberを採用しなかったことと、test endpointsが全環境で利用可能なことを明示しています。

**Self-awareness:** HIGH
**Verdict:** WEAK

middlewareはloggerへ例外を渡し、500以外では例外messageを応答に採用しています。
さらにtest controllerはproduction assemblyにあり、環境制限がありません。
テストはproduction Programではなく独自HostBuilderでpipelineを再構築しています。

---

## Candidate 3 — GPT-5.6 Luna / Open Code

**Head:** `86eeacac...`
**PR:** #72 Draft
**CI:** SUCCESS — run `31220034464`

**Score:** A 21 / B 14 / C 12 / D 8 / E 9 / F 8 / G 8 / H 4 = **84**

**Strengths:** WebApplicationFactory経由の実HTTPテスト、実JSON console parse、fake TimeProvider、validation envelope、秘密値sentinel検査を持ちます。例外オブジェクトをloggerへ渡さない設計です。

**Weaknesses:** 未マップ例外全般に、仕様上は特定のデータ不整合を意味する`data_integrity_violation`を固定適用しています。

**Findings:**
Critical: なし。
Major: generic runtime failureへbusiness意味を持つerror codeを流用している。Issue #40の「business mappingを先取りしない」に反する。
Minor: cancellationも500に変換する可能性がある。
Nit: なし。

**Best contribution:** safe structured logging、実JSON console test、model validationの共通envelope。

**Do not carry forward:** generic fallbackとしての`data_integrity_violation`。

**Post-Implementation Notes:** generic 500 codeがないため既存500 codeを使ったと説明していますが、この判断自体が採点上の主要欠陥です。

**Self-awareness:** MEDIUM
**Verdict:** GOOD（修正必須）

generic fallbackが実際に`data_integrity_violation`へ固定されています。
一方、technical loggingは例外本文をloggerへ渡さず、安全性は高い実装です。
テスト品質は上位候補に近い水準です。

---

## Candidate 4 — DeepSeek V4 Flash / Open Code

**Head:** `9181417fb806b574d4ba664af5595ab0b77fcb9f`
**PR:** #81 Open / Draft
**CI:** SUCCESS — run `31228219484`

**Score:**

* A Issue達成度: 20 / 25
* B 正しさ・実行可能性: 15 / 15
* C Scope遵守: 14 / 15
* D 設計・Repository適合性: 8 / 10
* E テスト・検証品質: 7 / 10
* F コード品質・保守性: 8 / 10
* G 変更精度・最小性: 7 / 10
* H エラー・リスク管理: 1 / 5
* **Total: 80 / 100**

### Strengths

* common baseから完全に独立した1 commit
* production `Program`を通すrequest-level integration test
* exact `{code,message}` envelope
* model validationを`validation_failed` envelopeへ統一
* caller correlation IDのbounded allow-list
* fake TimeProviderによる決定的検証
* `OperationCanceledException`を500へ変換せずrethrow
* response started後の例外を新規envelopeで上書きしない
* framework request-start logからquery stringが出ることをテストで発見し、カテゴリfilterで抑制

### Weaknesses

* exception objectをactual JSON console loggerへ渡す
* capture loggerがexceptionを落とすため、security testが実漏えいを検出できない
* test-only routesをproduction `Program`内に保持
* 22 files / +783行で、TerraやSolより変更規模が大きい
* mapped error messageの安全性が`ApiException`呼出側の規律に依存

### Findings

* Critical: なし
* Major: 1件 — exception object経由のtechnical log secret漏えい
* Minor: 2件 — mapped message安全性、actual JSON console未検証
* Nit: なし

### Best contribution

* `OperationCanceledException`の明示的pass-through
* ModelState validationの共通error envelope化
* `Microsoft.AspNetCore.Hosting.Diagnostics`のrequest-line log抑制
* WSL UNC環境でもproduction Programを起動するWebApplicationFactory fixture

### Do not carry forward

* `Exception`引数付きのtechnical log
* actual console出力の代替として独自JSON providerだけを検査する方式
* `ApiException.Message`を無条件でresponseへ採用する設計
* test-only business codeをproduction `Program`へ直接記述する構成

### Post-Implementation Notes

実装者は、WSL UNC上のhost起動ハング、capture loggerのserialization失敗、framework request logへのquery string混入を実際のtest failureから発見・修正しています。この部分の検証姿勢は良好です。

一方、exception message内のsecretを「developer制御領域」として許容しており、今回のsecurity contractを狭く解釈しています。

### Self-awareness

**MEDIUM**

運用・環境上の問題認識は高いものの、主要なsecurity欠陥を認識したうえで対象外と判断しています。

### Verdict

**ACCEPTABLE — Major修正必須**

---

## Candidate 5 — MiMo-V2.5 / Open Code

**Head:** `413a955b...`
**PR:** #74 Draft
**CI:** SUCCESS — run `31221412552`

**Score:** A 12 / B 9 / C 14 / D 5 / E 5 / F 6 / G 4 / H 0 = **55**

**Strengths:** business featureを先取りせず、TimeProvider、error envelope、TestServer fixtureを実装しています。

**Weaknesses:** caller correlation IDを「非空かつ128文字以下」だけで受理します。改行、control character、log injection文字列をそのまま採用可能です。

**Findings:**
Critical: なし。
Major: AC-4不達。exception objectをtechnical logへ渡す。exception-to-HTTP mapping extension pointがない。
Minor: response開始後の例外を記録して握りつぶす。actual JSON console出力を検証していない。production pipelineを通していない。
Nit: なし。

**Best contribution:** 小規模なTestServer fixture、fake TimeProviderの使い方。

**Do not carry forward:** `ResolveCorrelationId`の長さだけの検証、exception logging、独自test pipelineだけによる検証。

**Post-Implementation Notes:** actual console JSONを検証できていないことを認識しています。

**Self-awareness:** HIGH
**Verdict:** WEAK

caller値は文字種検査なしで受理されます。
例外オブジェクトのlogger渡しと、response started後の例外をrethrowしない問題もあります。

---

## Candidate 6 — MiMo-V2.5-Pro / Open Code

**Head:** `bc27e5e0...`
**PR:** #78 Draft
**CI:** SUCCESS — run `31222600990`

**Score:** A 14 / B 10 / C 9 / D 5 / E 4 / F 6 / G 8 / H 0 = **56**

**Strengths:** 13 filesと比較的少ない変更で、Program、TimeProvider、correlation、error envelopeを構成しています。

**Weaknesses:** テスト数31に対し、request-levelの実HTTP pipeline検証がありません。テスト数が契約実証力に直結していません。

**Findings:**
Critical: なし。
Major: exception objectをloggerへ渡す。mapping extension point欠落。production assemblyへ検証controllerを常設。AC-10不達。
Minor: response started後の例外を握りつぶす。API error codeをDomainへ置く。
Nit: なし。

**Best contribution:** 基本的なProgram配線、TimeProvider登録。

**Do not carry forward:** Domain側のAPI error catalog、production verification controller、middleware直接テストのみの構成。

**Post-Implementation Notes:** WebApplicationFactoryが利用できず、full HTTP cycleを未検証と明記しています。

**Self-awareness:** HIGH
**Verdict:** WEAK

exception objectをloggerへ渡し、response started後にもrethrowしない構造です。
検証controllerはproduction assemblyへ常設されています。

---

## Candidate 7 — MiniMax M3 / Open Code

**Head:** `4e6a7b32...`
**PR:** #76 Draft
**CI:** SUCCESS — run `31222258404`

**Score:** A 15 / B 10 / C 14 / D 6 / E 6 / F 6 / G 6 / H 1 = **64**

**Strengths:** caller IDのallow-list、overlength拒否、response started時のrethrow、test-only endpoint分離は妥当です。処理時間11は実装成立候補で最速です。

**Weaknesses:** production Programを直接通さず、test project側でhostを構成しています。actual JSON lineの検証もありません。

**Findings:**
Critical: なし。
Major: exception objectをJSON loggerへ渡す。exception mapping extension pointがない。
Minor: integration testがproduction wiringを証明しない。test projectだけのwarning抑制が多い。
Nit: なし。

**Best contribution:** caller correlation ID policy、TestServer起動fixture。

**Do not carry forward:** exception logger、test-only pipelineをproduction verificationの代替にする構成。

**Post-Implementation Notes:** actual console byte列が未検証であることを認識していますが、例外経由の漏えいは認識していません。

**Self-awareness:** HIGH
**Verdict:** WEAK

例外オブジェクトを`LoggerMessage`へ渡すため、例外messageとstack traceがtechnical logへ出ます。

---

## Candidate 8 — GPT-5.6 Luna / Codex

**Head:** `b7cb2a54...`
**PR:** #66 Draft
**CI:** SUCCESS — run `31212793320`

**Score:** A 24 / B 15 / C 15 / D 9 / E 9 / F 9 / G 9 / H 4 = **94**

**Strengths:** production ProgramをWebApplicationFactoryで通し、error、correlation、fake time、JSON provider、秘密値非露出を検証しています。mapperが例外を投げてもgeneric fallbackへ戻す防御もあります。

**Weaknesses:** request middlewareが`OperationCanceledException`もgeneric 500として扱う可能性があります。

**Findings:**
Critical: なし。
Major: なし。
Minor: cancellation専用の扱いがない。実JSON testの一部は独立logger factoryであり、error pipelineそのもののconsole lineではない。
Nit: productionの`runtime-contract/ping`はIssue上許容されるnon-business endpointですが、finalではtest-only化できる。

**Best contribution:** mapper失敗時のfallback、request契約を一責任にまとめたmiddleware、elapsed timeへのTimeProvider利用。

**Do not carry forward:** 常設probe endpointは必須でなければ削除。

**Post-Implementation Notes:** testhostの問題、JSON console検証方式、既知制約を具体的に記録しています。

**Self-awareness:** HIGH
**Verdict:** EXCELLENT

実HTTP、safe envelope、correlation、fake time、prohibited fieldを一つのfixtureで検証しています。
technical logは例外オブジェクトを受け取らず、固定fieldだけを出力します。

---

## Candidate 9 — GPT-5.6 Terra / Codex

**Head:** `c5e5f782...`
**PR:** #68 Draft
**CI:** SUCCESS — run `31215597642`

**Score:** A 24 / B 15 / C 15 / D 9 / E 9 / F 9 / G 10 / H 4 = **95**

**Strengths:** 12 files、+409行で、完全性と最小性のバランスが最良です。実HTTP pipeline、実JSON console capture、exact envelope、GUID validation、fake time、例外message内の秘密値sentinelまで検証しています。

**Weaknesses:** mapper extension point自体をmapped exceptionで直接実証するテストはSolより弱いです。

**Findings:**
Critical: なし。
Major: なし。
Minor: cancellationを通常500へ変換する可能性。test controllerはTesting環境限定ですがproduction assembly内に存在する。
Nit: なし。

**Best contribution:** 最小のproduction wiring、実console JSON capture、例外型名だけを記録するsafe logger。

**Do not carry forward:** finalではtest controllerをtest assembly側へ移動すると、さらに境界が明確になる。

**Post-Implementation Notes:** stdout/stderr captureの失敗を実出力先に合わせて修正しており、試験結果と実装の整合が高いです。

**Self-awareness:** HIGH
**Verdict:** EXCELLENT

テストは実際のWebApplicationFactoryを通し、JSON lineをparseし、例外messageに埋め込んだ5種のsentinelがログへ出ないことを検証しています。

---

## Candidate 10 — GPT-5.6 Sol / Codex

**Head:** `e9457cbc...`
**PR:** #71 Draft
**CI:** SUCCESS — run `31219084120`

**Score:** A 25 / B 15 / C 15 / D 9 / E 10 / F 9 / G 9 / H 4 = **96**

**Strengths:** 全ACを最も直接的に実証しています。production Programを通すWebApplicationFactory、test assemblyだけに存在するcontroller、mapped/unmapped両方のexception、exact envelope、request/response/logのcorrelation一致、実JSON parse、header/body/exception内の秘密値sentinel、fake TimeProviderを持ちます。

**Weaknesses:** custom mapper自身が例外を投げた場合のfallbackはLuna/Codexほど防御的ではありません。

**Findings:**
Critical: なし。
Major: なし。
Minor: cancellationが500になる可能性。mapper failure testがない。
Nit: なし。

**Best contribution:** test assemblyの`ApplicationPart`によるtest-only controller、allow-list technical log、mapper extension pointの実HTTP検証、秘密値sentinel設計。

**Do not carry forward:** 実質的にありません。cancellationとmapper failureの2テストを追加すればよい構成です。

**Post-Implementation Notes:** `Response.Clear()`がcorrelation headerを消す不具合をテストで検出し、修正しています。テストが実際に契約破壊を捕捉した証拠です。

**Self-awareness:** HIGH
**Verdict:** EXCELLENT

middlewareは例外messageやstackをloggerへ渡さず、固定error code、status、correlation ID、exception typeに限定しています。
テストではmapped/unmapped、秘密値、actual JSON、TimeProviderを実HTTP経由で直接検証しています。
修正過程も具体的に記録されています。

---

## Candidate 11 — Grok 4.5 / Cursor

**Head:** `70f736c1...`
**PR:** #65 Draft
**CI:** SUCCESS — run `31212480399`

**Score:** A 24 / B 15 / C 15 / D 9 / E 10 / F 8 / G 7 / H 4 = **92**

**Strengths:** `IExceptionHandler`、mapper registry、configで既定無効のcontract probes、caller IDの改行・tab・overlengthテスト、実JSON console、例外message内sentinelの非露出まで高水準です。

**Weaknesses:** 21 files・+936行で、Sol/Terraより分割とboilerplateが多いです。

**Findings:**
Critical: なし。
Major: なし。
Minor: cancellation専用扱いなし。production assembly内にconfig-gated probe infrastructureを置く。
Nit: 一部の型・fixture分割はIssue規模に対して細かすぎる。

**Best contribution:** correlation policyと悪意入力テスト、mapper registry、actual console log-content test。

**Do not carry forward:** 過度なファイル分割、production assembly側のprobe実装。

**Post-Implementation Notes:** generic fallback、probe有効化条件、scope境界を明示しています。

**Self-awareness:** HIGH
**Verdict:** EXCELLENT

exception handlerは例外型名のみをログへ渡し、本文を渡していません。
prohibited field testsはrequest header/bodyだけでなく、例外message内のsentinelも対象にしています。
correlationとactual JSON consoleの一致も検証されています。

---

## Candidate 12 — Composer 2.5 / Cursor

**Head:** `aaf6ae84...`
**PR:** #69 Draft
**CI:** SUCCESS — run `31216486765`

**Score:** A 19 / B 13 / C 13 / D 7 / E 7 / F 7 / G 3 / H 1 = **70**

**Strengths:** mapper registry、Program、WebApplicationFactory、correlation、TimeProvider、prohibited field policyを幅広く実装しています。

**Weaknesses:** 29 files・+981行で、Issue規模に対して重い独自logger providerを導入しています。

**Findings:**
Critical: なし。
Major: sanitizerはexception objectをそのままinner loggerへ渡す。LoggerMessage stateが`Dictionary`でない場合はsanitizationを通らないため、例外message・stackの漏えいを防げない。
Minor: raw JSON console pipelineのテストを削除し、provider wiringと独自serializationへ後退。
Nit: `.editorconfig`変更を含む。

**Best contribution:** mapper registryの整理、prohibited field名称の体系化。

**Do not carry forward:** `ProhibitedFieldSanitizingLoggerProvider`、二重logger factory、過剰なruntime redaction abstraction。

**Post-Implementation Notes:** actual stdout JSONを検証できていないことは認識していますが、exception経由の漏えいを認識していません。

**Self-awareness:** MEDIUM
**Verdict:** WEAK

sanitizerは一部stateだけを対象にし、exceptionは加工せずinner loggerへ渡します。
実際のLoggerMessage定義もException引数を持っています。

---

## Candidate 13 — Claude Sonnet 5 / Claude Code

**Head:** `395e1e85...`
**PR:** #80 Draft
**CI:** SUCCESS — run `31222956728`

**Score:** A 20 / B 14 / C 15 / D 8 / E 8 / F 8 / G 6 / H 1 = **80**

**Strengths:** ASP.NET Core標準`IExceptionHandler`、test assemblyだけのcontroller、実JSON console capture、fake TimeProvider、bounded correlation ID、exact envelopeを備えます。構造自体は上位候補に近いです。

**Weaknesses:** safe logging policyを設けながら、global exception loggerだけは例外オブジェクトを受け取っています。

**Findings:**
Critical: なし。
Major: JSON consoleへexception message・stack traceが出る。AC-7不達。既存testは`internal_error`とcorrelation IDを確認するだけで、例外messageのsentinel非露出を確認していない。
Minor: sensitive policyが一部call siteだけに適用される。
Nit: なし。

**Best contribution:** test-only ApplicationPart controller、`CurrentTimeReader`、actual JSON console fixture。

**Do not carry forward:** `GlobalExceptionHandlerLog`のException引数。

**Post-Implementation Notes:** policyがblanket interceptorではないことを認識していますが、global exception loggingの実漏えいを見落としています。

**Self-awareness:** MEDIUM
**Verdict:** ACCEPTABLE（Major修正必須）

global handlerはexceptionをloggerへ渡します。
LoggerMessage定義にもException引数があります。
JSON testはparseabilityとcode/correlationだけを確認し、exception secretを検査していません。

---

## Candidate 14 — Claude Opus 5 / Claude Code

**Head:** `f40c6046...`
**PR:** #79 Draft
**CI:** SUCCESS — run `31222766908`

**Score:** A 18 / B 11 / C 14 / D 7 / E 9 / F 7 / G 5 / H 1 = **72**

**Strengths:** 44 testsで、model-binding envelope、bare status behavior、malicious correlation、actual JSON、prohibited fields、mapper extensionなどを広範囲に検証しています。test endpointをtest assembly側へ隔離しています。

**Weaknesses:** 30 files・+1390行と最大規模です。production Api projectは`OutputType=Library`のままでProgramを持たず、契約が実際のproduction hostへ組み込まれていません。

**Findings:**
Critical: なし。
Major: unmapped exception objectをloggerへ渡すため秘密値漏えい。production wiring不成立。
Minor: `SuppressMapClientErrors`によりbare 404等が共通envelopeではなく空bodyになる。テスト全体の並列実行を無効化。
Nit: 抽象・fixture・DTOの分割が多い。

**Best contribution:** model-binding errorの共通envelope、correlation悪意入力テスト、test-only controller構成。

**Do not carry forward:** exception object logging、Programなしのtest-only architecture、30 filesの過剰分割。

**Post-Implementation Notes:** exception objectに秘密値が含まれればログへ出ること、production Kestrel未検証を自ら明記しています。

**Self-awareness:** HIGH
**Verdict:** WEAK

technical log eventは明示的にException引数を持ちます。
middlewareからその例外を渡しています。
さらにApi projectはLibraryのままでproduction entry pointを持ちません。

---

# 6. ランキング

| Category                          | 1位                    | 2位                    | 3位                     |
| --------------------------------- | --------------------- | --------------------- | ---------------------- |
| **Best Coding Quality**           | GPT-5.6 Sol / Codex   | GPT-5.6 Terra / Codex | GPT-5.6 Luna / Codex   |
| **Best Issue Adherence**          | GPT-5.6 Sol / Codex   | GPT-5.6 Terra / Codex | GPT-5.6 Luna / Codex   |
| **Best Scope Discipline**         | GPT-5.6 Sol / Codex   | GPT-5.6 Luna / Codex  | GPT-5.6 Terra / Codex  |
| **Best Test Quality**             | GPT-5.6 Sol / Codex   | Grok 4.5 / Cursor     | GPT-5.6 Terra / Codex  |
| **Best Minimality**               | GPT-5.6 Terra / Codex | GPT-5.6 Sol / Codex   | GPT-5.6 Luna / Codex   |
| **Best Security / Risk Handling** | GPT-5.6 Sol / Codex   | GPT-5.6 Terra / Codex | Grok 4.5 / Cursor      |
| **Best Practical Performance**    | GPT-5.6 Terra / Codex | GPT-5.6 Sol / Codex   | Grok 4.5 / Cursor      |
| **Best Quality / Time**           | GPT-5.6 Terra / Codex | Grok 4.5 / Cursor     | MiniMax M3 / Open Code |
| **Best Agent / Harness Result**   | GPT-5.6 Sol / Codex   | GPT-5.6 Terra / Codex | Grok 4.5 / Cursor      |

MiniMax M3はQ/Tでは3位ですが、Coding Score 64かつMajor findingsありです。**Q/Tは品質下限を保証しない**ため、主力候補にはなりません。

---

# 7. Agent / Harness集計

分散はCoding Scoreの母分散です。

| Agent / Harness |  n |   Average | Median | Min | Max |  Avg Time | Variance | Range |
| --------------- | -: | --------: | -----: | --: | --: | --------: | -------: | ----: |
| **Codex**       |  3 | **95.00** |     95 |  94 |  96 | **19.33** | **0.67** | **2** |
| **Cursor**      |  2 |     81.00 |     81 |  70 |  92 |     19.00 |   121.00 |    22 |
| **Claude Code** |  2 |     76.00 |     76 |  72 |  80 |     27.50 |    16.00 |     8 |
| **Open Code**   |  7 |     53.00 |     56 |   0 |  84 |     25.14 |   588.86 |    84 |

### 集計の読み方

Codexの3候補は94～96に集中しており、この試行では非常に安定しています。一方、Open Codeは0～84と幅が大きいです。

ただし、これはHarness単体の純粋性能ではありません。

* Codex側はGPT-5.6 Luna / Terra / Solだけ
* Open Code側はDeepSeek、Qwen、GPT、MiMo、MiniMaxを含む
* モデル、effort、tool availability、環境障害が異なる
* 各組合せ1試行だけ

したがって、**「Codexなら常に95点、Open Codeなら53点」と一般化してはいけません。**

なお、未実装のDeepSeek V4 Flashを除外すると、Open Codeは次の値になります。

* n: 6
* Average: 61.83
* Median: 60
* Min / Max: 46 / 84
* Average Time: 28.00
* Variance: 140.81
* Range: 38

それでも今回の#40ではCodex群との差は明確です。

---

# 8. GPT-5.6 Luna — Open Code vs Codex

| 項目           | Open Code + Luna | Codex + Luna |
| ------------ | ---------------: | -----------: |
| Coding Score |               84 |       **94** |
| A Issue達成    |               21 |       **24** |
| C Scope      |               12 |       **15** |
| E Test       |                9 |            9 |
| Files        |               14 |           14 |
| +/-          |          +521/-4 |      +544/-1 |
| Tests        |               16 |           12 |
| CI           |          SUCCESS |      SUCCESS |
| Time         |               29 |       **27** |
| Practical    |            78.36 |    **87.56** |

### 実質差

両者ともWebApplicationFactory、actual JSON、fake TimeProvider、correlation testを備えており、テストの表面的な充実度は近いです。

決定的な差は設計判断です。

* Open Code版はgeneric unmapped exceptionを`data_integrity_violation`へ割り当てた
* Codex版はbusiness semanticsを持たない`internal_error`を局所fallbackにした
* Codex版はmapper自身の失敗もgeneric fallbackへ戻す
* Codex版は同等以上の契約をほぼ同じ変更規模で構成した
* Codex版は処理時間も2短い

この試行では、同一モデルでも**Codex側がIssue追従、Scope判断、安全なfallback設計で明確に優位**でした。ただし1試行であり、差をHarnessだけの因果と断定はできません。

---

# 9. Final synthesis recommendation

## Base architecture

* **GPT-5.6 Sol / Codex**のproduction Program、middleware、test assembly ApplicationPart構成を基礎にする。
* ファイル配置と最小性は**GPT-5.6 Terra / Codex**へ寄せる。

## Error contract

* Solのexact `{ code, message }` assertionを採用。
* generic unmapped exceptionには非業務の`internal_error`を使用。
* Luna/Open Codeの`data_integrity_violation`流用は採用しない。

## Exception mapping

* Solの`IExceptionToHttpMapper`形状を採用。
* Luna/Codexの「mapper自身が失敗した場合もgeneric fallback」の防御を追加。
* `OperationCanceledException`か`RequestAborted`起因のcancelは通常500へ変換しない。

## Correlation

* Grokのsingle-value、bounded allow-list、改行・tab・control・overlength・multiple value拒否テストを採用。
* caller値を信用できない場合は新規GUIDを生成。
* 拒否したraw値はログへ出さない。
* 許容形式を製品仕様として過度に固定せず、実装局所policyとして保持する。

## TimeProvider

* Sol/Terraの`TimeProvider.System` DI登録とApplication側clock consumerを採用。
* fake providerをWebApplicationFactoryから差し替える。

## Logging

* Sol/Terra/Grokのallow-list log eventを採用。
* `Exception`をlogger APIへ渡さない。
* 記録するのはcorrelation ID、fixed error code、HTTP status、exception type等の安全な診断fieldだけ。
* regex redactorや独自logger wrapperに依存しない。

## Tests

* Solのtest-only controller分離
* Terraのactual JSON console capture
* Grokのmalicious correlation matrix
* Solのmapped mapper実証
* header、body、exception messageそれぞれへ5種のsentinelを埋める
* response started、cancellation、mapper failureの追加テスト

## Minimality

* Terraの12 files / +409程度を目標にする。
* Opusの30 files、Composerの29 filesのような分割は避ける。

## Explicitly reject

* DeepSeek V4 Proの`RedactingTextWriter`
* Qwenの`exception.Message`応答
* Luna/Open Codeのgeneric `data_integrity_violation`
* MiMo系、MiniMax、Composer、Sonnet、Opusのexception object logging
* production常設のtest/sensitive endpoint
* production Programを通さないtest-only pipelineのみの検証
* API contract型をDomainへ置く構成

---

# 10. 最終分析

## 1. 今回の最良実装

**GPT-5.6 Sol + Codex**です。

理由は、単にCI greenやテスト数が多いからではありません。

* Issue #40の全ACを直接検証
* mapped / unmapped双方のextension point実証
* production Programを実際に通す
* test controllerをtest assemblyへ隔離
* actual JSON consoleをparse
* exception、header、bodyを含む秘密値sentinel検査
* exact envelope assertion
* Scope外機能なし
* 11 filesで過剰ではない
* 自己レビューで実バグを検出・修正

Coding Scoreは**96 / 100**です。

## 2. 最も実務向き

公式Practical Scoreでは、**GPT-5.6 Terra + Codex**です。

* Coding: 95
* Time: 14
* Q/T: 6.79
* Practical: 91.21

Solとの差はわずか**0.10点**です。TerraはSolより3早く、12 files / +409と最小です。

したがって、

* 絶対品質・委任安全性を優先：Sol
* 高品質を維持しつつthroughput優先：Terra

という選択になります。

## 3. 最も安全に委任できる候補

**GPT-5.6 Sol + Codex**です。

特に、実装者の主張だけでなく、次の契約をテストが直接実証しています。

* exact error envelope
* generic 500
* mapper extension
* request / response / logのcorrelation
* actual JSON
* fake time
* exception内の秘密値非露出
* test-only endpoint isolation
* CI Head一致

人間が重点確認すべき残りは、cancellationとmapper failure程度です。

## 4. Harness差

同一GPT-5.6 Lunaでは、Codex版がOpen Code版を**10 Coding points**上回り、処理時間も2短い結果でした。

差はコード生成能力そのものというより、次の判断精度に現れています。

* generic fallbackの意味
* business code流用の回避
* mapper失敗時の防御
* Scope境界
* Post-Implementation Notesと実コードの整合

今回の1試行では、Codexのほうが「Issueを狭く正確に読むHarness」として機能しました。

## 5. #39との比較上の注意

Issue #39のcandidate PRは今回の採点資料として参照していません。

したがって、

* 「#39でもCodexが強かった」
* 「今回も同じHarness傾向が再現した」

という比較断定は行いません。

今回確実に言えるのは、**#40の一次証拠だけではCodex 3候補が94～96に集中し、Open Code候補は0～84に分散した**ということです。

## 6. 今後10 Issueを任せるなら

主力として選ぶのは、**GPT-5.6 Sol + Codex**です。

理由:

* quality：今回最高の96
* scope：business、DB、Docker、health等の先取りなし
* test：全ACを壊れ方に即して検証
* security：exception objectをログへ渡さない
* speed：17で上位実装群
* consistency：実装、テスト、CI、notesの矛盾が最小
* delegation：残る確認事項が局所的

Terraは高速サブ主力として有力ですが、今後10 Issueを単一候補へ委任するなら、3の速度差よりSolのmapper検証・secret testの厚さを優先します。

---

# 11. Final-code blueprint

## Foundation

* Solのproduction `Program`とrequest pipelineを基礎にする。
* Terra相当の12～14 files程度へ整理する。
* API projectを実行可能hostとして成立させる。
* business endpoint、DB、health、authenticationは追加しない。

## Error envelope

* JSON propertyは`code`と`message`の2つ。
* serializer naming policyに依存しすぎず、テストでproperty集合を固定する。
* generic unmapped fallbackはHTTP 500 / `internal_error` / 安全な固定message。

## Exception mapping

* `IApiExceptionMapper`を複数登録可能にする。
* registration orderを固定する。
* mapperが未所有ならgeneric fallback。
* mapper自身が例外を投げてもgeneric fallback。
* response started後は新しいenvelopeを書かずrethrow。
* cancellationは通常のunmapped 500と区別する。

## Correlation

* 単一header値だけを検討する。
* bounded safe opaque IDだけ受理する。
* 改行、control、overlength、multiple、不正形式は破棄して再生成。
* response header、`HttpContext.Items`、log scopeへ同一値を設定。
* error responseで`Response.Clear()`した後もheaderを再設定する。
* rejected raw caller valueはログへ出さない。

## Caller input handling

* 独自許容形式を仕様上の永続契約として扱わない。
* policyを小さな関数へ閉じ込め、将来変更可能にする。
* malicious input matrixをテストで固定する。

## TimeProvider

* `TimeProvider.System`をDI singleton登録。
* Application層の最小consumerがDI経由で使用する。
* integration testでfake providerへ置換し、HTTP responseから決定的に確認する。
* 独自clock interfaceは追加しない。

## JSON technical logging

* `Microsoft.Extensions.Logging` + `AddJsonConsole`
* scopes有効
* UTC timestamp
* correlation IDを構造化fieldとして記録
* error時はfixed code、HTTP status、exception typeを記録
* `Exception`オブジェクト、message、data、stackをloggerへ渡さない

## Sensitive-data protection

* runtime regex redactorを主防御にしない。
* logging call siteをallow-list化する。
* request headers、body、query string、configurationをtechnical event引数にしない。
* password、JWT、signing key、raw idempotency key、connection stringをsentinelで試験する。
* exception messageにも同じsentinelを埋める。

## Integration test host

* production `Program`をWebApplicationFactoryで起動。
* test-only controllerはIntegrationTests assembly内だけに配置。
* `AddApplicationPart`でtest hostにのみ追加。
* production assemblyへdiagnostic endpointを常設しない。
* actual Console.Out / Console.Errorをcaptureし、各行をJSON parseする。

## Tests to retain

* exact `{code,message}` property集合
* mapped exception status/code/message
* unmapped 500 safe response
* exception message・type・stackのresponse非露出
* generated correlation ID
* accepted caller ID
* newline/control/overlength/multiple caller ID拒否
* request / response / technical log一致
* fake TimeProvider
* actual JSON console parse
* fixed error code存在
* password/JWT/signing key/idempotency key/connection string非露出
* exception message内sentinel非露出
* mapper failure fallback
* response started behavior
* cancellation behavior
* business mapperが初期状態で0件であること

## Files / abstractions NOT to carry forward

* `RedactingTextWriter`
* 独自sanitizing logger provider
* static `AsyncLocal` correlation accessor
* Domain層のHTTP error型
* production test controller
* production sensitive-log endpoint
* generic failureへのbusiness code流用
* `Exception`引数付きLoggerMessage
* test側だけで再構築したproduction非連動pipeline
* 目的の重複する多数のDTO・extension・fixture

## Expected complexity

* **MEDIUM**

## Expected final Coding Score

* **98 / 100**

残る2点は、final-code完成後に実コード、CI、cancellation、response-started、mapper failureを再検証して確定すべき参考値です。
