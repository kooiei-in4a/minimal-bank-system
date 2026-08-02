# spec-fix-001

LLM仕様レビューの最終審理済みFindingに対し、複数LLMを同一条件で修正させるポータブル修正ベンチマークである。

## Dataset

- Benchmark ID: `spec-fix-001`
- Input dataset ID: `spec-fix-001-portable-v1`
- Prompt revision: `file-first-v3`
- Fixed Base SHA: `dedbcaf31fd4c40b966facd1829c7535b8d0e4ba`
- Fixed Head SHA: `4944fb22806526f9e92dc47b516b57431c6c7f0a`
- Target: `docs/specs/bank-system-specification.md`
- Language: Japanese Markdown
- Formal benchmark execution status: **COMPLETED**
- Formal run: [`runs/2026-08-02/`](runs/2026-08-02/)
- Formal models: **14**
- Formal submissions: **28 Markdown files**
- Valid / Invalid: **2 / 12**
- Winner: **GPT-5.6 Luna XHigh — 98.0 / Excellent**
- Runner-up: **ChatGPT-5.6 Sol High — 97.0 / Excellent**

実行時間と実行方法は正式runに参考メタデータとして保存するが、採点には使用しない。

## 実行モデルへ渡すもの

次の2件だけを渡す。

```text
fix-prompt.md の本文
fix-evidence-bundle.md
```

Evidence bundleには、対象仕様全文、固定差分、正本、承認済み決定、最終Finding、承認事項、対象外、Document index、SHA-256を含む。

Round 1・Round 2の個別モデル結果、モデル評価、点数、順位、Gold Fix、採点基準は渡さない。

## 推奨実行方法

### ファイル生成を利用できるサービス

1. `fix-prompt.md`の本文を入力画面へ貼り付ける。
2. `fix-evidence-bundle.md`を添付する。
3. モデルに次の2ファイルを新規生成・添付させる。

```text
spec-fix-001-{reviewer-slug}-{yyyymmdd}.md
fixed-bank-system-specification.md
```

既存ファイルの編集、検索置換、patch適用、Git操作は行わせない。

### ファイル生成を利用できないサービス

同じプロンプトを使用する。モデルは次の2段階で画面出力する。

1. 第1応答: 修正報告書だけ
2. オペレーターが固定メッセージ`CONTINUE_SPEC`を送信
3. 第2応答: 修正後仕様書全文だけ

モデルごとに「結果は？」「続けて」等の異なる追加指示を使用しない。

## 期待成果物

```text
spec-fix-001-{reviewer-slug}-{yyyymmdd}.md
fixed-bank-system-specification.md
```

評価時には出力方式も記録する。

```text
Actual output mode: file / screen-two-step
File attachment available: yes / no
Continuation turns: 0 / 1
Output truncated: yes / no
```

ファイルを作れること自体には加点しない。成果物の内容を評価する。

## 再実行規則

- ファイルが添付されていないのに生成成功と主張した場合は、提出不備とする。
- 画面出力が途中で切れた場合は`INCOMPLETE_SUBMISSION`とし、内容採点を開始しない。
- `screen-two-step`では`CONTINUE_SPEC`を1回だけ使用する。
- 2成果物が揃わない場合は、同じ条件で再実行する。
- 欠落を補うための自由文追加指示は使用しない。

## 承認待ちFinding

次は、修正モデルが完成判断を代行してはならない。

- F-003: 冪等性外部契約
- F-004: 利用者管理・役割権限管理のv0.1.0契約
- F-008: Transaction 0件時の履歴レスポンス

処理を停止せず、決定軸と影響を整理し、修正報告書で`BLOCKED_BY_APPROVAL`として報告する。

## v3で追加した統制

- ファイル出力優先、画面出力は2段階
- 入力プロンプト・Evidence bundleの再掲禁止
- 評価用語とFinding IDの仕様書混入禁止
- AC定義は§18だけ、§19は参照だけ
- §19.1はREQ、§19.2はB、§19.3はD
- F-007で新規ADR候補を追加しない
- N-002は既存4候補へ011〜014だけを付与
- 意味の異なる拒否原因と複数候補codeを一つのACへまとめない

## 評価者専用ファイル

実行モデルへ渡さない。

```text
fix-evaluation-rubric.md
gold-fix-acceptance-criteria.md
```

## Files

| File | Audience | Purpose |
|---|---|---|
| `fix-prompt.md` | Operator / execution model | File-first portable repair instructions |
| `fix-evidence-bundle.md` | Execution model | Offline evidence and final findings |
| `fix-evaluation-rubric.md` | Evaluator only | Weighted scoring, completeness gate and hard-fail rules |
| `gold-fix-acceptance-criteria.md` | Evaluator only | Meaning-level Gold Fix conditions |
| `manifest.yaml` | Operator / evaluator | Input package hashes, prompt revision, document IDs, fixed SHAs, formal run pointer |
| `runs/2026-08-02/` | Evaluator / reviewer | Formal model outputs, timing metadata, scoring and integrity evidence |
