# PostgreSQL integration test base (FND-03)

実PostgreSQL 18を使用するintegration test基盤。

## 1. 前提

- Dockerが利用可能であること（TestcontainersがDockerデーモンを使用する）
- PostgreSQL imageはdigest固定（下記参照）。テストは常にこのimageで起動する
- InMemory / SQLiteをprovider固有testの代替にしない

## 2. 固定値

| 項目 | 値 |
| --- | --- |
| PostgreSQL image | `postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a` |
| Testcontainers.PostgreSql | `4.13.0`（Central Package Managementで固定） |
| Npgsql | `10.0.3`（同上） |

image referenceは `PostgreSqlContainerFixture.ImageReference` に集約している。`PostgreSqlContainerLifecycleTests.ContainerUsesThePinnedPostgreSql18ImageDigest` が実際に起動したcontainerのimage digestがpinと一致することを検証する。

## 3. 所有単位

| リソース | 所有単位 | 所有者 |
| --- | --- | --- |
| PostgreSQL container | test collection（assembly実行1回につき1つ） | `PostgreSqlContainerFixture`（`ICollectionFixture`） |
| test database | test class（classごとに1つ） | `PostgreSqlTestDatabase`（`IClassFixture`） |
| database内のtable等 | 各test自身 | feature test |

## 4. Lifecycle

- **container**: 最初の利用時（`EnsureStartedAsync`）にlazy起動。collection終了時に`DisposeAsync`で停止・削除。`startLock`（SemaphoreSlim）で多重起動を防止。
- **database**: test class開始前に`CREATE DATABASE`、class終了後に`DROP DATABASE`。database名は `minibank_<owner>_<8桁hex>` 形式で、全test class間で一意（並列実行でも衝突しない）。文字数上限63の範囲内（owner部は44文字で切詰め）。

## 5. Isolation

database単位で分離する。test classごとに専用databaseを持つため、table名・データはclass間で共有されない。`PostgreSqlIsolationTests.ClassDatabasesAreMutuallyIsolated` が2つのclass database間で状態が共有されないことを検証する。

## 6. Cleanup

- class終了時のdatabase dropは `PostgreSqlTestDatabase.DisposeAsync` が実施し、失敗は**黙って無視せず例外として伝播**する（xunitがtest failureとして報告する）。
- container停止・削除はTestcontainersの`DisposeAsync`。containerがリークした場合の保険としてRyuk（Testcontainersのresource reaper）が掃除する。
- `PostgreSqlCleanupFailureTests.DatabaseDropFailureIsSurfacedAndNotSilentlyIgnored` が、drop失敗（SQLSTATE `55006` object_in_use）が例外として表面化することを検証する。
- Npgsqlのconnection poolingはtest database向けconnection stringで無効化している。poolingが有効だとdrop時に残存接続が原因で`55006`になるため。

## 7. Parallel実行方針

- xunitのassembly設定は並列実行無効（`tests/MinimalBankSystem.IntegrationTests/AssemblyInfo.cs`）。これはFND-02のAPI contract testがConsole出力を捕捉するため。本collectionも `DisableParallelization = true` で宣言し、将来assembly設定が変わってもcontainer共有collectionが他collectionと並列にならないことを保証する。
- **並列可能な範囲**: 所有databaseが独立していれば並列実行しても安全（container内のdatabaseは完全分離）。`PostgreSqlParallelExecutionTests.IndependentDatabasesRunConcurrentlyWithoutInterference` が8 workerの並列database作成・書込・削除が干渉なく完了することを検証する。
- **直列化が必要な範囲**:
  - 同一containerを共有するtestは本collection内（xunitがcollection内を直列実行する）
  - 共有のmutable state（例: Console捕捉を伴うtest、同じdatabase・tableを触るtest）を利用するtest
- **shared mutable stateの回避**: container fixtureは起動済みフラグと起動ロックのみ保持。database名は毎回一意生成。static mutable stateは持たない。

## 8. Lifecycle / 接続失敗のfailure reporting

- container起動失敗: Testcontainersの`StartAsync`が例外を投げ、本コードはcatchしないためtest failureになる。`PostgreSqlStartupFailureTests.ContainerStartupFailureIsReportedAsATestFailure` が不正digestで起動失敗が例外として報告されることを検証する。
- 接続失敗: `NpgsqlException`（SQLSTATE付き）が伝播する。`PostgreSqlTestDatabaseLifecycleTests.ConnectionFailureIsReportedAsAnException` が不正passwordで`28P01`が報告されることを検証する。
- database作成失敗: `PostgreSqlTestDatabase.InitializeAsync` が例外を投げ、class全体が明確なfailureになる。

## 9. 実行方法

```text
# 実PostgreSQL integration testのみ
dotnet test tests/MinimalBankSystem.IntegrationTests/MinimalBankSystem.IntegrationTests.csproj --filter "Category=PostgreSql"

# それ以外（unit + API contract等）
dotnet test tests/MinimalBankSystem.IntegrationTests/MinimalBankSystem.IntegrationTests.csproj --filter "Category!=PostgreSql"

# 全体
dotnet test MinimalBankSystem.slnx
```

CI（`.github/workflows/build-test.yml`）は`postgresql-integration` jobで同じ`--filter "Category=PostgreSql"`コマンドを実行する。localとCIで同じcategory / commandを使用し、container起動失敗をskipやsuccessに変換しない。

## 10. 新しいPostgreSQL testの追加方法

1. test classに `[Collection(PostgreSqlTestCollections.Name)]` と `[Trait("Category", "PostgreSql")]` を付与する
2. `IClassFixture<PostgreSqlTestDatabase>` を宣言し、ctorで `PostgreSqlTestDatabase` を注入する（class専用databaseが自動で作成・削除される）
3. containerを直接使う場合は ctorで `PostgreSqlContainerFixture` を注入し、先に `EnsureStartedAsync` を呼ぶ
4. 接続は `PostgreSqlTestDatabase.ConnectionString`、生SQLは `PostgreSqlTestSql` を使う

## 11. Scope境界

- business table・EF Core migration・DbContextは追加しない（FND-04以降の責任）
- feature testの中身は追加しない（各feature Issueの責任）
- test内で作成するtableはすべてtest localであり、class終了時にdatabaseごと削除される
