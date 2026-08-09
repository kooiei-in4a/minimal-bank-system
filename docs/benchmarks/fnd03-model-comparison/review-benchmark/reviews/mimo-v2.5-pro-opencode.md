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

- Model: MiMo-V2.5-Pro
- Harness: Open Code
- Effort: 指定値
- Reviewer slug: mimo-v2.5-pro-opencode
- Attempt: 1

## Target verification

- Repository: PASS
- PR: PASS
- Base SHA: PASS
- Head SHA: PASS
- CI SHA: PASS

## Verdict

APPROVE

- Blocker: 0
- Major: 0
- Minor: 0
- Nit: 0

## Findings

No findings.

## Acceptance Criteria assessment

- AC-01 Real PostgreSQL 18: PASS
  - `PostgreSqlContainerFixture` uses `postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a`. `ReadServerVersionNumberAsync` executes `SHOW server_version_num;` on the actual container and asserts `ExpectedServerVersionNumber == 180004`. `PinnedPostgreSql184ContainerProvidesTheTestDatabase` independently verifies this via the test database connection string. Local execution confirmed PostgreSQL 18.4 container starts and all 7 tests pass.
- AC-02 Digest pin: PASS
  - `ImageReference` constant includes `@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a`. Test asserts `Fixture.Container.Image.Digest` equals the expected digest at runtime, not only a constant comparison.
- AC-03 Automatic database lifecycle: PASS
  - `PostgreSqlDatabaseTestBase` implements `IAsyncLifetime`: `InitializeAsync` creates a uniquely named database via `CreateDatabaseAsync`, `DisposeAsync` drops it via `DropDatabaseAsync`. Each test gets an isolated database with a GUID-based name (`mbs_test_{guid}`). Lifecycle is fully automated by xUnit.
- AC-04 Test isolation: PASS
  - Each test creates its own database from `template0`. `SeparateDatabasesDoNotShareProbeState` inserts data into one database and verifies it does not exist in another. Database names use `Guid.NewGuid():N` ensuring uniqueness. Connection pooling is disabled (`Pooling = false`).
- AC-05 Parallel / serialization policy: PASS
  - `AssemblyInfo.cs` sets `DisableTestParallelization = false`. `TestExecutionCollections.cs` defines `ConsoleSensitive` collection with `DisableParallelization = true`. `ApiRuntimeContractTests` is assigned to this collection via `[Collection(TestExecutionCollections.ConsoleSensitive)]`. `PostgreSqlFixtureTests` uses `IClassFixture<PostgreSqlContainerFixture>` for shared container, per-test databases. README documents the policy explicitly: parallel-safe scope (independent databases), serialized scope (Console-sensitive tests), and future cluster-wide test guidance.
- AC-06 Cleanup failure visibility: PASS
  - `PostgreSqlTestDatabase.DisposeAsync` calls `owner.DropDatabaseAsync` which throws `InvalidOperationException` on failure — it does not swallow exceptions. `CleanupFailureIsVisibleAndTheDatabaseRemainsRetryable` test injects failure via pre-cancelled `CancellationToken`, asserts the exception message, verifies database still exists, then successfully retries disposal. The `disposed` flag is only set after successful drop, enabling retry.
- AC-07 Startup / connection failure: PASS
  - `InitializeAsync` catches startup exceptions, attempts container cleanup, and re-throws as `InvalidOperationException` with both the startup and any cleanup exceptions wrapped in `AggregateException`. `UnreachableDockerEndpointIsAnExplicitStartupFailure` tests this with `tcp://127.0.0.1:1`. `UnreachablePostgreSqlEndpointIsAnExplicitConnectionFailure` tests `OpenConnectionAsync` with an unreachable endpoint. Neither test falls back or skips.
- AC-08 CI real PostgreSQL: PASS
  - CI workflow `build-test.yml` splits tests into two steps: `Test (non-PostgreSQL)` with `--filter "Category!=PostgreSqlIntegration"` and `Test (real PostgreSQL)` with `--filter "Category=PostgreSqlIntegration"`. CI Run #31277771209 head SHA matches PR head SHA exactly, status completed/success. Both steps succeeded. No skip/fallback/success conversion for Docker or PostgreSQL failures.
- AC-09 No InMemory / SQLite substitute: PASS
  - No `DbContext`, `EnsureCreated`, `Migrate`, `SQLite`, or `InMemory` references exist in the PostgreSQL test code. README explicitly states: "This fixture does not provide an application DbContext, migrations, business schema, or business tables." The only references to these terms are in the README's exclusion statements.
- AC-10 No business table / migration: PASS
  - The `isolation_probe` table in `SeparateDatabasesDoNotShareProbeState` is a test-only probe table created and destroyed within a single test method. No business schema, EF Core migrations, or `EnsureCreated` calls exist anywhere in the diff.

## Verification performed

- CI independently checked: YES
  - PR #104 head SHA `91e3fca181558cd1523390347f4f2f80d6014d26` matches CI Run #31277771209 head SHA
  - CI status: completed / success
  - All 10 steps (including Test (non-PostgreSQL) and Test (real PostgreSQL)) succeeded
- Local build/test/probe performed: YES
  - `dotnet restore MinimalBankSystem.slnx` — success
  - `dotnet build MinimalBankSystem.slnx --no-restore` — 0 warnings, 0 errors
  - `dotnet test --filter "Category=PostgreSqlIntegration"` — 7/7 tests passed (19.8s)
  - Docker container `b2038d905b1d` (failure test) and `893f634c26f1` (fixture tests) created and deleted
  - Testcontainers confirmed Docker server version 29.6.2, PostgreSQL 18.4 image pulled and started

## Scope assessment

- Scope drift: NO
- Out-of-scope implementation detected: none

## Notes

- The `ConsoleCapture` race fix (adding `lock(synchronizedWriter)` to `Content` getter and `Dispose`) is a minimal regression fix required by the assembly parallelization change in this PR. It modifies only FND-02-owned code (`ApiRuntimeContractTests.cs`) to accommodate the `DisableTestParallelization = false` change. The scope is justified as a necessary prerequisite for parallelization.
- `IndependentDatabaseScopesExecuteRealPostgreSqlWorkConcurrently` uses `Task.WhenAll` within a single test to demonstrate concurrent PostgreSQL operations on independent databases. This proves application-level concurrency capability but does not claim to prove xUnit scheduler parallelism, consistent with the README's honest documentation.
- Local verification was performed on Windows/WSL2 with Docker Desktop 29.6.2. The `UnreachableDockerEndpointIsAnExplicitStartupFailure` test succeeded in ~5s (within the 20s timeout), confirming the failure path works correctly in this environment.
- No benchmark result documents, existing reviewer results, or gold reviews were consulted during this review.
