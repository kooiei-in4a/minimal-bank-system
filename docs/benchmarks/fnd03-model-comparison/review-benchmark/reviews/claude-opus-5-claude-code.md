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

- Model: Claude Opus 5
- Harness: Claude Code
- Effort: xhigh
- Reviewer slug: claude-opus-5-claude-code
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
- Minor: 1
- Nit: 0

## Findings

### F-01 — Minor — Image digest assertions are constant-vs-constant tautologies, not runtime image evidence

- Blocking: false
- Affected component: `tests/MinimalBankSystem.IntegrationTests/PostgreSql/PostgreSqlFixtureTests.cs` (`PinnedPostgreSql184ContainerProvidesTheTestDatabase`)
- Description: The test presents two assertions as digest verification:
  `Assert.Equal(PostgreSqlContainerFixture.ImageReference, Fixture.Container.Image.FullName)` and
  `Assert.Equal("sha256:3a82e1f5…", Fixture.Container.Image.Digest)`.
  `IImage.FullName` / `IImage.Digest` in Testcontainers 4.13.0 are parsed from the reference string
  supplied to `PostgreSqlBuilder`, with no Docker daemon round trip. Both assertions therefore compare
  a constant against a value derived from that same constant in the same process, and cannot fail
  unless the Testcontainers reference parser itself is broken. They contribute no evidence about the
  image the running container was actually created from.
  This does **not** invalidate AC-02: the pin is real and is enforced by Docker, because the
  digest-qualified reference is what is passed to the daemon, and `InitializeAsync` additionally
  performs a genuine runtime guard (`SHOW server_version_num` must equal `180004`). The defect is in
  the strength of the automated verification artifact that Issue #41 §9 asks for ("image digest確認"),
  not in the implementation. A daemon-side check of the started container's actual image ID /
  `RepoDigests` would close the gap.
- Evidence:
  - `tests/MinimalBankSystem.IntegrationTests/PostgreSql/PostgreSqlFixtureTests.cs:10-15` — the two assertions under review.
  - `tests/MinimalBankSystem.IntegrationTests/PostgreSql/PostgreSqlContainerFixture.cs:9-11,30-33` — `ImageReference` constant is the sole input to `new PostgreSqlBuilder(ImageReference)`; `Container.Image` is derived from it.
  - Independent runtime probe (scratch project, Testcontainers.PostgreSql 4.13.0, no Docker call): building `new PostgreSqlBuilder("postgres:18.4@sha256:0000…0000").Build()` and reading the image **without starting it** returned
    `FullName=postgres:18.4@sha256:0000000000000000000000000000000000000000000000000000000000000000` and
    `Digest=sha256:0000000000000000000000000000000000000000000000000000000000000000`.
    A fabricated all-zero digest round-trips unchanged, proving these properties are parse-only.
  - Counter-evidence limiting severity: `PostgreSqlContainerFixture.cs:47-55` throws unless the live container reports `server_version_num == 180004`; local run and CI both exercised this against a real container.
- Proposed root-cause key: N/A

## Acceptance Criteria assessment

- AC-01 Real PostgreSQL 18: PASS
- AC-02 Digest pin: PASS
- AC-03 Automatic database lifecycle: PASS
- AC-04 Test isolation: PASS
- AC-05 Parallel / serialization policy: PASS
- AC-06 Cleanup failure visibility: PASS
- AC-07 Startup / connection failure: PASS
- AC-08 CI real PostgreSQL: PASS
- AC-09 No InMemory / SQLite substitute: PASS
- AC-10 No business table / migration: PASS

Per-criterion evidence:

- **AC-01** — `PostgreSqlContainerFixture.InitializeAsync` opens a real Npgsql connection and reads `SHOW server_version_num;`, failing hard unless it equals `180004` (= 18.4 under the PG 10+ `major*10000+minor` encoding). `PostgreSqlFixtureTests` re-reads it over the per-test database. This is provider evidence, not a constant assert.
- **AC-02** — Digest-qualified reference is the only image input; Docker resolves and content-verifies by digest. See F-01 for the weakness in the *assertion*, not the pin.
- **AC-03** — `PostgreSqlDatabaseTestBase : IAsyncLifetime` creates a uniquely named database (`CREATE DATABASE … TEMPLATE template0`) before each Fact and drops it after. `DisposingADatabaseScopeRemovesTheDatabase` proves removal via `pg_database` lookup.
- **AC-04** — `SeparateDatabasesDoNotShareProbeState` creates a real `isolation_probe` table in one database and proves `to_regclass` is null in the other. Database names are GUID-suffixed; `Pooling=false` on every fixture connection string prevents a pooled session outliving a lease.
- **AC-05** — Documented in `tests/MinimalBankSystem.IntegrationTests/README.md` and enforced in code. I independently verified the enforcement mechanism (see "Verification performed"): a `DisableParallelization = true` collection genuinely does not overlap any other collection in xUnit 2.9.3, so the `Console.Out`/`Console.Error` mutation in `ApiRuntimeContractTests` is fully isolated. The README's explicit disclaimer that the interval-overlap test does not prove xUnit scheduling is honest and correct.
- **AC-06** — `PostgreSqlTestDatabase.DisposeAsync` sets `disposed = true` **only after** a successful drop; `DropDatabaseAsync` wraps and rethrows. `CleanupFailureIsVisibleAndTheDatabaseRemainsRetryable` verifies the full contract against real PostgreSQL: failure surfaces → database still exists → retry on the same lease → database gone. The injection is a pre-cancelled token, which fails at connection open rather than at the `DROP` statement itself; the state transitions it asserts are nevertheless real and deterministic, not accidental. Container-side: I confirmed by probe that xUnit v2 reports a class-fixture `DisposeAsync` throw as `[Test Class Cleanup Failure]` and fails the run even when every test passed.
- **AC-07** — `PostgreSqlFailureTests` covers unreachable Docker endpoint (`tcp://127.0.0.1:1`, 20 s local timeout) and unreachable PostgreSQL endpoint (`127.0.0.1:1`, 2 s connect timeout). Neither touches shared or unrelated Docker state — `WithDockerEndpoint` is per-container configuration, not `TestcontainersSettings`. No `Skip`, `SkippableFact`, or fallback exists anywhere in the test assembly.
- **AC-08** — Verified independently against the GitHub Actions API, not the PR body (details below).
- **AC-09** — Repo-wide grep for `Sqlite|InMemory|DbContext|EntityFrameworkCore|Migrate|Migration|docker-compose` over `*.cs/*.csproj/*.props/*.yml/*.json`: **zero** hits.
- **AC-10** — The only DDL in the diff is `CREATE TABLE isolation_probe` executed inside a throwaway per-test database by the isolation test itself. No migration machinery, no business schema, no persisted table.

## Verification performed

- CI independently checked: YES
- Local build/test/probe performed: YES
- Summary:
  - **Target gate.** `git cat-file -e` confirmed both SHAs; GitHub API confirmed PR #104 `base.sha = 7946cc55…`, `head.sha = 91e3fca1…`, branch `agent/issue-41-fnd-03-final-code`. `git diff --stat 7946cc5…91e3fca` = 10 files, +607/−9, matching the PR's reported counts. Local `main` (cc14526) is an ancestor of the base and was not used as the review target.
  - **CI (independent).** Run `31277771209`: `headSha = 91e3fca181558cd1523390347f4f2f80d6014d26`, event `pull_request`, conclusion `success`. Step-level log inspection: Restore ✔, Build ✔ (0 warnings / 0 errors), `Test (non-PostgreSQL)` → Unit 3/3 + Integration 27/27, `Test (real PostgreSQL)` → **7/7 passed, 0 skipped, 12 s**. The PostgreSQL step is a separate `dotnet test … --filter "Category=PostgreSqlIntegration"` invocation that ran a non-zero number of tests, so a filter that silently matched nothing is excluded. No skip/fallback/success-conversion for Docker unavailability exists in the workflow or the code.
  - **Local build/test** (worktree-free `git archive` of the exact Head into a scratch directory; the repository checkout, branches, and tracked files were not modified). Windows 11, .NET SDK 10.0.302, Docker 29.6.2:
    - `dotnet build MinimalBankSystem.slnx` → succeeded, 0 warnings, 0 errors.
    - `--filter "Category!=PostgreSqlIntegration"` → Unit 3/3, Integration 27/27.
    - `--filter "Category=PostgreSqlIntegration"` → 7/7 (9 s), against a real digest-pinned container.
    - Full single-process suite `dotnet test MinimalBankSystem.slnx --no-build` → Unit 3/3, Integration 34/34.
    - `docker ps -a` before and after: no PostgreSQL container created by the review remained; the only containers present were two unrelated, pre-existing exited containers from other work, which I did not touch. Deterministic teardown confirmed — cleanup does not depend on Ryuk or process exit.
  - **Probe 1 — xUnit 2.9.3 parallelization semantics (decisive for §9.3 and the CI incident fix).** Scratch xUnit project with `[assembly: CollectionBehavior(DisableTestParallelization = false)]`, one `DisableParallelization = true` collection and two ordinary parallel classes, each recording timestamped start/end around a 2 s sleep. Result: the two parallel collections ran concurrently (`22:04:45.783`→`47.795` and `22:04:45.784`→`47.796`), and the non-parallelizable collection ran strictly afterwards and alone (`22:04:47.797`→`49.799`). **A `DisableParallelization = true` collection does not overlap any other collection.** Therefore `ApiRuntimeContractTests`'s process-global `Console.Out`/`Console.Error` replacement cannot be observed or corrupted by concurrently scheduled tests, and no other test can inject non-JSON text into its capture buffer — which matters, because `ParseJsonLogLines` calls `JsonDocument.Parse` on **every** captured line and several tests assert `DoesNotContain("StackTrace"…)` over the whole buffer. The README's parallel/serialize claim matches actual behavior.
  - **Probe 2 — `lock(TextWriter.Synchronized(...))` semantics (root cause of the CI incident fix).** `TextWriter.Synchronized` returns a writer whose members are `MethodImplOptions.Synchronized`, i.e. they lock on the instance. Measured: holding `lock(sync)` on one thread blocked a concurrent `sync.Write` for 806 ms. So the fix in `ConsoleCapture` makes `Flush()`+`buffer.ToString()` atomic with respect to concurrent `Console.Out` writes from ASP.NET Core's background console-logger thread — which is the genuine root cause of the Linux CI race, and a race that exists *within a single test* independent of assembly parallelization (enabling parallelization merely increased contention until it manifested). The same lock makes `Dispose` atomic against in-flight writes. Disposal order is safe in all 11 `ConsoleCapture` usages: `capture` is always declared first and therefore disposed last, after the host (and its `ConsoleLoggerProcessor`, which flushes on dispose) has been torn down. No deadlock risk: the lock is held only across non-blocking, non-reentrant buffer operations. I judge this the minimal correct regression fix — 2 `lock` statements plus one collection attribute — and not an unnecessary alteration of FND-02 scope.
  - **Probe 3 — class-fixture teardown visibility.** A class fixture whose `DisposeAsync` throws produced `[Test Class Cleanup Failure (…)] System.InvalidOperationException` and failed the overall `dotnet test` run even though the test itself passed. Container cleanup failure is therefore genuinely surfaced, not swallowed.
  - **Probe 4 — digest semantics.** See F-01.
  - **Cleanup-swallowing audit.** Every `catch` in the new code (`PostgreSqlContainerFixture.cs:67,76,106,127,170,190,198`) either rethrows a wrapped exception or captures into a local that is aggregated into the thrown `AggregateException`. No empty catch, no swallowed cleanup exception, no `disposed`/handle transition on a failed path. Startup failure and partial-container cleanup failure are both preserved; a failed container dispose keeps `container` non-null so xUnit's later fixture teardown retries it.

## Scope assessment

- Scope drift: NO
- Out-of-scope implementation detected: none. `Npgsql` and `Testcontainers.PostgreSql` appear only as a central `PackageVersion` declaration plus `PackageReference` entries in `tests/MinimalBankSystem.IntegrationTests`; no production project references either, and `Directory.Build.props` adds no implicit package references. No `DbContext`, `EnsureCreated`, `Migrate`, migration machinery, business schema, business table, SQLite, InMemory, Docker Compose, or production Npgsql wiring anywhere in the repository at this Head. The `ApiRuntimeContractTests`/`ConsoleCapture` change is confined to the minimum needed to make the FND-03 parallelization change safe, and does not alter FND-02 behavior or assertions. The workflow change stays within "CIでのreal PostgreSQL integration test". `SolutionConfigurationTests.NuGetVersionsAreCentrallyPinnedExactly` still passes with the two new exact versions.

## Notes

- No benchmark or reviewer material was consulted. I did not open `docs/benchmarks/fnd03-model-comparison/summary.md`, `implementation-evaluation.md`, `docs/benchmarks/fnd02-model-comparison/review-benchmark/`, any existing review or review thread on PR #104, any Gold/Reference review, or any ranking/normalization artifact. No accidental exposure occurred. The PR body was read as secondary evidence only; every claim reported above was re-derived from code, runtime behavior, or the GitHub Actions API. The PR body's own "Candidate learnings adopted" section names other models, which I treated purely as authorship metadata and did not use to seek out any other reviewer's or implementer's output.
- Review-only was maintained: no file edit, commit, push, branch change, PR/Issue comment, or review posting. Local verification used a `git archive` export of the Head into the session scratchpad; the repository checkout, its branches, and all tracked files are unchanged. No unrelated container was stopped or removed and no prune was run.
- Environment caveat: local execution was on Windows + Docker Desktop, whereas the CI incident was Linux-specific and timing-sensitive. My local passes therefore do not by themselves refute a residual race; the parallelization and synchronization probes above, which are platform-independent semantics of xUnit 2.9.3 and `TextWriter.Synchronized`, are what I rely on for that conclusion, together with the green Linux CI run on the exact Head.
- The PR is still in draft state. That is a process observation for the merge gate, not a code finding.
- On §9.1's specific question: the cleanup-failure test is deterministic, not accidentally passing. The pre-cancelled token fails inside `OpenConnectionAsync`, which wraps into `InvalidOperationException`, which `DropDatabaseAsync` wraps again — so `Assert.ThrowsAsync<InvalidOperationException>` (exact-type in xUnit) matches by construction, and the surrounding assertions verify real `pg_database` state before and after the retry. The one honest limitation is that the injection point is the connection rather than the `DROP` statement itself; the observable lease contract exercised is identical, so I did not raise it as a finding.
