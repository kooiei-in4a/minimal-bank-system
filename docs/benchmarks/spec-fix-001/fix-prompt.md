# spec-fix-001 ポータブル修正ベンチマーク指示

```yaml
BENCHMARK_ID: "spec-fix-001"
INPUT_DATASET_ID: "spec-fix-001-portable-v1"
PROMPT_REVISION: "file-first-v3"
OUTPUT_MODE: "file-preferred"
REVIEWER_MODEL: "<実行サービス上のモデル・推論モード名>"
REVIEWER_SLUG: "<英小文字・数字・ハイフンまたはアンダースコア>"
EXECUTION_DATE: "<YYYY-MM-DD>"
EXECUTION_DATE_COMPACT: "<YYYYMMDD>"
```

## 1. 役割・入力・固定対象

あなたは仕様修正担当者である。`fix-evidence-bundle.md`だけを事実認定の根拠として、固定対象仕様を最終確定Findingへ適合させる。

使用できる入力は、このプロンプトと`fix-evidence-bundle.md`だけである。Git、GitHub、別ファイル、Web、過去会話、memory、他モデル結果、Round評価、Gold Fix、採点基準は参照しない。入力プロンプトやEvidence bundleを回答へ再掲・要約しない。

- Repository label: `kooiei-in4a/minimal-bank-system`
- Review target PR: `#9`
- Base SHA: `dedbcaf31fd4c40b966facd1829c7535b8d0e4ba`
- Head SHA: `4944fb22806526f9e92dc47b516b57431c6c7f0a`
- Target file: `docs/specs/bank-system-specification.md`

固定対象、正本、SHA、Finding IDを変更しない。

`REVIEWER_SLUG`は英小文字・数字・ハイフン・アンダースコアだけに正規化する。

## 2. 成果物と出力方式

次の2件を作成する。

```text
spec-fix-001-{REVIEWER_SLUG}-{EXECUTION_DATE_COMPACT}.md
fixed-bank-system-specification.md
```

### ファイルを実際に添付できる場合

2件を新しいUTF-8 Markdownファイルとして生成し、ダウンロード可能な状態で添付する。

- Evidence bundle、入力ファイル、既存仕様書を直接編集・上書きしない。
- 検索置換、patch、diff、Git、GitHub操作を行わない。
- 回答本文には生成した2ファイル名だけを簡潔に記載する。
- 添付していないファイルを「生成した」と報告しない。

### ファイルを添付できない場合

画面への2段階出力を使用する。

第1応答では修正報告書だけを出力して終了する。

```text
===== BEGIN OUTPUT: spec-fix-001-{REVIEWER_SLUG}-{EXECUTION_DATE_COMPACT}.md =====
<修正報告書の完全なMarkdown>
===== END OUTPUT: spec-fix-001-{REVIEWER_SLUG}-{EXECUTION_DATE_COMPACT}.md =====
```

オペレーターから固定メッセージ`CONTINUE_SPEC`を受けた後、第2応答で修正後仕様書全文だけを出力する。

```text
===== BEGIN OUTPUT: fixed-bank-system-specification.md =====
<修正後仕様書の完全なMarkdown>
===== END OUTPUT: fixed-bank-system-specification.md =====
```

区切り外の説明、`省略`、`変更なし`、`以下同文`、差分だけの出力は禁止する。出力方式や継続許可を質問せず、利用可能な方式を選んで実行する。

## 3. 修正範囲

Evidence bundleの`FINAL-FINDINGS-001`にある次の11件だけを扱う。

- `F-001`〜`F-009`
- `N-001`
- `N-002`

Findingを追加、削除、改番、分割しない。

維持するもの:

- 原始要件、B-01〜B-06、D-01〜D-17
- 正常系、主要異常系、境界、権限、状態遷移
- 解約時残高0円、残高非負
- 登録、解約、入金、出金、振込の不可分性
- 並行入金時の正確な取引後残高
- 冪等性、Transaction不変性、4取引種別
- 全額出金を`出金`として記録する契約
- 履歴全件返却、API自動リトライ非保証
- 既存のREQ、B、D、AC ID

禁止するもの:

- 製品機能の追加、原始要件・既存決定の弱体化
- Finding対応を超える全面改稿、章再設計、無関係な文体変更
- 不要なID改番
- DBスキーマ、認証方式、lock順序、timeout等の実装方式固定
- 未承認の具体値、固定code、HTTP状態、操作集合の創作

出金・振込のDB行ロック要求は維持する。入金は正確性を要求するが、排他方式を固定しない。

## 4. 仕様書と評価用語の分離

次は修正報告書だけで使用できる。修正後仕様書へ記載しない。

- Finding IDまたは`Finding`
- `FIXED`、`BLOCKED_BY_APPROVAL`、`NOT_FIXED`、`NOT_APPLICABLE`
- `PENDING_KOO_APPROVAL`
- `execution model`
- benchmark、Gold Fix、rubric、reviewer、modelに関する記述

仕様書の未決事項は、「Koo承認待ち」「未決」「承認後に確定する」と製品文書として記載する。新しい状態トークンを作らない。

§16.3のcode・HTTP表はDraft提案のまま維持する。ACでは未承認codeを確定結果として断定せず、該当結果が§16.3のKoo承認後に確定することを通常文で示す。

## 5. Finding別統制

### 直接修正

`F-001`、`F-002`、`F-006`、`F-007`、`F-009`、`N-001`、`N-002`

既存決定の明確化、AC補完、責務分離、表現修正、追跡更新として対応する。

### 既存承認事項と連動

`F-005`

一つのACには、原則として一つの拒否原因と一つの期待結果を書く。意味の異なる原因を`または`でまとめず、期待codeを`AまたはB`のように複数候補で記載しない。

特に次を分離する。

- 自己振込 / 解約済み
- 振込元不存在 / 振込先不存在
- 不存在 / 解約済み
- 負残高 / 状態・解約日時不整合

同じ原因分類の境界値は一つのACで検証してよい。

### 承認待ち

`F-003`、`F-004`、`F-008`

完成契約を選択せず、修正報告書では原則`BLOCKED_BY_APPROVAL`とする。問い、決定軸、各選択の影響、承認後に必要なACを整理する。

仕様書ではFinding IDや評価Statusを使わず、製品上の未決事項として記載する。承認待ちが残っても、直接修正可能なFindingを完了した仕様書全文を作成する。

## 6. ACとトレーサビリティ

- ACの定義本文はすべて§18へ置く。
- §19にはAC IDの参照だけを置き、Given / When / Then本文を置かない。
- §19.1は`REQ-*`だけ、§19.2は`B-01`〜`B-06`だけ、§19.3は`D-01`〜`D-17`だけを扱う。
- 異なるID種別を同じ表へ混在させない。
- ACは再現可能なGiven / When / Thenまたは同等形式とする。
- F-003、F-004、F-008の未承認結果を確定ACにしない。
- 既存AC IDを不必要に改番しない。
- ACを追加・分割した場合は§19も更新する。

## 7. ADR候補

F-007では既存`ADR-CANDIDATE-003`の記述を修正し、出金・振込の行ロック要求と入金の方式非固定を区別する。**新しい入金用ADR候補を追加しない。**

N-002でIDを付けるのは、Evidence bundleにある既存の未採番4候補だけである。

1. 認証・認可方式
2. Audit Logの保存・保護・閲覧・保持方式
3. 有限回の内部リトライ採否
4. バックアップ・復旧の具体方式

これらへ`ADR-CANDIDATE-011`〜`ADR-CANDIDATE-014`を一つずつ付与する。

- 既存001〜010を変更しない。
- `ADR-CANDIDATE-015`以降を追加しない。
- 候補の意味を変更せず、採用・承認しない。

## 8. 修正報告書

仕様書本文を重複転載せず、次の構成で簡潔に記載する。

```markdown
# spec-fix-001 修正報告書

## Execution metadata
- Benchmark ID:
- Input dataset ID:
- Prompt revision:
- Reviewer model:
- Reviewer slug:
- Execution date:
- Actual output mode: file / screen-two-step
- Base SHA:
- Head SHA:
- External information used: no
- Other model outputs used: no

## Verdict
- COMPLETE / COMPLETE_WITH_APPROVAL_BLOCKERS / FAILED

## Finding status
| Finding ID | Status | Changed sections | Rationale |
|---|---|---|---|

## Changes
## Open approval items
## Traceability changes
## Regression self-review
## Unresolved findings
## Changed files
## Independence declaration
```

Statusは`FIXED`、`BLOCKED_BY_APPROVAL`、`NOT_FIXED`、`NOT_APPLICABLE`だけを使用する。

Evidence bundleに不足・矛盾がある場合は、影響するFindingを`NOT_FIXED`として理由を記録し、他の修正を継続する。

## 9. 提出前確認

1. 11件すべてにStatusがある。
2. `FIXED`は仕様本文、§18のAC、§19の追跡まで整合している。
3. 承認待ちの判断を代行していない。
4. 仕様書へ評価用語やFinding IDを混入させていない。
5. AC定義は§18だけ、§19は参照だけである。
6. §19.1 / §19.2 / §19.3のID種別を混在させていない。
7. F-007で新ADR候補を追加していない。
8. N-002の新規IDは011〜014の4件だけである。
9. 原始要件、既存決定、既存IDを壊していない。
10. 未承認契約や実装方式を確定していない。
11. 修正後仕様書が全文揃っている。
12. 実際の出力方式を`Actual output mode`へ記録している。

承認待ちFindingが残っても、直接修正可能なFindingを完了し、安全に整理できた場合は`COMPLETE_WITH_APPROVAL_BLOCKERS`とする。
