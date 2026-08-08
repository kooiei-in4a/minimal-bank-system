# minimal-bank-system

最小銀行システムを題材として、要件定義からシステムリリースまでを一貫してシミュレーションする公開リポジトリです。

## 目的

次の開発方式を組み合わせ、成果物・判断・実装・検証の追跡可能性を実証します。

- 仕様駆動開発
- ADR駆動開発
- AI-PR駆動開発
- 実装者と独立レビュアーの役割分離

## 現在のフェーズ

要件定義レビュー開始前の初期基盤です。アプリケーション実装はまだ開始しません。

## 正本

- 製品挙動: `docs/specs/`
- 重要な設計判断: `docs/adr/`
- 原始要件: `docs/requirements/`
- 作業範囲と進捗: GitHub Issues
- 変更差分と検証証拠: Pull Requests
- 実装状態: コードと自動テスト

## テスト実行

```bash
dotnet test
```

`dotnet test`にはPostgreSQL integration testが含まれ、Docker互換のcontainer runtimeを必要とします。CIも同じcommandを使用します。runtimeが無い場合はskipせずfailします。

PostgreSQL integration testだけを実行する場合は`dotnet test --filter "Category=PostgresIntegration"`、除外する場合は`dotnet test --filter "Category!=PostgresIntegration"`を使用します。fixtureの詳細は`tests/MinimalBankSystem.PostgresIntegrationTests/README.md`を参照してください。

## リリース境界

本リポジトリで構築するシステムは開発手法検証用の内部デモです。実際の銀行業務、実口座、実送金、実顧客データには使用しません。

## 開発開始順序

1. 原始要件のレビュー
2. 未決事項と矛盾の解消
3. 承認済み仕様の作成
4. 重要設計判断のADR化
5. 実装Issueへの分割
6. AI実装と独立レビュー
7. 統合検証と内部デモリリース
