# spec-review-002 — LLM独立レビュー 第2ラウンド

## 目的

第1ラウンドと同じ固定レビュー対象・正本資料を使用し、ブラッシュアップ済みのポータブルプロンプトで各LLMの独立レビューを再実行する。

このディレクトリは、第2ラウンドの実行条件と、各LLMが出力したMarkdownレビュー結果を記録するための専用領域である。

## 固定レビュー対象

- Round: `2`
- Review ID: `spec-review-002`
- Source review ID: `spec-review-001`
- Repository label: `kooiei-in4a/minimal-bank-system`
- PR label: `#9`
- Base SHA: `dedbcaf31fd4c40b966facd1829c7535b8d0e4ba`
- Head SHA: `4944fb22806526f9e92dc47b516b57431c6c7f0a`
- Target file: `docs/specs/bank-system-specification.md`
- Changed files in review target: 1

本ブランチでは、レビュー対象仕様、PR #9、Issue、ゲート、第1ラウンド成果物を変更しない。

## 各LLMへ渡す資料

次の2ファイルだけを同時に渡す。

```text
review-prompt.md
review-evidence-bundle.md
```

配布パッケージはリポジトリ外で固定し、すべてのLLMへ同一バイト列を渡す。第1ラウンドのレビュー結果、審理結果、モデル評価、Gold Findingは渡さない。

### 固定SHA-256

| Artifact | SHA-256 |
| --- | --- |
| `review-prompt.md` | `af70cf9f1112b7479abcd2814f08746103329fef04dad7d3fc3fb1258c30f966` |
| `review-evidence-bundle.md` | `408bfc861e494931224f9c0feeb146e11fb27f271f5caf94d1f74884ffd9321b` |
| `spec-review-002-portable.zip` | `db2aa36f2d2eab0b4a03337935fbbb72dc5d1520d5ebf3b8757a0f7c1529e56d` |

実行前に、可能な環境ではSHA-256を照合する。

## 実行時に変更する項目

`review-prompt.md`の先頭にある次の項目だけを、実行するLLMごとに設定する。

```yaml
REVIEWER_MODEL: "<実行サービス上のモデル・推論モード名>"
REVIEWER_SLUG: "<ファイル名用slug>"
REVIEW_DATE: "<YYYY-MM-DD>"
REVIEW_DATE_COMPACT: "<YYYYMMDD>"
```

その他のプロンプト本文とEvidence bundleは変更しない。

## レビュー結果の格納

各LLMの結果は、1モデルにつき1つのMarkdownとして次へ格納する。

```text
docs/reviews/spec-review-002/results/
```

ファイル名:

```text
spec-review-002-{REVIEWER_SLUG}-{REVIEW_DATE_COMPACT}.md
```

例:

```text
spec-review-002-sonnet-5-xhigh-20260802.md
```

## 結果ファイルの受入確認

格納前に次を確認する。

- Review IDが`spec-review-002`
- Reviewer modelが実行時設定と一致
- Base SHAとHead SHAが固定値と一致
- Evidence datasetが`spec-review-002-portable-v1`
- 結果がMarkdownとして完結している
- 他モデル結果を参照していない旨が記載されている
- 外部情報を参照していない旨が記載されている
- リポジトリ、Issue、PR、ゲートを変更していない
- ファイル名が命名規則に一致する

## 独立性

各モデルの実行が完了するまでは、次をモデルへ提示しない。

- `docs/reviews/spec-review-001/`配下のレビュー成果物
- 第2ラウンドの他モデル結果
- Finding審理マトリクス
- モデル評価結果
- 統合済み最終レビュー
- Gold Finding

## 第2ラウンド完了条件

- 対象モデルすべてのMarkdownが`results/`へ格納されている
- 各結果の実行モデル、日付、ファイル名が一意である
- 全モデルが同じPrompt SHAとEvidence SHAを使用している
- 欠落・破損した出力がない
- 比較・審理を開始する前に全モデルの提出を固定している

全モデルの提出完了後、Finding正規化、盲検審理、モデル評価、最終統合レビューを別成果物として実施する。
