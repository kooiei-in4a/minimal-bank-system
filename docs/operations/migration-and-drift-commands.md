# Migration and drift commands (FND-04)

この文書は、`MinimalBankSystem` のEF Core migration基盤を使った標準操作を定義する。Issue #42（FND-04）が確立したmachineryの運用方法であり、製品仕様・ADRを上書きしない。

## 前提

- `MinimalBankSystem.Infrastructure` が `BankDbContext`、provider設定、migrations、snapshot、design-time factoryを所有する。
- migrations assemblyは `MinimalBankSystem.Infrastructure`。
- migrationの適用は専用one-shot executable `MinimalBankSystem.Migrator` のみ。通常のAPI startupはschemaを変更しない。
- canonical connection keyは `ConnectionStrings:Database`。環境変数では `ConnectionStrings__Database`。
- 実行budgetは60秒固定。失敗は非0 exit codeへ伝播する。

## Tool restore

```text
dotnet tool restore
```

repository-local `dotnet-ef` 10.0.10がmanifest（`.config/dotnet-tools.json`）からexact固定される。

## Migration apply

```text
dotnet run --project src/MinimalBankSystem.Migrator
```

`ConnectionStrings__Database` を設定して実行する。`0 -> InitialFoundation` のclean applyでは、`public.__EFMigrationsHistory` へbaselineが記録される。成功はexit 0、失敗（connection / migration / timeout / cancellation）は非0。

## Model drift check（標準command）

```text
dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project src/MinimalBankSystem.Infrastructure \
  --startup-project src/MinimalBankSystem.Migrator
```

pending model changeが無ければ成功、あれば非0終了する。同等のprogrammatic検証として、IntegrationTestsの `PendingModelChangesTests` が実PostgreSQL fixtureを使い `HasPendingModelChanges()` を検証する。

## Idempotent migration SQL（evidence path）

```text
dotnet tool run dotnet-ef migrations script --idempotent \
  --project src/MinimalBankSystem.Infrastructure \
  --startup-project src/MinimalBankSystem.Migrator
```

ADR-0009に従い、review / release evidence用のidempotent SQLを生成する。生成SQLはproduction deploymentそのものではなく、evidence generation pathとして扱う。

## Schema-owning Issue向け migration追加手順

1. このIssueが `MinimalBankSystem.Infrastructure` のmigrationを所有することをScopeへ明記する。
2. 既存の `InitialFoundation` を起点に、model変更を先に実装する（entity、`OnModelCreating`、`DbSet`）。
3. 既存migrationへの追記や既存snapshotの手編集をしない。
4. 新しいmigrationを追加する:

   ```text
   dotnet tool run dotnet-ef migrations add <MigrationName> \
     --project src/MinimalBankSystem.Infrastructure \
     --startup-project src/MinimalBankSystem.Migrator
   ```

5. 生成されたmigrationの `Up` / `Down` をレビューする。representative business rowsを使うupgrade検証（ADR-0009のforward validation）は、最初のnon-empty schema-owning migrationから必須。
6. 生成された `.cs` が block-scoped namespaceで生成された場合は、repositoryのcode style（`.editorconfig` の `csharp_style_namespace_declarations = file_scoped`）へ合わせてfile-scopedへ整形する。
7. 実PostgreSQLでclean `0 -> latest` apply、history inspection、drift check、`--idempotent` SQL生成を検証する。
8. migrationはsource-controlledコードとしてレビューし、migration machineryの再実装はしない。

## 禁止

- API startupでの `Migrate` / `MigrateAsync` / `EnsureCreated` / ad-hoc DDL
- application schema evolutionへの `EnsureCreated`
- SQLite / InMemoryへのprovider固有verificationの代替
- `ConnectionStrings__Database` 未設定時にSQLite / InMemory / fake providerへfallback
