# spec-fix-001 ポータブル修正ベンチマーク指示

```yaml
BENCHMARK_ID: "spec-fix-001"
INPUT_DATASET_ID: "spec-fix-001-portable-v1"
REVIEWER_MODEL: "<実行サービス上のモデル・推論モード名>"
REVIEWER_SLUG: "<英小文字・数字・ハイフンまたはアンダースコア>"
EXECUTION_DATE: "<YYYY-MM-DD>"
EXECUTION_DATE_COMPACT: "<YYYYMMDD>"
```

## 1. あなたの役割

あなたは仕様修正担当者である。`fix-evidence-bundle.md`だけを事実認定の根拠として、固定対象仕様を最終確定Findingへ適合させる。

このベンチマークは、完成文章の一致ではなく、正本との整合、Finding対応、回帰防止、範囲統制、承認規律、検証可能性を評価する。

## 2. 使用できる入力

実行時に使用できる入力は次の2ファイルだけである。

```text
fix-prompt.md
fix-evidence-bundle.md
```

次を利用してはならない。

- Git、GitHub、リポジトリの別ファイル
- Web検索、外部URL、外部文書
- 過去会話、memory、個人プロファイル
- 他モデルのレビュー結果または修正結果
- Round 1・Round 2のモデル評価、順位、点数
- Gold Fix、採点基準、評価用ルーブリック
- 推測による不足資料の補完

Evidence bundleに不足または矛盾がある場合は、推測して完成させず、修正報告書へ記録する。

## 3. 固定対象

- Repository label: `kooiei-in4a/minimal-bank-system`
- Review target PR: `#9`
- Base SHA: `dedbcaf31fd4c40b966facd1829c7535b8d0e4ba`
- Head SHA: `4944fb22806526f9e92dc47b516b57431c6c7f0a`
- Target file: `docs/specs/bank-system-specification.md`

固定対象、正本、SHA、Finding IDを変更してはならない。

## 4. 作成する出力

Markdownファイルを2件作成する。

```text
fixed-bank-system-specification.md
spec-fix-001-{REVIEWER_SLUG}-{EXECUTION_DATE_COMPACT}.md
```

チャットへ説明するだけでは不合格とする。両ファイルを完全なMarkdownとして出力する。

### 4.1 `fixed-bank-system-specification.md`

- Evidence bundleに収録された対象仕様全文を基礎とする。
- 最終確定Findingだけを修正する。
- 新規Koo承認が必要なFindingは、選択を代行せずOpen approval itemへ整理する。
- 承認待ちのため解決できないFindingがあっても、対象仕様を途中で省略しない。
- 変更しなかった章を`省略`、`変更なし`、diff形式だけで置き換えない。
- 完全な修正後仕様書を出力する。

### 4.2 修正報告書

最低限、次の構成を使用する。

```markdown
# spec-fix-001 修正報告書

## Execution metadata
- Benchmark ID:
- Input dataset ID:
- Reviewer model:
- Reviewer slug:
- Execution date:
- Base SHA:
- Head SHA:
- External information used: no
- Other model outputs used: no

## Verdict
- COMPLETE / COMPLETE_WITH_APPROVAL_BLOCKERS / FAILED

## Finding status
| Finding ID | Status | Changed sections | Rationale |
|---|---|---|---|

Status:
- FIXED
- BLOCKED_BY_APPROVAL
- NOT_FIXED
- NOT_APPLICABLE

## Changes
## Approval discipline
## Open approval items
## Traceability changes
## Regression self-review
## Unresolved findings
## Changed files
## Independence declaration
```

## 5. 修正対象

Evidence bundleの`FINAL-FINDINGS-001`に収録された次のFindingだけを対象とする。

- `F-001`
- `F-002`
- `F-003`
- `F-004`
- `F-005`
- `F-006`
- `F-007`
- `F-008`
- `F-009`
- `N-001`
- `N-002`

Findingを追加、削除、改番、分割してはならない。下位の修正作業を独立Findingとして水増ししない。

## 6. 必須統制

### 6.1 範囲

- 製品機能を拡張しない。
- 原始要件を削除、縮小、弱体化しない。
- 既存のKoo決定を変更しない。
- 対象仕様以外のファイルを修正対象にしない。
- Finding修正に必要な範囲を超えて全面改稿しない。
- 文体統一、表現改善、章再構成だけを目的とした無関係変更を行わない。
- 既存章番号、REQ ID、B ID、D ID、AC IDを不必要に変更しない。

### 6.2 承認

- 未承認の製品判断を代行しない。
- 新規Koo承認が必要なFindingは`BLOCKED_BY_APPROVAL`とする。
- 選択肢、決定軸、各選択の影響は記載してよい。
- 推奨案を示す場合も、確定仕様として本文へ混入させない。
- 既存§22.1承認事項を承認済みに昇格させない。
- 承認待ちの具体値、code、HTTP状態、操作集合を創作しない。

### 6.3 仕様と技術方式の境界

- 外部から観測可能な意味だけを仕様へ記載する。
- DBスキーマ、保存テーブル、認証ライブラリ、fingerprint、lock順序、timeout等を固定しない。
- ADRへ委譲すべき事項を仕様へ混入させない。
- 原始要件が明示した出金・振込のDB行ロック要求は削除しない。
- 入金の具体的排他方式は固定しない。

### 6.4 契約保全

次を維持する。

- 正常系、主要異常系、境界値
- 役割・権限
- Customer／Accountの単方向状態遷移
- 解約時残高0円
- 登録、解約、入金、出金、振込の不可分性
- 残高非負
- 並行入金時の正確な取引後残高
- 金銭操作の冪等性要求
- Transaction不変性
- 4種類のTransaction
- 全額出金を`出金`として記録
- D-06の履歴全件返却
- D-17のAPI自動リトライ非保証
- 既存トレーサビリティID

## 7. Finding対応規則

### 7.1 直接修正できるFinding

次は既存決定の明確化、AC補完、責務分離、表現修正として対応する。

- F-001
- F-002
- F-006
- F-007
- F-009
- N-001
- N-002

### 7.2 既存承認事項と連動するFinding

次は§22.1の承認前提を維持しつつ、曖昧なACや先取り表現を修正する。

- F-005
- F-009

F-005では原因別ACへ分割してよいが、未承認codeを確定してはならない。

### 7.3 新規承認が必要なFinding

次は完成契約を選択せず、Open approval item、選択肢、影響、承認後ACを整理する。

- F-003
- F-004
- F-008

これらを推測で`FIXED`にしてはならない。承認前の正しいStatusは原則`BLOCKED_BY_APPROVAL`である。

## 8. Acceptance Criteriaとトレーサビリティ

- 追加ACはGiven / When / Thenまたは同等に検証可能な形式にする。
- 異なる失敗原因を一つのACへまとめない。
- 正常結果、HTTP、固定codeまたは承認待ち状態、非更新結果を一意にする。
- REQ、B、D、仕様節、ACの意味的な対応を確認する。
- ACを追加しただけでなく、§19の追跡表を更新する。
- F-003、F-004、F-008の承認待ち部分について、未承認の期待結果をACへ書かない。

## 9. 自己レビュー

提出前に最低限、次を確認する。

1. 11件のFindingすべてにStatusがある。
2. `FIXED`としたFindingは、意味とACの両方が修正されている。
3. `BLOCKED_BY_APPROVAL`は、決定軸と影響が記録されている。
4. 既存Koo決定を変更していない。
5. 原始要件を削除・縮小していない。
6. 技術方式を先取りしていない。
7. 不要な全面改稿がない。
8. REQ、B、D、AC IDが壊れていない。
9. 修正後仕様が完全な文書として読める。
10. 外部情報、他モデル結果、Gold Fix、採点基準を参照していない。

## 10. 禁止

- Findingの追加
- 製品機能の追加
- 未承認判断の確定
- 既存決定の変更
- 対象仕様の全面再設計
- 具体的な実装方式の固定
- 他モデル結果の参照
- 採点者向け正解の推測
- Git、GitHub、Issue、PR操作
- 修正対象外ファイルの出力

## 11. 完了条件

次をすべて満たした場合だけ`COMPLETE`または`COMPLETE_WITH_APPROVAL_BLOCKERS`とする。

- 2ファイルが完全なMarkdownとして存在する。
- 全Findingが報告される。
- 直接修正可能なFindingが正しく修正される。
- 承認待ちFindingで判断を代行していない。
- 既存契約の回帰がない。
- 追跡表とACが整合する。
- 独立性宣言がある。

承認待ちFindingが残ること自体は失敗ではない。承認を代行せず、正しく`BLOCKED_BY_APPROVAL`としたかを評価する。
