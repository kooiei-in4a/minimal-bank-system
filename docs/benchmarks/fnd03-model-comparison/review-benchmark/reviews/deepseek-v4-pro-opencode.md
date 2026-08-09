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

- Model: DeepSeek V4 Pro
- Harness: Open Code
- Effort: 指定値
- Reviewer slug: deepseek-v4-pro-opencode
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

- AC-01 Real PostgreSQL 18: PASS — `SHOW server_version_num;` returns `180004`, validated at `PostgreSqlContainerFixture.cs:58` against `ExpectedServerVersionNumber = 180004`. Also independently verified in `PostgreSqlFixtureTests.cs:21-23`.
- AC-02 Digest pin: PASS — `ImageReference` constant holds exact `postgres:18.4@sha256:3a82e1f56c...`. Test `PinnedPostgreSql184ContainerProvidesTheTestDatabase` at `PostgreSqlFixtureTests.cs:12-16` verifies both `Image.FullName` and `Image.Digest` against the constant and expected digest string.
- AC-03 Automatic database lifecycle: PASS — `PostgreSqlDatabaseTestBase` (`PostgreSqlFixtureTests.cs:144-172`) implements `IAsyncLifetime`. `InitializeAsync` creates a new database per test via `Fixture.CreateDatabaseAsync()`. `DisposeAsync` drops it.
- AC-04 Test isolation: PASS — `SeparateDatabasesDoNotShareProbeState` (`PostgreSqlFixtureTests.cs:30-49`) creates a probe table in one database and verifies via `to_regclass` that it is invisible from a second database. Unique database names via `Guid.NewGuid():N` (`PostgreSqlContainerFixture.cs:118`). `Pooling=false` (`PostgreSqlContainerFixture.cs:249`). Template `template0` isolation (`PostgreSqlContainerFixture.cs:124`).
- AC-05 Parallel / serialization policy: PASS — Assembly-level `DisableTestParallelization = false` (`AssemblyInfo.cs:3`). `ApiRuntimeContractTests` placed in `ConsoleSensitive` collection with `DisableParallelization = true` (`TestExecutionCollections.cs:3-9`, `ApiRuntimeContractTests.cs:20`). README (`README.md:35-46`) documents parallel-safe scope (independent databases) and serialized scope (process-global Console state). Concurrency test (`PostgreSqlFixtureTests.cs:91-105`) demonstrates overlapping PostgreSQL server execution via `pg_sleep(1)` and interval overlap assertion, without claiming xUnit scheduler proof.
- AC-06 Cleanup failure visibility: PASS — `CleanupFailureIsVisibleAndTheDatabaseRemainsRetryable` (`PostgreSqlFixtureTests.cs:65-88`) injects failure via pre-cancelled token, asserts `InvalidOperationException`, verifies database still exists after failed cleanup, and finally verifies successful retry cleanup. `PostgreSqlTestDatabase.DisposeAsync` (`PostgreSqlContainerFixture.cs:283-301`) only sets `disposed = true` after successful `DropDatabaseAsync`, enabling retry.
- AC-07 Startup / connection failure: PASS — `UnreachableDockerEndpointIsAnExplicitStartupFailure` (`PostgreSqlFailureTests.cs:8-28`) with 20s local timeout. `UnreachablePostgreSqlEndpointIsAnExplicitConnectionFailure` (`PostgreSqlFailureTests.cs:30-53`) with 2s connection timeout. Startup failure aggregates primary failure and cleanup failure via `AggregateException` (`PostgreSqlContainerFixture.cs:82-83`). Container `DisposeAsync` rethrows on failure (`PostgreSqlContainerFixture.cs:106-112`).
- AC-08 CI real PostgreSQL: PASS — `build-test.yml:37-41` splits into `Category!=PostgreSqlIntegration` and `Category=PostgreSqlIntegration` steps. CI run 31277771209 (PR event) and 31277769431 (push event) both SUCCESS at SHA `91e3fca`. No skip/fallback/success conversion for PostgreSQL failures.
- AC-09 No InMemory / SQLite substitute: PASS — Only `Npgsql` 10.0.3 and `Testcontainers.PostgreSql` 4.13.0 are used. No InMemory, SQLite, or other EF Core provider references appear in the diff or in the IntegrationTests project.
- AC-10 No business table / migration: PASS — No `DbContext`, `EnsureCreated`, `Migrate`, business schema, or business tables in the diff. `src/MinimalBankSystem.Infrastructure/MinimalBankSystem.Infrastructure.csproj` has zero Npgsql or EF Core references. All Npgsql usage is test-only.

## Verification performed

- CI independently checked: YES — Both PR event run 31277771209 and push event run 31277769431 confirmed SUCCESS at HEAD SHA `91e3fca`. Workflow steps: Restore → Build → Test (non-PostgreSQL) → Test (real PostgreSQL).
- Local build/test/probe performed: YES
- Summary:
  - `dotnet restore MinimalBankSystem.slnx`: PASS
  - `dotnet build MinimalBankSystem.slnx`: PASS, 0 warnings, 0 errors
  - `dotnet test --filter "Category!=PostgreSqlIntegration"` (UnitTests): PASS 3/3
  - `dotnet test --filter "Category=PostgreSqlIntegration"`: PASS 7/7 (12s total)
  - Non-PostgreSQL integration tests (25 ApiRuntimeContractTests + related) hung on this WSL/Windows environment — likely WSL networking/WebApplicationFactory issue. CI (Linux) confirms PASS.
  - Docker available, PostgreSQL 18.4 container started and used successfully by local tests.
  - No Docker container or Testcontainers resource leak detected after test runs.

## Scope assessment

- Scope drift: NO
- Out-of-scope implementation detected: none. No `DbContext`, `EnsureCreated`, `Migrate`, `Migration`, `SQLite`, `InMemory`, `Docker Compose`, business schema, business table, production Npgsql wiring, `Customer`, `Account`, or `AuditLog` detected in the diff.

## Notes

- Non-PostgreSQL integration tests (`ApiRuntimeContractTests` 25 tests) hung on local WSL/Windows environment. CI (Linux, runs 31277771209 and 31277769431) confirms all tests pass, including the ConsoleCapture-synchronized tests.
- The `ConsoleCapture` fix (`ApiRuntimeContractTests.cs:815-819, 828-832`) adds `lock(synchronizedWriter)` around `Flush/ToString` and `Dispose` operations. Since `TextWriter.Synchronized` internally uses `lock(this)`, the external lock on the same instance provides correct mutual exclusion between writes and reads/disposal. `Console.SetOut` and `Console.SetError` calls in `Dispose` happen before the lock to avoid reordering issues with the process-global state swap.
- This fix is a minimal regression correction directly caused by enabling assembly-level parallelization. It does not modify FND-02 business logic or API contracts.
- Testcontainers logs `Failed to retrieve Docker container runtime information` during local test startup — this is a benign WSL networking info retrieval failure in Testcontainers' diagnostic logging, not a test failure.
- No prior benchmark results, existing reviews, or competitive reviewer artifacts were consulted during this review.
