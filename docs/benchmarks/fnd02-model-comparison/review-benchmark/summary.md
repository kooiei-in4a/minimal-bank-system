以下を、そのまま保存・共有できる「まとめ報告」として整理します。評価は、同一PR・同一プロンプト条件で、ブランドや文章量ではなく、重大問題の検出・誤検知・証拠・Severity・最終判定を100点で比較する方式です。

# Issue #40 FND-02 — AIモデル独立第三者レビュー性能比較 まとめ報告

## 1. 目的

`kooiei-in4a/minimal-bank-system` の Issue #40
**「[FND-02] 共通API実行契約を確立する」** のFinal Synthesis実装に対して、複数のAIモデルへ完全に同一の独立第三者レビュープロンプトを投入し、レビュー能力を比較評価した。

評価対象は、PR #83 / Head `2306c634abc40b4e5330c9492c8bcee8c0d6a5cc` である。対象実装ではbuild/testおよびCIは成功し、23/23テストがPASSしていた。 

評価ではモデルのブランド、一般的な評判、文章量、指摘件数は考慮せず、

* 重大問題を発見できたか
* False Positiveを抑えられたか
* 一次証拠まで確認したか
* Severity判断が適切か
* Issue / Specification / ADRを正しく理解したか
* CI greenを過信していないか
* 実務上のSignal-to-Noiseが高いか
* 最終的なMerge判定が正しいか

を100点満点で採点した。

---

## 2. Reference / Gold Review

独立検証の結果、PR #83にはmerge前に解消が必要な重大問題が4件存在すると判定した。

### G-01 Blocker

**未承認の固定API code `internal_error` を外部契約へ追加している。**

仕様§16.3の固定code一覧には`internal_error`が存在しない。一方、実装ではunmapped exception時のAPI responseとして外部へ返している。

PR本文で「局所infrastructure fallback」と説明しても、外部利用者から観測可能な`code`である以上API契約であり、Kooによる仕様決定が必要である。

### G-02 Major

**404 / 405などframework生成エラーが共通 `{code,message}` envelopeにならない。**

実装が共通化しているのは主に、

* Exception
* ModelState validation

であり、route不一致等のstatus-only errorはASP.NET Core標準の空bodyまたは別形式のresponseとなる。

Issue #40が所有する共通REST error contractとして不完全である。

### G-03 Major

**request abortではない `OperationCanceledException` まで無条件rethrowしている。**

内部timeout等のOCEでもsafe 500 fallbackを通らず、空body 500等の契約外responseになる可能性がある。

client disconnectによるrequest abortと、application内部cancellationを区別する必要がある。

### G-04 Major

**middlewareから逃げた例外がproduction KestrelのJSON logへ詳細を露出する。**

既存integration testは`WebApplicationFactory<Program>`を使用しているものの、標準ではTestServerを使用する。

response-started後などにrethrowされた例外は、実Kestrelではserver側が例外objectをError logへ渡し、JSON formatterがmessage / stack traceを出力する。

したがってTestServer上のsecret sentinel testがPASSしていても、production server境界で同じ保証は成立していなかった。

### Reference Verdict

**REQUEST CHANGES / NOT MERGE READY**

* Blocker: 1
* Major: 3
* Merge Ready: FAIL

---

## 3. 総合ランキング

| 順位 | Model                          | Harness     |     Score |
| -: | ------------------------------ | ----------- | --------: |
|  1 | **Gpt 5.6 Sol xHigh**          | Codex       | **100.0** |
|  2 | **Claude Opus 5 xhigh**        | Claude Code |  **92.5** |
|  3 | **GPT-5.6 Luna**               | Open Code   |  **88.0** |
|  4 | **Chatgpt Opus 5.6 Sol xhigh** | Browser     |  **87.5** |
|  5 | Gpt 5.6 luna xHigh             | Codex       |      82.0 |
|  6 | DeepSeek V4 Flash              | Open Code   |      77.0 |
|  7 | Gpt 5.6 terra xHigh            | Codex       |      75.5 |
|  8 | Claude Sonnet 5 xhigh          | Claude Code |      60.0 |
|  9 | Composer 2.5                   | Cursor      |      54.5 |
| 10 | Grok 4.5 high fast             | Cursor      |      54.0 |
| 11 | Chatgpt Opus 5.5 xhigh         | Browser     |      53.0 |
| 12 | DeepSeek V4 Pro                | Open Code   |      47.5 |
| 13 | chatgpt o2                     | Browser     |      35.0 |
| 14 | MiMo-V2.5-Pro                  | Open Code   |      19.0 |
| 15 | MiMo-V2.5                      | Open Code   |       7.0 |
| 16 | Qwen3.7 Plus                   | Open Code   |       2.0 |
| 17 | MiniMax M3                     | Open Code   |       0.0 |

---

## 4. 上位モデルの評価

### 1位 — Gpt 5.6 Sol xHigh / Codex

**100.0点**

今回の4件の重大root causeをすべて検出した。

特に優れていたのは、

* `internal_error`を単なる実装詳細ではなく未承認API契約と認識
* 実際に404 / 405をprobe
* 非request-abort OCEを独立した問題として検出
* TestServerとproduction Kestrelを区別
* 実Kestrelでsecret付きexceptionのlog露出を再現
* 不要なMinor / Nitを追加しなかった

という点である。

提出結果でもBlocker 1 / Major 3 / Minor 0 / Nit 0とし、Reference Reviewと完全に一致した。

**最終merge gate担当として最も信頼できる結果だった。**

---

### 2位 — Claude Opus 5 xhigh / Claude Code

**92.5点**

重大4件をすべて発見しており、hidden bugの発見能力は1位とほぼ同等だった。

実Kestrelを使った追加検証まで実行し、

* OCE → 空500
* Kestrel exception log
* route mismatch
* TestServerによるfalse assurance

を独立再現している。

一方でMinor 6 / Nit 3まで挙げており、成功request log、correlation policy文書化、ApplicationTime wrapper等、merge blockerではない指摘も多かった。

**深く掘る能力は非常に高いが、最終gateでは人間または別モデルによるfinding整理が望ましい。**

---

### 3位 — GPT-5.6 Luna / Open Code

**88.0点**

次の3件を正しく検出した。

* 未承認`internal_error`
* Kestrel exception log risk
* framework 404のenvelope不一致

TestServerとKestrelの差にも到達している。

一方、非request-abort OCEそのものを独立したHTTP contract問題として切り出せなかった。

**Open Code環境の中では今回最も高いレビュー性能だった。**

---

### 4位 — Chatgpt Opus 5.6 Sol xhigh / Browser

**87.5点**

特に強かったのはframework挙動の解析である。

.NET / ASP.NET Coreの公式実装まで確認し、

* WebApplicationFactory既定がTestServer
* Kestrel Event 13
* JSON Console FormatterによるException出力

という一連のproduction leak経路を正確に構築した。

また、404 envelope問題も検出した。

一方、

* 未承認`internal_error`
* 非abort OCE

を独立findingとして検出できなかった。

**framework内部や障害・security pathのレビューに強い。**

---

## 5. 中位モデルの傾向

### Gpt 5.6 luna / Codex

`internal_error`と404/405の2件を正しく検出した。

誤検知もほぼなく、非常に簡潔だったが、Kestrel / OCE問題を見逃した。

**Precisionは高いがRecall不足。**

### DeepSeek V4 Flash / Open Code

`internal_error`と404/405を検出した。

一方で既存Agent Bレビューを確認した上での評価であり、完全な独立探索という意味ではやや弱い。またKestrel/OCEには到達しなかった。

### Gpt 5.6 terra / Codex

実際に未知routeへアクセスし、404空bodyを確認した。

指摘はこの1件だけで、誤検知はなかった。

**非常に低ノイズだが重大問題の見逃しが多い。**

---

## 6. 下位モデルで発生した典型的な問題

### CI greenを過信

Grok 4.5、Chatgpt Opus 5.5、DeepSeek V4 Pro等は、

* 23/23 test PASS
* JSON console test PASS
* secret sentinel test PASS

を強い安全証拠として扱った。

しかし実際にはtest server境界までしか検証できておらず、production Kestrelで異なる挙動が存在した。

---

### 実装者の説明を再確認しただけ

DeepSeek V4 ProはResult documentの各claimとコードを丁寧に照合した。

しかし、

> 「実装が説明どおりであるか」

は確認しても、

> 「その設計が仕様上・runtime上正しいか」

までは検証できなかった。

---

### Severity判断の失敗

Claude Sonnet 5やComposer 2.5は`internal_error`が仕様に存在しないことを認識していた。

しかし、

> 将来整理すればよい

としてmerge blockerにしなかった。

問題の存在を発見するだけでなく、**今mergeしてよい問題か**を判断できることが重要である。

---

### 過剰レビュー

chatgpt o2はREQUEST CHANGESには到達したものの、

* `urn:uuid:`対応要求
* Domain共通時刻service
* 10MB log対策
* mapping table要求

など、今回のIssueで修正必須ではない項目を大量に提示した。

厳しいレビューと正確なレビューは同義ではない。

---

### 対象SHAの誤認

Qwen3.7 Plusは指定されたHeadではなくbase相当のローカル状態をレビューし、

> FND-02実装が存在しない

と誤判定した。

独立レビューでは、詳細なコード理解以前に、

* Repository
* PR
* Base SHA
* Head SHA
* CI SHA

を固定することが必須である。

---

### レビューを完了できない

MiMo-V2.5はレビュー計画段階でユーザーへ質問して停止した。

MiniMax M3は複数回実行してもレビュー結果を完成できなかった。

この種のAgent benchmarkでは、推論能力だけでなく**最後までタスクを完遂する能力**もHarness込みの性能として重要である。

---

## 7. 最も性能差が出たポイント

今回の結果から、独立第三者レビュー性能を最も分けたのは次の5点だった。

### 1. テストが「何を証明していないか」を考えられるか

23/23 PASSそのものではなく、

> このテストはproduction Kestrelを通っているのか？

と疑えたモデルだけが重大なlog leakへ到達した。

### 2. 正常に実装されたpathではなく逃げ道を見るか

上位モデルは、

* middleware catch外
* response started後
* cancellation
* framework status response
* server最終exception handler

を探索した。

### 3. PR説明よりSpecificationを優先できるか

`internal_error`について、

> PRが「局所code」と説明している

ことを根拠に許容したモデルは失点した。

正本上の承認があるかどうかを確認したモデルが高得点となった。

### 4. テスト名ではなくassertionを読むか

OCE用テストが存在するだけでは不十分で、実際には空500でも例外伝播でもPASS可能だった。

上位モデルはテストの存在ではなく、テストが固定している挙動そのものを確認した。

### 5. False Positiveを抑えられるか

Claude Opusのように重大問題をすべて発見していても、Minor/Nitが多いと実務ではトリアージコストが増える。

今回100点となったCodex Solは、

> **重大4件を全部発見し、それ以外をほぼ出さない**

という最も理想的な結果だった。

---

## 8. 実務用途別推奨

### 最終Merge Gate

**Gpt 5.6 Sol xHigh / Codex**

重大問題のRecallとFalse Positive抑制の両方で最良。

### Hidden Bug / 障害系探索

**Claude Opus 5 xhigh / Claude Code**

広い探索、probe、framework境界検証に強い。

### Framework / Securityレビュー

**Chatgpt Opus 5.6 Sol xhigh / Browser**

公式sourceまで追跡する検証能力が高い。

### Open Codeで利用する場合

**GPT-5.6 Luna**

Open Code対象モデルの中では最も安定した結果。

---

## 9. 推奨レビュー構成

### 1モデルだけ利用する場合

**Gpt 5.6 Sol xHigh / Codex**

をAgent Bの第一候補とする。

今回のベンチマークでは、

* Recall
* Precision
* Severity
* Evidence
* Final Verdict
* Signal-to-Noise

の全項目で最も安定した。

### 2モデルを利用する場合

推奨構成は、

**Claude Opus 5 xhigh / Claude Code
→ Gpt 5.6 Sol xHigh / Codex**

とする。

役割は、

**Claude Opus**

* adversarial exploration
* hidden failure path探索
* 実環境probe
* framework内部まで深掘り

**Codex Sol**

* findingの事実確認
* Severity正規化
* False Positive除去
* 最終Merge Gate

と分担する。

単純な「1位＋2位」ではなく、

> **探索力のClaude + 精度のCodex**

という補完関係を利用する構成である。

---

## 10. 最終結論

今回の比較では、同一条件・同一プロンプトでもレビュー品質には非常に大きな差が確認された。

特に、

> **CIが成功していることと、production contractが正しいことは別問題**

であることを認識できるかが重要だった。

### Final Result

```text
Best Overall Reviewer:
Gpt 5.6 Sol xHigh / Codex

Best Deep Bug Finder:
Claude Opus 5 xhigh / Claude Code
（Codex Solも同等水準）

Best Precision Reviewer:
Gpt 5.6 Sol xHigh / Codex

Best Specification Reviewer:
Gpt 5.6 Sol xHigh / Codex
GPT-5.6 Luna / Open Code

Best Framework / Security Reviewer:
Chatgpt Opus 5.6 Sol xhigh / Browser

Best Open Code Reviewer:
GPT-5.6 Luna / Open Code

Recommended Agent B:
Gpt 5.6 Sol xHigh / Codex

Recommended Two-Model Configuration:
Claude Opus 5 xhigh / Claude Code
        ↓
Gpt 5.6 Sol xHigh / Codex
```

今回のベンチマークから得られる最大の示唆は、

**優れたAIレビューアとは「多く指摘するモデル」ではなく、「CIや既存テストが見逃した本当に修正すべき問題を発見し、不要な指摘を出さないモデル」である。**

という点である。

必要なら次に、これをさらに圧縮して **「経営・非技術者向け1ページ版」** または **「GitHub Issueへ貼る実験結果版」** にできます。
