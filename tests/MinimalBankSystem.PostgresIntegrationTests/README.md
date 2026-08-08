# PostgreSQL integration test基盤

実PostgreSQL 18に対するintegration testの共通fixture。Issue #41 / FND-03が所有する。

InMemoryやSQLiteはprovider固有挙動を再現できないため、この基盤の代替として使用しない（ADR-0001 Rejected alternatives）。

## 1. 実行方法

前提として、Docker互換のcontainer runtimeが利用できること。

solution全体（CIと同一のcommand）:

```bash
dotnet test
```

PostgreSQL integration testだけを選択:

```bash
dotnet test --filter "Category=PostgresIntegration"
```

PostgreSQL integration testを除外:

```bash
dotnet test --filter "Category!=PostgresIntegration"
```

container runtimeが無い場合、testはskipされずfailする。fallback providerも用意しない。

## 2. Fixtureの使い方

testごとに専用databaseが欲しい場合は `PostgresIntegrationTest` を継承する。xUnitはtest methodごとにtest classを再生成するため、`Database` はtestごとに新規作成・破棄される。

```csharp
[Trait(PostgresTestCategories.Category, PostgresTestCategories.PostgresIntegration)]
public sealed class MyProviderTests : PostgresIntegrationTest
{
    [Fact]
    public async Task Example()
    {
        await Database.ExecuteAsync("CREATE TABLE probe (id integer PRIMARY KEY)");
        Assert.Equal(0L, await Database.ExecuteScalarAsync<long>("SELECT count(*) FROM probe"));

        await using NpgsqlConnection connection = await Database.OpenConnectionAsync();
        // provider固有の検証（row lock、advisory lock、constraint、trigger等）
    }
}
```

追加のdatabaseが必要な場合（同時実行する2つのdatabaseを比較する等）は `Server.CreateDatabaseAsync()` を使い、`await using` で破棄する。

## 3. 所有単位

| 対象 | 所有単位 | 生成 | 破棄 |
| --- | --- | --- | --- |
| container | test assembly（testプロセス）に1つ | 最初のtestが要求した時点 | assembly実行終了時に`PostgresTestFramework`が削除 |
| database | test 1件に1つ | `PostgresIntegrationTest.InitializeAsync` | `PostgresIntegrationTest.DisposeAsync` |
| connection pool | database 1つに1つ | database生成時 | database破棄時 |

containerは共有するが、共有されるのはserverだけで、testが変更する状態は自分のdatabaseに閉じている。

## 4. Isolation

- databaseは `template0` から作成する。`template0` は接続不可のため、他testの残留物を引き継がない。
- database名は `mbs_<label>_<GUID>` で衝突しない。
- PostgreSQLはadvisory lock、sequence、schema、temporary objectをdatabase単位でscopeするため、schema単位ではなくdatabase単位をisolation境界に採用した。ADR-0004のrow lockとADR-0005のadvisory lock collision testは、この境界で相互干渉なく実行できる。
- test用のprobe tableはtest本体で作成し、databaseごと破棄する。business tableでもmigrationでもない。

## 5. Cleanup

- databaseの破棄責任は、それを生成したtestにある。
- 破棄は `DROP DATABASE ... WITH (FORCE)` で行う。`IF EXISTS` は付けない。既に消えているのはcleanup欠陥であり、無視してよい状態ではない。
- 破棄に失敗した場合は `PostgresTestCleanupException` を送出する。`DisposeAsync` から送出されるため、xUnitは対象testのfailureとして報告する。
- container削除に失敗した場合は `POSTGRES_TEST_CONTAINER_CLEANUP_FAILED` をstderrへ出力したうえで例外を再送出し、run全体を失敗させる。

## 6. Parallel policy

| 範囲 | 挙動 | 理由 |
| --- | --- | --- |
| 別test class（別collection） | 並列実行する | testごとにdatabaseが分離しており共有可変状態がない |
| 同一test class内のtest | 直列実行する（xUnit標準） | 同一collectionのtestは同時実行されない |
| cluster全体を変更するtest | `PostgresClusterScope` collectionに参加させ直列化する | role、`ALTER SYSTEM`、他backendの終了、追加containerの起動などはcluster単位の状態 |
| `CREATE DATABASE` / `DROP DATABASE` | fixture内部のgateで直列化する | PostgreSQLがtemplateと対象databaseをlockするため。retry loopを持ち込まない |

並列度は `[assembly: CollectionBehavior(MaxParallelThreads = 4)]` で固定する。core数に追随させないため、開発機とCI runnerで同じ並列度になり、同時接続数もcontainerの `max_connections` に対して十分小さく保たれる。

`PostgresClusterScope` に参加するtestも、無関係なtestのdatabaseが同時に存在することは前提にしなければならない。cluster全体が静止していることを前提にした表明は書けない。

## 7. Failure reporting

| 事象 | 結果 |
| --- | --- |
| container runtimeへ到達できない | `PostgresTestInfrastructureException`。skipしない |
| pinned imageのcontainerが起動しない | `PostgresTestInfrastructureException`。起動失敗は1度だけ実行され、以降のtestは同じ失敗を即座に報告する |
| serverへ接続できない | `PostgresTestInfrastructureException`（接続先host/port/databaseを含む） |
| 起動したserverがPostgreSQL 18でない | `PostgresTestInfrastructureException` |
| 実行中imageのdigestがpinと一致しない | `PostgresTestInfrastructureException` |
| database cleanupに失敗した | `PostgresTestCleanupException`（対象database名を含む） |

## 8. Image pin

```text
postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a
```

`PostgresTestImage.Reference` が唯一の定義であり、fixtureは起動後に実行中containerのdigestがpinと一致することを検証する。CI workflowの `docker pull` が同じreferenceを使っていることは `PostgresTestPolicyTests` が検証する。

## 9. Out of scope

この基盤はFND-04以降のためのmigration machineryを持たない。DbContext、Npgsql application configuration、EF Core migration、business schema、feature testの中身は含まない。
