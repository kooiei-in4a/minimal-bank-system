# PostgreSQL integration tests

PostgreSQL provider固有の検証は、SQLiteやInMemoryへfallbackせず、次の同一コマンドで実行する。

```powershell
dotnet test --no-build --filter "Category=PostgreSqlIntegration"
```

CIもこのコマンドを使用する。Docker daemonまたは指定イメージの起動に失敗した場合、testはskipやsuccessへ変換せず、fixtureの明示的なfailureとして失敗する。

## Lifecycle and isolation

- `PostgreSqlContainerFixture`が`PostgreSQL integration` collectionごとに、digest固定のPostgreSQL 18 containerを1つ所有する。
- `PostgreSqlIntegrationTestBase`が各test開始時にUUID付きの専用databaseを作成し、終了時に`DROP DATABASE ... WITH (FORCE)`で削除する。
- database生成、接続、cleanupの失敗は例外をwrapしてtest failureとして通知する。cleanupを握りつぶさない。
- この基盤はschema、migration、DbContext、business tableを作成しない。各featureが必要なschemaを自分のtest database内で所有する。

## Parallel policy

- container lifecycleを共有する`PostgreSQL integration` collectionのtest methodは直列化する。
- 各testには独立databaseを割り当てるため、database leaseの同時作成・接続・cleanupは安全に並列化できる。この範囲は`ParallelDatabaseLeasesRemainIndependent`で実PostgreSQLに対して検証する。
- process間ではcontainerを共有しない。将来、shared mutable databaseを必要とするtestを追加する場合は、このcollectionに配置して直列化し、専用databaseを再利用しない。
