# AIコーディングモデル 実装比較・能力評価

以下は、同一の GitHub Issue `#39 [FND-01]` を複数のAIモデル／コーディングエージェントに実装させた実験結果です。

各実装の **Git branch / Head commit を直接確認し、実際の差分・テスト・設計・Issue適合性を一次証拠として比較評価**してください。

モデルの評判や公開ベンチマークだけで評価してはいけません。

---

# 1. Repository / Target

```yaml
REPOSITORY: kooiei-in4a/minimal-bank-system
TARGET_ISSUE: 39
TARGET_TITLE: "[FND-01] Solution・project・build/test CIを確立する"
BASE_BRANCH: main
```

最初に Issue #39、`AGENTS.md`、関連仕様・ADR・計画を確認し、Issue #39 が要求している実装範囲とAcceptance Criteriaを確定してください。

その後、各branch / Headを同じ基準で比較してください。

---

# 2. 評価対象

| # | Issue | Branch | Head | Agent | Model | Effort | 処理時間 |
|---:|---|---|---|---|---|---|---:|
| 1 | #39 FND-01 | `agent/issue-39-fnd-01-dsv4pro` | `14ae134` | Open Code | DeepSeek V4 Pro | Max | 6 |
| 2 | #39 FND-01 | `agent/issue-39-fnd-01-qwen3.7-plus` | `c7980b9` | Open Code | Qwen3.7 Plus | Max | 12 |
| 3 | #39 FND-01 | `agent/issue-39-fnd-01-gpt5.6-luna` | `131f9a7` | Open Code | GPT-5.6 Luna | Max | 15 |
| 4 | #39 FND-01 | `agent/issue-39-fnd-01-dsv4flash` | `c1f7c37` | Open Code | DeepSeek V4 Flash | Max | 10 |
| 5 | #39 FND-01 | `agent/issue-39-fnd-01-mimo-v2.5` | `ba2c72d` | Open Code | MiMo-V2.5 | 未指定 | 12 |
| 6 | #39 FND-01 | `agent/issue-39-fnd-01-mimo-v2.5-pro` | `01a6d00` | Open Code | MiMo-V2.5-Pro | 未指定 | 9 |
| 7 | #39 FND-01 | `agent/issue-39-fnd-01-minimax-m3` | `2849450` | Open Code | MiniMax M3 | Thinking | 17 |
| 8 | #39 FND-01 | `agent/issue-39-fnd-01-gpt5.6-luna-codex` | `eafa631` | Codex | GPT-5.6 Luna | Xhigh | 14 |
| 9 | #39 FND-01 | `agent/issue-39-fnd-01-gpt5.6-terra-codex` | `b67ba14` | Codex | GPT-5.6 Terra | Xhigh | 13 |
| 10 | #39 FND-01 | `agent/issue-39-fnd-01-gpt5.6-sol-codex` | `6dfd241` | Codex | GPT-5.6 Sol | Xhigh | 17 |
| 11 | #39 FND-01 | `agent/issue-39-fnd-01-grok-4.5` | `65f4b24` | Cursor | Grok 4.5 | high | 8 |
| 12 | #39 FND-01 | `agent/issue-39-fnd-01-composer-2.5` | `69ce416` | Cursor | Composer 2.5 | 未指定 | 5 |
| 13 | #39 FND-01 | `agent/issue-39-fnd-01-claude-sonnet-5` | `8e50cb5` | Claude Code | Sonnet 5 | Xhigh | 16 |
| 14 | #39 FND-01 | `agent/issue-39-fnd-01-claude-opus-5` | `fe26d58` | Claude Code | Opus 5 | Xhigh | 19 |

処理時間の単位が明示されていない場合、値そのものを相対比較に使用し、勝手に単位を補完しないでください。

---

# 3. 最重要ルール

## 実コードを最優先する

評価優先順位は以下とします。

1. Issue #39 の要求
2. 承認済み仕様・Accepted ADR・AGENTS.md
3. 各Headの実際のコード差分
4. テスト・CI・build結果
5. 実装の設計品質
6. モデル／エージェントの公開情報
7. 公開ベンチマーク

**一般的に高性能とされるモデルだから高得点にしてはいけません。**

今回の実装が悪ければ、有名モデルでも低得点にしてください。

---

# 4. 比較方法

全候補について、可能なら共通baseから各Headまでのdiffを取得してください。

以下を確認します。

- 変更ファイル数
- 追加行 / 削除行
- 作成されたsolution / project構成
- project reference
- package version
- compiler / analyzer設定
- CI
- test構成
- 不要な変更
- Scope外実装
- build / test結果
- secret混入
- placeholder実装
- 過剰設計
- 将来Issueの先取り

単に「動くか」だけでなく、**Issueに対して必要十分な差分か**を重視してください。

---

# 5. コーディング能力スコア

各実装を **100点満点** で評価してください。

## A. 要件・Issue達成度 — 25点

Issue #39 のAcceptance Criteriaをどこまで正確に満たしているか。

評価例:

- solution / project構成
- `net10.0`
- nullable
- analyzer / warning policy
- restore / build / test
- CI
- exact package version
- secret非保存
- placeholder business codeを追加していない

重大なAC未達がある場合、大きく減点してください。

---

## B. 正しさ・実行可能性 — 15点

実際に、

- restoreできる
- buildできる
- testできる
- CIで再現できる
- project referenceが成立する

かを評価してください。

見た目だけ整っていて実行不能な実装は低評価にします。

---

## C. Scope遵守・指示追従 — 15点

Issue #39 の範囲だけを実装しているか。

特に以下の先取りを減点してください。

- HTTP error contract
- correlation ID
- TimeProvider
- logging
- PostgreSQL
- Testcontainers
- EF Core
- DbContext
- migration
- Docker Compose
- health endpoint
- Identity
- business code
- business schema

「余計に実装した方が高性能」という評価は禁止します。

**必要最小限の正しい変更を高く評価してください。**

---

## D. 設計・Repository適合性 — 10点

- ADRに沿ったproject境界
- 依存方向
- repository conventions
- 将来Issueとの責任境界
- modular monolithとしての自然さ

を評価します。

独自アーキテクチャを勝手に導入した場合は減点してください。

---

## E. テスト・検証品質 — 10点

- 適切なtest project
- build/test確認
- CI検証
- project graph確認
- package pinning確認
- secret scan

など、IssueをCloseできるだけの検証能力を評価してください。

---

## F. コード品質・保守性 — 10点

- 可読性
- 単純性
- 命名
- 設定の一貫性
- 保守性
- 不要な抽象化の少なさ

を評価してください。

コード量が多いこと自体を高評価にしないでください。

---

## G. 変更の精度・最小性 — 10点

同じIssueを満たすなら、

- 不要ファイルが少ない
- 不要な設定が少ない
- boilerplateが少ない
- unrelated changeがない
- 将来変更を邪魔しない

実装を高く評価してください。

AIコーディングでは特に **「正しいものを必要な分だけ変更する能力」** を重視します。

---

## H. エラー・リスク管理 — 5点

- warningを安易に無効化していない
- secretを保存していない
- CIを形骸化していない
- flakyな構成になっていない
- 将来のIssueを阻害する設定を入れていない

ことを確認してください。

---

# 6. Coding Score

以下で合計します。

```text
Coding Score =
A Issue達成度              25
B 正しさ・実行可能性       15
C Scope遵守                15
D 設計・Repository適合性   10
E テスト・検証              10
F コード品質                10
G 変更精度・最小性          10
H リスク管理                 5
--------------------------------
合計                       100
```

これは **純粋な実装品質・コーディング能力のスコア** とします。

処理時間はCoding Scoreへ直接加算しません。

---

# 7. 処理効率の別評価

実装品質と速度を混同しないため、処理時間は別軸で評価してください。

各候補について、

```text
処理時間
Coding Score
1分あたりCoding Score
```

または単位不明の場合、

```text
Quality / Time Index = Coding Score ÷ 処理時間
```

を計算してください。

ただし、この値だけで最終順位を決めないでください。

例えば、

- 5で70点
- 19で95点

の場合、「前者が全面的に優秀」とは判定しないでください。

---

# 8. 実務総合スコア

Coding Scoreとは別に、実務上の速度を少し加味した指標も計算してください。

```text
Practical Score =
Coding Score × 0.90
+ Speed Score × 0.10
```

Speed Scoreは最速候補を100として、処理時間に反比例する形で0〜100へ正規化してください。

ただし外れ値の影響が大きすぎる場合は、その問題を説明してください。

**主ランキングはCoding Scoreとし、Practical Scoreは補助ランキングとします。**

---

# 9. AgentとModelを分離して分析

今回の結果には以下のコーディング環境が混在しています。

- Open Code
- Codex
- Cursor
- Claude Code

したがって、

```text
Model能力
+
Agent / Harness能力
+
Effort設定
```

の影響を完全には分離できません。

これを無視して「Model AはModel Bより強い」と断定しないでください。

特に GPT-5.6 Luna は、

- Open Code / GPT-5.6 Luna
- Codex / GPT-5.6 Luna

の両方があります。

これは重要な比較対象です。

### 必須比較

**GPT-5.6 Luna**

```text
Open Code + GPT-5.6 Luna
vs
Codex + GPT-5.6 Luna
```

について、

- Coding Score
- 差分品質
- Scope遵守
- テスト
- コード量
- 処理時間

を比較し、

**同じモデルでもAgent環境によってどの程度結果が変わるか**

を分析してください。

---

# 10. Effort設定の扱い

Effortには、

- Max
- Xhigh
- high
- Thinking
- 未指定

が混在しています。

これは公平な完全統制実験ではありません。

したがって、

**Effort差がモデル能力差として誤認される可能性**

を明記してください。

Effort未指定の候補について、勝手に値を推定しないでください。

---

# 11. 外部ベンチマーク

実装比較を完了した後に限り、必要であれば公開情報も参照してください。

例:

- SWE-bench Verified
- SWE-bench Pro
- SWE-rebench
- Terminal-Bench
- LiveCodeBench
- RepoBench
- Agentic Coding評価
- 公開されたIDE/CLI coding agent評価

ただし、今回の実コード評価とベンチマーク結果が食い違った場合、

**今回のIssue #39の実装結果を優先してください。**

外部ベンチマークは「なぜこの結果になった可能性があるか」を補足する用途に限定します。

---

# 12. 出力1 — 総合ランキング

| 順位 | Model | Agent | Head | Coding Score /100 | 処理時間 | Practical Score | 評価 |
|---:|---|---|---|---:|---:|---:|---|

---

# 13. 出力2 — 詳細採点

| Model | Issue 25 | Correct 15 | Scope 15 | Design 10 | Test 10 | Quality 10 | Precision 10 | Risk 5 | Total |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|

各点数には必ず根拠を付けてください。

---

# 14. 出力3 — 実装差分比較

各候補について簡潔に、

- 良かった点
- 悪かった点
- 不要だった変更
- 不足している変更
- 特徴的な設計判断
- テスト品質
- Scope違反
- 致命的問題の有無

を記載してください。

---

# 15. 出力4 — Finding

問題を以下で分類してください。

```text
Critical
Major
Minor
Nit
```

ただし、重箱の隅をつつくレビューは禁止します。

以下の場合は原則としてFindingにしないでください。

- 発生可能性が極めて低い
- 発生しても影響が小さい
- 単なる好み
- stylistic preference
- 将来への改善提案
- Issue #39のCloseを妨げないもの

**正しさ、安全性、Issue達成、保守性に実質的な影響がある問題を優先してください。**

---

# 16. 出力5 — ModelとAgentの分析

以下を分けて考察してください。

### Model側の可能性が高い差

- コード理解
- 推論
- Scope理解
- 設計
- バグ回避
- テスト設計

### Agent / Harness側の可能性が高い差

- repository探索
- tool利用
- build/test実行
- diff確認
- command retry
- git操作
- context管理

断定できない場合は「判別不能」としてください。

---

# 17. 出力6 — 用途別ランキング

今回の結果を主な根拠として、次を順位付けしてください。

- 最も正確な実装
- 最もScope遵守が良い
- 最もコード品質が高い
- 最もテストが良い
- 最も変更が簡潔
- 最も速い
- 最もQuality / Timeが高い
- 最も安心してIssue実装を任せられる

---

# 18. 最終結論

最後に以下を明示してください。

```text
Best Coding Quality:
1.
2.
3.

Best Practical Performance:
1.
2.
3.

Best Quality / Time:
1.
2.
3.

Best Agent Environment:
1.
2.
3.
```

さらに、

**「同じIssueを今後10件実装させるなら、どの Model + Agent を主力にするか」**

を1つ選んでください。

その際、

- Coding Score
- 安定性
- Scope遵守
- 処理速度
- Agent環境
- Effort
- 実装品質のばらつき

を考慮してください。

---

# 19. 評価上の注意

これは14候補について各1回の実装結果を比較する実験です。

そのため、

- 1回の結果だけでモデル本来の能力を断定しない
- Agent環境の差をモデル差と混同しない
- Effort差を無視しない
- 処理時間だけで優劣を判断しない
- コード量が多いほど高性能とは判断しない
- ベンチマーク順位を実装評価へ強制的に合わせない

こと。

最終的には、

**「このIssueに対して、誰が最も正確・簡潔・安全な変更を作ったか」**

を中心に評価してください。

---

# 20. FND-01 benchmark archive manifest

この節は、Issue #39 の14候補を比較実験結果として保存するためのアーカイブ台帳です。各候補のbranch HeadはGitHub上で再確認し、提示された短縮SHAと一致したfull SHAをsnapshot対象にしました。

候補別のCoding Scoreは、既存のreportおよび各PRの一次証拠に記録された値がないため、**本アーカイブ作業では再採点していません**。`未記録`は0点や失敗を意味しません。処理時間は実験時の原記録値をそのまま保持し、単位を補完していません。

| # | Model | Agent | Original branch | Full Head | Benchmark tag | PR | CI | Coding Score | 処理時間 | Final disposition | Selected |
|---:|---|---|---|---|---|---:|---|---:|---:|---|---|
| 1 | DeepSeek V4 Pro | Open Code | `agent/issue-39-fnd-01-dsv4pro` | `14ae1344c68f2e62faa3f4d81dc7c6af2ea4db3e` | `benchmark/fnd01/deepseek-v4-pro-opencode` | [#48](https://github.com/kooiei-in4a/minimal-bank-system/pull/48) | [31182285063](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31182285063) SUCCESS | 未記録 | 6 | Closed benchmark candidate | No |
| 2 | Qwen3.7 Plus | Open Code | `agent/issue-39-fnd-01-qwen3.7-plus` | `c7980b99e5ea54408ee539940f841270c1adbc74` | `benchmark/fnd01/qwen3.7-plus-opencode` | [#49](https://github.com/kooiei-in4a/minimal-bank-system/pull/49) | [31184197843](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31184197843) SUCCESS | 未記録 | 12 | Closed benchmark candidate | No |
| 3 | GPT-5.6 Luna | Open Code | `agent/issue-39-fnd-01-gpt5.6-luna` | `131f9a72942372a8bd1a8b7b0d369d9c919f31a4` | `benchmark/fnd01/gpt5.6-luna-opencode` | [#50](https://github.com/kooiei-in4a/minimal-bank-system/pull/50) | [31185622226](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31185622226) SUCCESS | 未記録 | 15 | Closed benchmark candidate | No |
| 4 | DeepSeek V4 Flash | Open Code | `agent/issue-39-fnd-01-dsv4flash` | `c1f7c37e8ed9059e8d46bd5655e656486d40d778` | `benchmark/fnd01/deepseek-v4-flash-opencode` | [#51](https://github.com/kooiei-in4a/minimal-bank-system/pull/51) | [31186977878](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31186977878) SUCCESS | 未記録 | 10 | Closed benchmark candidate | No |
| 5 | MiMo-V2.5 | Open Code | `agent/issue-39-fnd-01-mimo-v2.5` | `ba2c72dd70c7d6008438b5c482e724ef532f13c9` | `benchmark/fnd01/mimo-v2.5-opencode` | [#52](https://github.com/kooiei-in4a/minimal-bank-system/pull/52) | [31188524656](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31188524656) SUCCESS | 未記録 | 12 | Closed benchmark candidate | No |
| 6 | MiMo-V2.5-Pro | Open Code | `agent/issue-39-fnd-01-mimo-v2.5-pro` | `01a6d00c8c46223749cd1c4a0ffc5d3f1a02beca` | `benchmark/fnd01/mimo-v2.5-pro-opencode` | [#53](https://github.com/kooiei-in4a/minimal-bank-system/pull/53) | [31189586132](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31189586132) SUCCESS | 未記録 | 9 | Closed benchmark candidate | No |
| 7 | MiniMax M3 | Open Code | `agent/issue-39-fnd-01-minimax-m3` | `28494508f792cf07386ccbf148e0e7bcb4260640` | `benchmark/fnd01/minimax-m3-opencode` | [#54](https://github.com/kooiei-in4a/minimal-bank-system/pull/54) | [31190900666](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31190900666) SUCCESS | 未記録 | 17 | Closed benchmark candidate | No |
| 8 | GPT-5.6 Luna | Codex | `agent/issue-39-fnd-01-gpt5.6-luna-codex` | `eafa6312a83b9f8b04e6984c7dd5d36eb29ecfa8` | `benchmark/fnd01/gpt5.6-luna-codex` | [#55](https://github.com/kooiei-in4a/minimal-bank-system/pull/55) | [31192680940](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31192680940) SUCCESS | 未記録 | 14 | Closed benchmark candidate | No |
| 9 | GPT-5.6 Terra | Codex | `agent/issue-39-fnd-01-gpt5.6-terra-codex` | `b67ba14235abbf6473713802005e56adff2f71c7` | `benchmark/fnd01/gpt5.6-terra-codex` | [#56](https://github.com/kooiei-in4a/minimal-bank-system/pull/56) | [31194126317](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31194126317) SUCCESS | 未記録 | 13 | Closed benchmark candidate | No |
| 10 | GPT-5.6 Sol | Codex | `agent/issue-39-fnd-01-gpt5.6-sol-codex` | `6dfd241b9bd3b877de5b04c60d6c594b8edad5ec` | `benchmark/fnd01/gpt5.6-sol-codex` | [#57](https://github.com/kooiei-in4a/minimal-bank-system/pull/57) | [31195782531](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31195782531) SUCCESS | 未記録 | 17 | Closed benchmark candidate | No |
| 11 | Grok 4.5 | Cursor | `agent/issue-39-fnd-01-grok-4.5` | `65f4b24ec8c9292f6be69ecb00f2bb41affcf31a` | `benchmark/fnd01/grok-4.5-cursor` | [#58](https://github.com/kooiei-in4a/minimal-bank-system/pull/58) | [31195828710](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31195828710) SUCCESS | 未記録 | 8 | Closed benchmark candidate | No |
| 12 | Composer 2.5 | Cursor | `agent/issue-39-fnd-01-composer-2.5` | `69ce4160f2ec9ced4d75e3f282f17058447dd891` | `benchmark/fnd01/composer-2.5-cursor` | [#59](https://github.com/kooiei-in4a/minimal-bank-system/pull/59) | [31196525305](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31196525305) SUCCESS | 未記録 | 5 | Closed benchmark candidate | No |
| 13 | Sonnet 5 | Claude Code | `agent/issue-39-fnd-01-claude-sonnet-5` | `8e50cb5f78614fd069de3ed0a7443e2da2586a6c` | `benchmark/fnd01/claude-sonnet-5-claude-code` | [#60](https://github.com/kooiei-in4a/minimal-bank-system/pull/60) | [31198108091](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31198108091) SUCCESS | 未記録 | 16 | Closed benchmark candidate | No |
| 14 | Opus 5 | Claude Code | `agent/issue-39-fnd-01-claude-opus-5` | `fe26d58395d802c4b488e16b14334bf394bc0fab` | `benchmark/fnd01/claude-opus-5-claude-code` | [#61](https://github.com/kooiei-in4a/minimal-bank-system/pull/61) | [31199794399](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31199794399) SUCCESS | 未記録 | 19 | Closed benchmark candidate | No |

全14候補について、branch Headと期待値は一致し、14個のannotated tagがremoteで期待commitへ解決することを確認しました。PR #48〜#61はmergeせず、比較候補としてcloseしています。PRの差分・CI・会話履歴は各PRから引き続き参照できます。

# 21. Final integrated implementation

14候補の比較後に、良い設計・検証方法を比較して作成したcurated / synthesized implementationです。単独モデルの15番目の結果として扱いません。

- Branch: `agent/issue-39-fnd-01-final-code`
- Head: `d8e75bc6eab7fd14b7a58042b24deabe2227e189`
- Coding Score: **99/100**
- Disposition: curated / synthesized implementation
- Selected candidate: **No**（14候補とは別の統合成果物）

# 22. Benchmark interpretation and archive scope

このベンチマークは、各Model + Agent + Effortによる1回の実装結果を比較したものです。モデル本来の一般性能を断定するものではなく、Agent / Harness、Effort、repository探索、tool利用、検証実行、context管理の影響を完全には分離できません。処理時間とCoding Scoreを混同せず、Effort未指定を推定しません。

今回のアーカイブでは、既存の比較結果を恣意的に変更せず、候補のsource snapshot、PR履歴、CI結果、比較条件を後から追跡できる状態を優先しています。
