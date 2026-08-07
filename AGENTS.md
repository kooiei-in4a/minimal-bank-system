# AGENTS.md

## 1. 目的

本リポジトリで作業する人間およびAIエージェントは、要件定義からリリースまでの追跡可能性を維持する。

## 2. 正本の優先順位

1. Kooが承認した製品方針および仕様
2. `Accepted`状態のADR
3. GitHub Issueで定義された作業範囲
4. コードおよび自動テスト
5. Pull Requestの説明とコメント

下位成果物が上位成果物と矛盾する場合、下位成果物を修正する。IssueまたはPRコメントだけで仕様やADRを暗黙変更してはならない。

## 2.1 プロジェクト統制Issue

プロジェクト全体の進行は、Parent Issue #3で管理する。

- Parent Issue: https://github.com/kooiei-in4a/minimal-bank-system/issues/3
- 計画、仕様化、ADR作成、Issue分割、実装、レビュー、merge、releaseに着手する前に、必ずParent Issue #3を確認する。
- 確認対象は、現在フェーズ、前提フェーズゲート、対象作業を管理するIssue、未決定のBlocker、現在の禁止事項、プロジェクト目的と対象外とする。

Parent Issue #3は、進行、フェーズ、ゲート、Blocker、子Issueおよび検証証拠を管理する統制Issueであり、仕様、ADR、受入条件または設計判断の正本ではない。

Parent Issue #3と、Kooが承認した仕様または`Accepted`状態のADRが矛盾する場合は、承認済み仕様またはADRを優先し、矛盾を報告して停止する。Parent Issue #3のコメントまたはチェックボックスだけで、仕様またはADRを暗黙変更してはならない。

## 2.2 子Issueの追跡ルール

各作業は、原則としてParent Issue #3から追跡可能な対象Issueを持つ。

対象Issueには、最低限、次を記録する。

- Parent / Control Issue: #3
- Project phase
- Required gate
- Required gate status

専用の対象Issueが必要な作業であるにもかかわらず対象Issueが存在しない場合は、独断でIssueを作成したり、実装へ進んだりせず、停止して報告する。

Parent Issue #3の詳細を子Issueへ複製しない。具体的な作業範囲、対象外、Acceptance Criteriaおよび操作権限は、対象となる子Issueを正本とする。

今回の統制ルール導入PRは、Parent Issue #3を対象Issueとしてよい。PR本文では`Refs #3`を使用し、Issue #3をcloseする表現を使用しない。

## 3. 成果物の責任範囲

- `docs/requirements/`: 受領した原始要件。内容を黙って書き換えない。
- `docs/reviews/`: 要件・仕様・設計・リリース成果物のレビュー結果。
- `docs/specs/`: 承認済みの製品挙動、契約、受入条件。
- `docs/adr/`: 重要かつ変更コストの高い設計判断。
- `docs/plans/`: 実行計画。仕様やADRの代替にしない。
- `docs/benchmarks/`: AIモデル／Agent等の比較実験、評価方法、結果、再現用snapshotを記録する。製品仕様、ADR、実装Issueの正本にはしない。
- `docs/traceability/`: 要件、仕様、Issue、PR、テスト、リリース証拠の対応関係。
- `docs/releases/`: リリース判定、手順、結果、既知制約。

## 4. 役割

### Koo: Product Owner / Decision Owner

- 要件上の未決事項を決定する。
- 仕様およびADRを承認する。
- 最終的なGo / No-Goを判断する。

### Agent A: Author / Implementer

- 探索、計画、実装、テスト、セルフレビューを行う。
- Draft PRを作成し、検証証拠と未検証事項を明示する。
- 仕様不足を独自解釈で埋めない。

### Agent B: Independent Reviewer

- 実装者の説明を前提にせず、仕様、ADR、Issue、差分、テストの順で再検証する。
- 原則としてレビュー対象のコードを変更しない。
- Blocker / Major / Minor / Nitで指摘を分類する。

### Agent C: Release Reviewer

- Release Candidate、migration、デプロイ、ロールバック、smoke testの証拠を独立検証する。

## 5. 共通作業フロー

1. Parent Issue #3を確認し、現在フェーズ、前提ゲート、Blocker、禁止事項を確認する。
2. 対象Issueと正本を確認する。
3. 対象IssueがParent Issue #3から追跡可能であることを確認する。
4. 対象範囲、対象外、依存関係を確定する。
5. 計画を作成し、仕様・ADRとの整合性をセルフレビューする。
6. 許可された変更だけを実施する。
7. 自動テストと必要な手動検証を実施する。
8. 差分をセルフレビューする。
9. Draft PRに証拠、未検証事項、既知リスクを記録する。
10. Agent Bの独立レビューを受ける。

## 6. 停止条件

次の場合は推測して進めず、未決事項として報告する。

- 原始要件または承認済み仕様に矛盾がある。
- 重要な設計判断にADRが必要だが存在しない。
- Issueの範囲を超える変更が必要である。
- 残高整合性、データ消失、監査証跡に影響する不明点がある。
- 必須検証を実行できない。
- 前提フェーズゲートが`FAIL`または`NOT EVALUATED`である。
- 対象作業がParent Issue #3または対象Issueから追跡できない。
- Parent Issue #3の現在地と依頼された作業フェーズが一致しない。
- Parent Issue #3で次工程の開始が許可されていない。
- 未承認の推奨案を実装へ反映する必要がある。
- プロジェクト目的より機能追加自体が優先されている。

ただし、ゲート再評価、Blocking Decision確定、統制文書更新など、ゲートを通過させるための作業はこの限りではない。前提ゲートが未通過の場合は、その先の工程に着手せず停止する。

## 7. 初期フェーズの禁止事項

要件レビュー、仕様化、必要なADRの承認が完了するまでは、次を実施しない。

- アプリケーション雛形の作成
- DBスキーマまたはmigrationの作成
- APIの実装
- Docker構成の確定
- 本実装用Issueの確定

## 8. セキュリティとデータ

- 実在人物の個人情報を使用しない。
- 実口座、実送金、実金融機関への接続を行わない。
- secret、credential、tokenをリポジトリへ保存しない。
