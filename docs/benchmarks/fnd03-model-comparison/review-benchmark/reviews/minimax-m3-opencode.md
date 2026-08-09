# Independent Review Result

## Target

- Benchmark ID: fnd03-final-synthesis-independent-review
- Run ID: fnd03-final-91e3fca-20260809
- Repository: kooiei-in4a/minimal-bank-system
- Issue: #41
- PR: #104
- Base SHA: 7946cc55e49c0c6e21ad7b86c20a8435b4976269
- Head SHA: 91e3fca181558cd1523390347f4f2f80d6014d26
- CI target SHA: 91e3fca181558cd1523390347f4f2f80d6014d26

## Reviewer

- Model: MiniMax M3
- Harness: Open Code
- Effort: 指定値
- Reviewer slug: minimax-m3-opencode
- Attempt: 1

## Target verification

- Repository: PASS
- PR: PASS
- Base SHA: PASS
- Head SHA: PASS
- CI SHA: PASS

`git cat-file -e` で Base/Head 双方の存在を確認。`git diff --stat 7946cc55e49c0c6e21ad7b86c20a8435b4976269...91e3fca181558cd1523390347f4f2f80d6014d26` の結果は10ファイル / +607/-9 であり、変更はテストインフラとCI workflowに限定されている。`origin/agent/issue-41-fnd-03-final-code` の HEAD ref も `91e3fca181558cd1523390347f4f2f80d6014d26` であり、PR Head と一致。`gh run view 31277771209` で CI Run の head SHA = `91e3fca181558cd1523390347f4f2f80d6014d26`、conclusion = success を確認。

## Verdict

APPROVE

- Blocker: 0
- Major: 0
- Minor: 1
- Nit: 0

## Findings

### F-01 — Minor — ConsoleCapture のlock対象と内部lockの不一致

- Blocking: false
- Affected component: tests/MinimalBankSystem.IntegrationTests/ApiRuntimeContractTests.cs:797-833 (ConsoleCapture)
- Description: PR は `lock (synchronizedWriter)` を `Content` getter と `Dispose` に追加したが、`TextWriter.Synchronized(buffer)` が返す `SyncTextWriter` は内部で `lock (_out)` (inner writer = `buffer`) を取るため、new outer lock (`synchronizedWriter`) と inner lock (`buffer`) は別オブジェクトである。これにより、Content 読取時の `buffer.ToString()` は内部 lock と排他にならず、background writer との間で微小なdata raceが残存する。CI evidence (run 31277771209, 27/27 integration + 7/7 PG) と PR body の "5 consecutive runs" からは practical な問題は観測されないが、対象ロックの選択が `TextWriter.Synchronized` の同期単位と一致していない。
- Evidence:
  - `tests/MinimalBankSystem.IntegrationTests/ApiRuntimeContractTests.cs:797-833` で `synchronizedWriter = TextWriter.Synchronized(buffer)` を生成し、Content getter / Dispose を `lock (synchronizedWriter)` で囲む
  - .NET の `TextWriter.Synchronized` は内部で `SyncTextWriter` を生成し、`lock (_out)` で内部writerを保護する (synchronizedWriter 自身ではない)
  - PR diff の head SHA `91e3fca` はこの lock 追加のみの変更 (`git show 91e3fca`)
  - CI run 31277771209 で全 27 integration test と 7 PG test が success
- Proposed root-cause key: N/A

この Minor は FND-02 `ConsoleCapture` の race に対する修正の一部であり、merge ブロッカーではない。CI evidence は fix の主要目的 (Dispose-during-read race) が満たされていることを示している。より厳密な同期にする場合は `lock (buffer)` (inner writer) を lock 対象にする、または `TextWriter.Synchronized` を外して手動 lock に統一する選択肢がある。修正は本 PR の scope (test infrastructure) には影響しないため、Finding として記録するに留める。

## Acceptance Criteria assessment

- AC-01 Real PostgreSQL 18: PASS
  - `PostgreSqlContainerFixture` (cs:8-259) は `PostgreSqlBuilder(ImageReference).Build()` で実際にTestcontainers コンテナを起動する
  - `InitializeAsync` (cs:38-90) で `candidate.StartAsync(...)` を呼び、`ReadServerVersionNumberAsync` で `SHOW server_version_num` を実 PostgreSQL に対して実行
  - 期待値 `180004` との一致を検証 (cs:60-65)
  - CI run 31277771209 で 7/7 通過、Duration 12s は実コンテナ起動を示す
  - ローカル (Windows, Docker Desktop) でも `dotnet test ... --filter "Category=PostgreSqlIntegration"` で 7/7 通過 (10s) を確認

- AC-02 Digest pin: PASS
  - `PostgreSqlContainerFixture.ImageReference` (cs:10-11) は `postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a` を constant で固定
  - `PinnedPostgreSql184ContainerProvidesTheTestDatabase` test (PostgreSqlFixtureTests.cs:9-27) で `Fixture.Container.Image.FullName` と `Fixture.Container.Image.Digest` の両方を runtime assert
  - runtime evidence (cs:12-15) で digest が一致しない場合 test failure

- AC-03 Automatic database lifecycle: PASS
  - コンテナ lifecycle は xUnit `IClassFixture<PostgreSqlContainerFixture>` (PostgreSqlFixtureTests.cs:7) で自動化
  - database lifecycle は `PostgreSqlDatabaseTestBase.InitializeAsync` / `DisposeAsync` (PostgreSqlFixtureTests.cs:155-171) で xUnit テスト instance に紐付け
  - 追加 database scope は `await using PostgreSqlTestDatabase` で各 test が所有
  - README の "Commands" / "Ownership and isolation" セクションで明文化

- AC-04 Test isolation: PASS
  - 各 test が一意 database name を `Guid.NewGuid():N` で取得 (PostgreSqlContainerFixture.cs:118)
  - `CREATE DATABASE ... TEMPLATE template0` で encoding/locale 衝突を回避 (cs:124)
  - `Pooling = false` (cs:249) で pool を跨いだ test 接続残留を防止
  - `SeparateDatabasesDoNotShareProbeState` test (PostgreSqlFixtureTests.cs:29-50) で `public.isolation_probe` table が他 database から見えないことを実 PostgreSQL で検証
  - 追加 database scope は fixture-owned prefix (`mbs_test_`) のみdrop可 (cs:158-162) で safety 確保

- AC-05 Parallel / serialization policy: PASS
  - `AssemblyInfo.cs` で `[assembly: CollectionBehavior(DisableTestParallelization = false)]` (head) により assembly-wide parallelization が enabled
  - `TestExecutionCollections.cs` で `ConsoleSensitive` collection を `DisableParallelization = true` で定義
  - `ApiRuntimeContractTests` に `[Collection(TestExecutionCollections.ConsoleSensitive)]` を追加し、process-global な `Console.Out` / `Console.Error` を扱う test を隔離
  - PostgreSQL test 群は `Category=PostgreSqlIntegration` trait で識別され、CI 上で独立 step として実行
  - README の "Parallel policy" セクションで明文化。`IndependentDatabaseScopesExecuteRealPostgreSqlWorkConcurrently` test は PostgreSQL server-side interval overlap を検証するもので、xUnit scheduling そのものの証拠ではないことも README で明記

- AC-06 Cleanup failure visibility: PASS
  - `PostgreSqlTestDatabase.DisposeAsync` (cs:283-301) は `cleanupGate` 上で cleanup を直列化し、失敗時に `disposed = true` を **設定しない** ことで retry 可能
  - `DropDatabaseAsync` 失敗時は `InvalidOperationException` で wrapping (cs:170-175) し、failure 内容を message に含める
  - `CleanupFailureIsVisibleAndTheDatabaseRemainsRetryable` test (PostgreSqlFixtureTests.cs:65-89) は cancelled `CancellationToken` を渡して cleanup failure を注入し、以下を検証:
    1. `InvalidOperationException` が throw され、message に "Failed to drop isolated PostgreSQL test database" と database name が含まれる (cs:79-80)
    2. failure 後も database が `DatabaseExistsAsync` で true を返す (cs:81)
    3. `finally` block で retry した `DisposeAsync` が成功し、database が削除される (cs:85-88)

- AC-07 Startup / connection failure: PASS
  - `UnreachableDockerEndpointIsAnExplicitStartupFailure` test (PostgreSqlFailureTests.cs:9-28) は `tcp://127.0.0.1:1` を endpoint として 20秒 timeout で起動を試み、`InvalidOperationException` 発生と message 内容を検証
  - `UnreachablePostgreSqlEndpointIsAnExplicitConnectionFailure` test (PostgreSqlFailureTests.cs:31-53) は unreachable port 1 への Npgsql 接続を試み、`InvalidOperationException` 発生と message 内容を検証
  - `PostgreSqlContainerFixture.InitializeAsync` (cs:38-90) は startup 失敗時に partial cleanup failure を `AggregateException` で primary exception に wrap する
  - `OpenConnectionAsync` (cs:178-211) は connection 失敗時に connection dispose failure を `AggregateException` で primary exception に wrap する
  - CI workflow に `continue-on-error` / `if: always()` 等の fallback 条件は無く、failure は hard fail する (`build-test.yml:37-41`)

- AC-08 CI real PostgreSQL: PASS
  - `.github/workflows/build-test.yml:40-41` で `Test (real PostgreSQL)` step が `dotnet test tests/MinimalBankSystem.IntegrationTests/MinimalBankSystem.IntegrationTests.csproj --no-build --filter "Category=PostgreSqlIntegration"` を実行
  - CI run 31277771209 の log で `Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 12 s` を確認
  - run の head SHA は `91e3fca181558cd1523390347f4f2f80d6014d26` (PR Head と一致)
  - skip / fallback 機構なし

- AC-09 No InMemory / SQLite substitute: PASS
  - 新規追加された test ファイル (PostgreSqlContainerFixture.cs, PostgreSqlFixtureTests.cs, PostgreSqlFailureTests.cs) には Npgsql のみを使用
  - grep 結果: src/ 配下に `Sqlite` / `InMemory` / `Migration` / `EnsureCreated` の使用なし
  - 新規 test は全件 `[Trait("Category", "PostgreSqlIntegration")]` で実 PostgreSQL のみ対象

- AC-10 No business table / migration: PASS
  - diff 内の business schema / table / migration 追加なし
  - grep 結果: `Customer` / `Account` / `Migration` / `EnsureCreated` / `DbContext` の新規使用なし
  - Npgsql / Testcontainers は test プロジェクトのみ (csproj 確認: IntegrationTests/MinimalBankSystem.IntegrationTests.csproj にのみ PackageReference)

## Verification performed

- CI independently checked: YES
  - `gh run view 31277771209` で CI run の存在、head SHA、conclusion、jobs を確認
  - log を取得し、Restore / Build / non-PG test / PG test の 4 step 全て success を確認
  - PG step で `Passed! 7/7` を確認
- Local build/test/probe performed: YES
  - `dotnet restore MinimalBankSystem.slnx --verbosity minimal`: success
  - `dotnet build MinimalBankSystem.slnx --no-restore --verbosity minimal`: success, 0 warnings / 0 errors
  - `dotnet test tests/MinimalBankSystem.IntegrationTests/MinimalBankSystem.IntegrationTests.csproj --no-build --filter "Category=PostgreSqlIntegration"`: 7/7 passed in 10s
  - `dotnet test tests/MinimalBankSystem.IntegrationTests/MinimalBankSystem.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~PinnedPostgreSql184ContainerProvidesTheTestDatabase"`: 1/1 passed
  - `dotnet test MinimalBankSystem.slnx --no-build --list-tests`: 27 integration + 3 unit + 7 PG tests が discovery されることを確認
  - Summary: PostgreSQL test 7/7 を実 Docker コンテナで通過。`ApiRuntimeContractTests` (FND-02) は local Windows testhost で process crash するが、これは base commit `7946cc5` でも再現する pre-existing な Windows 環境依存問題であり、本 PR が原因ではない。CI (Linux) では 27/27 通過しており、PR の verification evidence としては十分。

## Scope assessment

- Scope drift: NO
- Out-of-scope implementation detected: none
  - diff は test インフラ (PostgreSqlContainerFixture.cs, PostgreSqlFixtureTests.cs, PostgreSqlFailureTests.cs) と integration test project の package reference 追加、CI workflow の test step 分割、README 追加、FND-02 の parallelization 安全化 (ConsoleCapture lock + ConsoleSensitive collection) のみ
  - src/ 配下 (Application / Domain / Infrastructure / Api) には変更なし
  - DbContext / Migration / business schema / table / 追加 API の導入なし
  - Npgsql / Testcontainers は IntegrationTests project のみ (csproj 確認済み)
  - Docker Compose / production row lock / advisory lock / Customer / Account / money / auth / health endpoint への先取りなし
  - FND-02 `ConsoleCapture` への変更 (F-01) は、assembly parallelization 有効化に必要な最小回帰修正 (lock 追加 4 行 net + collection 属性 1 行 + collection 定義 9 行) であり、FND-02 責任を逸脱する FND-04 領域 (DbContext / migration / business schema) への先取りはない

## Notes

- local Windows (Docker Desktop 4.85.0) で `ApiRuntimeContractTests` 系の ConsoleCapture を使う test が testhost process crash を起こす。本 PR の head `91e3fca` だけでなく base `7946cc5` でも再現するため、pre-existing な FND-02 由来の問題であり、本 PR (FND-03) が原因ではない。CI (Linux) では 27/27 通過しているため、PR verification evidence としては問題なし。
- レビュー中、benchmark scoring 結果 / 他 reviewer の verdict / Gold Review は一切参照していない (`docs/benchmarks/fnd03-model-comparison/summary.md` 等は開いていない)。`docs/benchmarks/fnd02-model-comparison/review-benchmark/` 配下も参照していない。PR #104 上の既存 review / inline thread も参照していない。
- `Microsoft.NET.Test.Sdk 18.8.1` という version は Release された version として実在する .NET testing SDK (preview を含む) であるかの独立確認はしていないが、CI で restore / build / test が成功しているため、functional な問題は無い。
