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

- Model: Qwen3.7 Plus
- Harness: Open Code
- Effort: MAX
- Reviewer slug: qwen3.7-plus-opencode
- Attempt: 1

## Target verification

- Repository: PASS
- PR: PASS — #104, base `main` (`7946cc5`), head `agent/issue-41-fnd-03-final-code` (`91e3fca`)
- Base SHA: PASS — `git cat-file -e` succeeded
- Head SHA: PASS — `git cat-file -e` succeeded
- CI SHA: PASS — Run 31277771209 headSha matches `91e3fca181558cd1523390347f4f2f80d6014d26`

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
  - Evidence: `InitializeAsync` calls `SHOW server_version_num` at runtime and asserts `180004`. `PinnedPostgreSql184ContainerProvidesTheTestDatabase` independently queries `SHOW server_version_num` against the test database and asserts `180004`. CI log shows the PostgreSQL step ran for 12s and passed 7 tests.
- AC-02 Digest pin: PASS
  - Evidence: `ImageReference` constant is `postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a`. `PinnedPostgreSql184ContainerProvidesTheTestDatabase` asserts `Fixture.Container.Image.Digest == "sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a"` — runtime evidence from the Testcontainers library, not just a constant comparison.
- AC-03 Automatic database lifecycle: PASS
  - Evidence: `PostgreSqlDatabaseTestBase.InitializeAsync` calls `Fixture.CreateDatabaseAsync()`, `DisposeAsync` calls `candidate.DisposeAsync()`. `PostgreSqlTestDatabase` performs `CREATE DATABASE ... TEMPLATE template0` and `DROP DATABASE ... WITH (FORCE)`. `DisposingADatabaseScopeRemovesTheDatabase` proves the lifecycle end-to-end.
- AC-04 Test isolation: PASS
  - Evidence: Each test gets a uniquely named database (`mbs_test_{Guid}`). `SeparateDatabasesDoNotShareProbeState` creates a table in one database and asserts the other database does not see it. `DROP DATABASE WITH (FORCE)` terminates all sessions. Pooling is disabled.
- AC-05 Parallel / serialization policy: PASS
  - Evidence: `AssemblyInfo.cs` sets `DisableTestParallelization = false`. `ConsoleSensitiveTestGroup` collection has `DisableParallelization = true` for `ApiRuntimeContractTests` which manipulates process-global `Console.Out`/`Console.Error`. `IndependentDatabaseScopesExecuteRealPostgreSqlWorkConcurrently` proves server-side overlap of two real PostgreSQL operations. README documents the parallel/serialized policy accurately.
- AC-06 Cleanup failure visibility: PASS
  - Evidence: `PostgreSqlTestDatabase.DisposeAsync` does not set `disposed = true` on failure, allowing retry. `CleanupFailureIsVisibleAndTheDatabaseRemainsRetryable` injects a pre-cancelled token, asserts the failure message and that the database still exists, then successfully retries cleanup in the `finally` block. `PostgreSqlContainerFixture.DisposeAsync` throws on failure with the container reference retained.
- AC-07 Startup / connection failure: PASS
  - Evidence: `InitializeAsync` wraps startup + cleanup failures into `InvalidOperationException` (with `AggregateException` if both fail). `UnreachableDockerEndpointIsAnExplicitStartupFailure` proves startup failure with a 20s timeout. `UnreachablePostgreSqlEndpointIsAnExplicitConnectionFailure` proves connection failure with a 2s timeout. Neither test has fallback or skip.
- AC-08 CI real PostgreSQL: PASS
  - Evidence: CI Run 31277771209 runs `dotnet test ... --filter "Category=PostgreSqlIntegration"` as a separate step. 7 PostgreSQL tests passed in 12s. No fallback, skip, or success conversion for Docker unavailability. The CI Head SHA matches the PR Head SHA.
- AC-09 No InMemory / SQLite substitute: PASS
  - Evidence: No InMemory or SQLite package references in the diff. No substitute provider in any test file. README explicitly states "There is no InMemory or SQLite fallback."
- AC-10 No business table / migration: PASS
  - Evidence: `git diff` shows zero changes to `src/` and `docs/`. The only `CREATE TABLE` in the diff is `isolation_probe` — a test-scoped table in a fixture-owned database that is dropped after the test. No `DbContext`, `EnsureCreated`, `Migrate()`, or migration files. Npgsql is only referenced in `PostgreSql/` test files and `Directory.Packages.props`.

## Verification performed

- CI independently checked: YES
  - `gh run view 31277771209 --json headSha,status,conclusion,jobs` — headSha matches, conclusion is success
  - CI log confirms: Restore, Build (0 warnings/0 errors), Test non-PostgreSQL (30 passed), Test real PostgreSQL (7 passed, 12s)
  - PR metadata confirmed via `gh pr view 104` — base `7946cc5`, head `91e3fca`, state OPEN, mergeable MERGEABLE
- Local build/test/probe performed: NO
  - `dotnet` SDK 10.0.302 is not installed in the local WSL environment
  - local_verification.performed = false
- Summary:
  - Target identity fully verified through git and GitHub CLI
  - CI provides strong evidence: all steps passed, PostgreSQL tests executed for 12s with 7 tests
  - Code review covered all 10 files in the diff with focus on lifecycle, isolation, failure handling, parallel safety, and scope

## Scope assessment

- Scope drift: NO
- Out-of-scope implementation detected: none
  - `src/` directory: zero changes
  - `docs/` directory: zero changes
  - No DbContext, EnsureCreated, Migrate, Migration, SQLite, InMemory, Docker Compose, business schema, business table, production Npgsql wiring, or application-side configuration

## Notes

- Local dotnet SDK was not available; local build/test was not performed. CI evidence is the primary runtime verification source.
- No accidental exposure to benchmark results, existing reviews, or gold review occurred during this review. The reviewer did not read any of the excluded benchmark documents.
- The PR consists of 2 commits: `e769447` (initial implementation) and `91e3fca` (ConsoleCapture synchronization fix). The ConsoleCapture fix is a minimal, necessary regression fix for the assembly parallelization change — it adds `lock (synchronizedWriter)` around `Content` getter and `Dispose` to prevent concurrent access to the underlying `StringWriter` buffer. The lock object (`synchronizedWriter`) matches the one used internally by `TextWriter.Synchronized()`, ensuring proper mutual exclusion without deadlock risk.
