# spec-fix-001 ポータブル修正ベンチマーク指示

```yaml
BENCHMARK_ID: "spec-fix-001"
INPUT_DATASET_ID: "spec-fix-001-portable-v1"
PROMPT_REVISION: "screen-output-v2"
REVIEWER_MODEL: "<実行サービス上のモデル・推論モード名>"
REVIEWER_SLUG: "<英小文字・数字・ハイフンまたはアンダースコア>"
EXECUTION_DATE: "<YYYY-MM-DD>"
EXECUTION_DATE_COMPACT: "<YYYYMMDD>"
```

## 1. 役割と根拠

あなたは仕様修正担当者である。`fix-evidence-bundle.md`だけを事実認定の根拠として、固定対象仕様を最終確定Findingへ適合させる。

評価対象は、完成文章の表面的な一致ではなく、正本との整合、Finding対応、回帰防止、範囲統制、承認規律、検証可能性である。

使用してよい入力は、このプロンプトと`fix-evidence-bundle.md`だけである。次を参照してはならない。

- Git、GitHub、リポジトリの別ファイル
- Web検索、外部URL、外部文書
- 過去会話、memory、個人プロファイル
- 他モデルのレビュー結果または修正結果
- Round 1・Round 2の評価、順位、点数
- Gold Fix、採点基準、評価ルーブリック

## 2. 固定対象

- Repository label: `kooiei-in4a/minimal-bank-system`
- Review target PR: `#9`
- Base SHA: `dedbcaf31fd4c40b966facd1829c7535b8d0e4ba`
- Head SHA: `4944fb22806526f9e92dc47b516b57431c6c7f0a`
- Target file: `docs/specs/bank-system-specification.md`

固定対象、正本、SHA、Finding IDを変更してはならない。

## 3. 出力方法

ファイルシステムを操作してはならない。ファイル作成・更新、既存文字列の検索置換、patchまたはdiffの適用、Git・GitHub操作を行わない。

回答画面へ、次の2つの完成済みMarkdownをこの順番で出力する。

1. 修正報告書
2. 修正後仕様書全文

次の区切りを厳守する。

```text
===== BEGIN OUTPUT: spec-fix-001-{REVIEWER_SLUG}-{EXECUTION_DATE_COMPACT}.md =====
<修正報告書の完全なMarkdown>
===== END OUTPUT: spec-fix-001-{REVIEWER_SLUG}-{EXECUTION_DATE_COMPACT}.md =====

===== BEGIN OUTPUT: fixed-bank-system-specification.md =====
<修正後仕様書の完全なMarkdown>
===== END OUTPUT: fixed-bank-system-specification.md =====
```

区切りの外へ前置き、説明、後書きを出力しない。仕様書は差分や抜粋ではなく全文を出力し、`省略`、`変更なし`、`以下同文`を使用しない。

途中で確認、継続許可、ファイル編集許可を求めない。承認待ちFindingや入力上の不足があっても中断せず、可能な修正を完了して報告する。

## 4. 修正範囲と禁止事項

Evidence bundleの`FINAL-FINDINGS-001`にある次の11件だけを扱う。

- `F-001`〜`F-009`
- `N-001`
- `N-002`

Findingを追加、削除、改番、分割しない。下位作業を新しいFindingとして水増ししない。

次を維持する。

- 原始要件、B-01〜B-06、D-01〜D-17
- 正常系、主要異常系、境界値、権限
- Customer／Accountの単方向状態遷移と解約時残高0円
- 登録、解約、入金、出金、振込の不可分性
- 残高非負、並行入金時の正確な取引後残高
- 金銭操作の冪等性要求、Transaction不変性
- 4種類のTransactionと、全額出金を`出金`として記録する契約
- 履歴全件返却、API自動リトライ非保証
- 既存のREQ、B、D、AC ID

次を行わない。

- 製品機能の拡張
- 原始要件や既存決定の削除、縮小、反転
- Finding対応を超える全面改稿や章再設計
- 無関係な文体統一、用語変更、ID改番
- DBスキーマ、保存テーブル、認証方式、fingerprint、lock順序、timeout等の固定
- 未承認の具体値、固定code、HTTP状態、操作集合の創作

原始要件が明示する出金・振込のDB行ロック要求は削除しない。入金は正確性を要求するが、具体的な排他方式を固定しない。

## 5. Findingの扱い

### 5.1 直接修正する

- `F-001`
- `F-002`
- `F-006`
- `F-007`
- `F-009`
- `N-001`
- `N-002`

既存決定の明確化、Acceptance Criteria補完、責務分離、表現修正、トレーサビリティ更新として対応する。

### 5.2 既存承認事項と連動して修正する

- `F-005`

異なる失敗原因を別のAcceptance Criteriaへ分割してよい。ただし、§16.3のcodeやHTTP対応を承認済みとして確定しない。

### 5.3 承認待ちとして整理する

- `F-003`
- `F-004`
- `F-008`

これらは完成契約を選択せず、原則`BLOCKED_BY_APPROVAL`とする。

- 決定が必要な問い
- 選択肢または決定軸
- 各選択の外部契約・工程上の影響
- 承認後に必要となるAcceptance Criteria

を未決事項節と修正報告書へ整理する。推奨を示す場合も、確定仕様へ混入させない。

承認待ちが残っていても、既存本文を必要以上に削除せず、直接修正可能なFindingを完了した仕様書全文を出力する。

## 6. Acceptance Criteriaとトレーサビリティ

- ACはGiven / When / Thenまたは同等に検証可能な形式とする。
- 異なる失敗原因を一つのACへまとめない。
- 正常結果、HTTP、固定codeまたは承認待ち状態、非更新結果を一意にする。
- ACを追加・分割した場合は§19の追跡表も更新する。
- REQ、B、D、仕様節、ACの意味的対応を確認する。
- F-003、F-004、F-008の未承認結果をACへ書かない。

## 7. 修正報告書

次の構成を使用する。

```markdown
# spec-fix-001 修正報告書

## Execution metadata
- Benchmark ID:
- Input dataset ID:
- Prompt revision:
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

## Changes
## Open approval items
## Traceability changes
## Regression self-review
## Unresolved findings
## Changed files
## Independence declaration
```

Statusは次の4つだけを使用する。

- `FIXED`
- `BLOCKED_BY_APPROVAL`
- `NOT_FIXED`
- `NOT_APPLICABLE`

Evidence bundleに不足または矛盾がある場合も質問や中断をせず、影響するFindingを`NOT_FIXED`として理由を記録し、他の修正を継続する。

## 8. 最終確認

出力前に次を確認する。

1. 11件すべてにStatusがある。
2. `FIXED`は仕様本文、AC、追跡の必要箇所まで整合している。
3. `BLOCKED_BY_APPROVAL`は決定軸と影響を記録し、判断を代行していない。
4. 原始要件と既存Koo決定を変更していない。
5. 技術方式、未承認code・HTTP・操作集合を確定していない。
6. 不要な機能追加、全面改稿、ID破壊がない。
7. 修正後仕様書を全文出力している。
8. 外部情報、他モデル結果、Gold Fix、採点基準を参照していない。
9. 指定した2つの出力区切りだけを使用している。

承認待ちFindingが残っていても、直接修正可能なFindingを完了し、安全に`BLOCKED_BY_APPROVAL`とした場合は`COMPLETE_WITH_APPROVAL_BLOCKERS`とする。
