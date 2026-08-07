# minimal-bank-system

最小銀行システムを題材として、要件定義からシステムリリースまでを一貫してシミュレーションする公開リポジトリです。

## 目的

次の開発方式を組み合わせ、成果物・判断・実装・検証の追跡可能性を実証します。

- 仕様駆動開発
- ADR駆動開発
- AI-PR駆動開発
- 実装者と独立レビュアーの役割分離

## 現在のフェーズ

進捗、フェーズ、ゲートの正本はParent Issue #3です。現在はWP-1 Foundationの実装段階で、solutionとCIの基盤のみを構築しています。業務機能、DBスキーマ、Docker構成はまだ実装しません。

## 正本

- 製品挙動: `docs/specs/`
- 重要な設計判断: `docs/adr/`
- 原始要件: `docs/requirements/`
- 作業範囲と進捗: GitHub Issues
- 変更差分と検証証拠: Pull Requests
- 実装状態: コードと自動テスト

## リリース境界

本リポジトリで構築するシステムは開発手法検証用の内部デモです。実際の銀行業務、実口座、実送金、実顧客データには使用しません。

## ビルドとテスト

必要なSDKは`global.json`で固定しています（.NET SDK 10.0.302）。

リポジトリ標準コマンドは次の3つです。CIもこの3つをそのまま実行します。

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

警告・アナライザー方針は`Directory.Build.props`と`.editorconfig`に集約し、NuGetのバージョンは`Directory.Packages.props`で一元的に固定しています。CI側で追加のビルドフラグは指定しません。

## 開発開始順序

1. 原始要件のレビュー
2. 未決事項と矛盾の解消
3. 承認済み仕様の作成
4. 重要設計判断のADR化
5. 実装Issueへの分割
6. AI実装と独立レビュー
7. 統合検証と内部デモリリース
