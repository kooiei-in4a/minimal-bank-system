# FND-04 migration runbook

このrunbookは、FND-04が提供するEF Core migration machineryの利用手順である。通常のAPI起動はschemaを変更しない。schema変更は、schemaを所有する後続Issueがsource-controlled migrationを追加し、専用Migratorで適用する。

## 前提

- .NET SDK: `global.json`に固定されたSDK
- EF Core / Design: `10.0.10`
- Npgsql / EF Core provider: `10.0.3`
- repository-local `dotnet-ef`: `10.0.10`
- PostgreSQLの接続設定: `ConnectionStrings:Database`
- 環境変数で渡す場合: `ConnectionStrings__Database`

connection stringとcredentialをリポジトリやcommand-line引数へ保存しない。

## schema-owning Issueがmigrationを追加する手順

1. 対象Issueの受入条件、仕様、ADR、依存ゲートを確認する。
2. `src/MinimalBankSystem.Infrastructure/Persistence`の`BankDbContext`へ、そのIssueが所有するmodelだけを追加する。
3. PostgreSQL接続先を環境変数へ設定し、design-time factoryを使ってmigrationを生成する。

```text
$env:ConnectionStrings__Database = '<postgresql connection string>'
dotnet tool restore
dotnet ef migrations add <MeaningfulMigrationName> `
  --project src/MinimalBankSystem.Infrastructure `
  --startup-project src/MinimalBankSystem.Migrator `
  --context MinimalBankSystem.Infrastructure.Persistence.BankDbContext `
  --output-dir Persistence/Migrations
```

4. 生成されたmigrationとsnapshotをレビューし、所有Issueのbusiness DDL以外が混入していないことを確認する。`EnsureCreated`、startup `Migrate`、SQLite／InMemory fallbackを追加しない。
5. empty databaseからのapply、直前migrationからのrepresentative row upgrade、`HasPendingModelChanges()`を、対象Issueの実PostgreSQL fixtureで検証する。破壊的変更はADR-0009のbackup／restore方針を別途満たす。
6. migrationをsource-controlled codeとしてcommitする。

`InitialFoundation`はFND-04が所有する空のbaselineであり、business table、sequence、trigger、constraintを含めない。最初のnon-empty migrationはschema-owning Issueが所有する。

## 明示的な適用

```text
$env:ConnectionStrings__Database = '<postgresql connection string>'
dotnet run --project src/MinimalBankSystem.Migrator --no-launch-profile
```

Migratorは最大60秒のtimeout／cancellation budgetで実行し、接続、migration、timeoutの失敗時は非0終了する。成功後は次で適用履歴を確認する。

```sql
SELECT migration_id, product_version
FROM public."__EFMigrationsHistory"
ORDER BY migration_id;
```

通常APIは次で起動する。API起動前にMigratorを完了させる。

```text
dotnet run --project src/MinimalBankSystem.Api --no-launch-profile
```

## drift checkとidempotent SQL evidence

pending model differenceは、constantやmigration一覧の比較で代替せず、EF Coreの実mechanismを使う。

```text
dotnet ef migrations has-pending-model-changes `
  --project src/MinimalBankSystem.Infrastructure `
  --startup-project src/MinimalBankSystem.Migrator
```

review／release evidence用のidempotent SQLは次で生成する。これはproduction deploymentそのものではない。

```text
dotnet ef migrations script --idempotent `
  --project src/MinimalBankSystem.Infrastructure `
  --startup-project src/MinimalBankSystem.Migrator
```

生成SQLはレビュー対象として保存し、実適用の成功証拠には専用Migratorの実PostgreSQL結果を使用する。
