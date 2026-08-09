# Independent Review Result

## Target

* Benchmark ID: fnd03-final-synthesis-independent-review
* Run ID: fnd03-final-91e3fca-20260809
* Repository: kooiei-in4a/minimal-bank-system
* Issue: #41
* PR: #104
* Base SHA: 7946cc55e49c0c6e21ad7b86c20a8435b4976269
* Head SHA: 91e3fca181558cd1523390347f4f2f80d6014d26
* CI target SHA: 91e3fca181558cd1523390347f4f2f80d6014d26

## Reviewer

* Model: ChatGPT Opus 5.6 Sol
* Harness: Browser
* Effort: xhigh
* Reviewer slug: chatgpt-opus-5.6-sol-browser
* Attempt: 1

## Target verification

* Repository: PASS
* PR: PASS
* Base SHA: PASS
* Head SHA: PASS
* CI SHA: PASS

## Verdict

APPROVE

* Blocker: 0
* Major: 0
* Minor: 0
* Nit: 0

## Findings

No findings.

## Acceptance Criteria assessment

* AC-01 Real PostgreSQL 18: PASS
* AC-02 Digest pin: PASS
* AC-03 Automatic database lifecycle: PASS
* AC-04 Test isolation: PASS
* AC-05 Parallel / serialization policy: PASS
* AC-06 Cleanup failure visibility: PASS
* AC-07 Startup / connection failure: PASS
* AC-08 CI real PostgreSQL: PASS
* AC-09 No InMemory / SQLite substitute: PASS
* AC-10 No business table / migration: PASS

## Verification performed

* CI independently checked: YES
* Local build/test/probe performed: NO
* Summary:

  * GitHub PR #104 metadata independently confirms base `7946cc55e49c0c6e21ad7b86c20a8435b4976269` and head `91e3fca181558cd1523390347f4f2f80d6014d26`.
  * Commit comparison confirms Head is exactly two commits ahead of Base with no divergence and limits the target diff to 10 files: CI, central test package pins, IntegrationTests infrastructure/tests/documentation, assembly parallelization policy, and the ConsoleCapture regression fix.
  * `PostgreSqlContainerFixture` constructs `PostgreSqlBuilder` with the exact pinned image `postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a`.
  * `Directory.Packages.props` pins `Testcontainers.PostgreSql` to `4.13.0` and `Npgsql` to `10.0.3`; both references are added only to `MinimalBankSystem.IntegrationTests`.
  * Fixture initialization starts the actual container and reads `SHOW server_version_num;`; initialization rejects anything other than `180004`, and the PostgreSQL fixture test independently queries the per-test database and `SHOW server_version_num`.
  * Each xUnit test instance derives from `PostgreSqlDatabaseTestBase`, creates a GUID-named database from `template0` before the Fact, and drops that database after the Fact. Fixture connection strings use `Pooling=false`.
  * Isolation is exercised against real PostgreSQL by creating `isolation_probe` in one database and verifying `to_regclass` does not see it from another independently created database.
  * `PostgreSqlTestDatabase.DisposeAsync` serializes cleanup with `cleanupGate`, sets its disposed state only after `DROP DATABASE ... WITH (FORCE)` succeeds, and therefore remains retryable after a failed drop.
  * `CleanupFailureIsVisibleAndTheDatabaseRemainsRetryable` injects a cancelled cleanup operation, asserts an explicit failure and continued database existence, retries cleanup in `finally`, then verifies database removal.
  * Fixture container teardown clears its container handle only after successful `DisposeAsync`. Failed teardown propagates an `InvalidOperationException` and retains the handle for a later retry.
  * Fixture initialization failure attempts candidate-container cleanup. When both startup/connection and cleanup fail, the code retains both causes in an `AggregateException`; cleanup exceptions are not caught and discarded.
  * The unreachable-Docker test uses only `tcp://127.0.0.1:1` with a 20-second cancellation boundary. The unreachable-PostgreSQL test uses `127.0.0.1:1` with a 2-second connection timeout. Neither substitutes another provider or manipulates unrelated containers.
  * Assembly-wide xUnit serialization is removed. `ApiRuntimeContractTests`, which mutates process-global `Console.Out` and `Console.Error`, is explicitly assigned to a `DisableParallelization=true` collection. Repository search found those Console replacements only in `ApiRuntimeContractTests`; no environment-variable or current-directory mutation was found.
  * README accurately limits the parallel claim: independently owned databases are parallel-safe, process-global/cluster-global mutations require serialization, and the `Task.WhenAll`/`pg_sleep(1)` test is explicitly described as PostgreSQL-work overlap rather than proof of xUnit scheduler behavior.
  * The parallel PostgreSQL test creates two independent databases, executes `pg_sleep(1)` queries concurrently, records PostgreSQL-side execution intervals, and requires those server-side intervals to overlap.
  * CI incident Run `31277607769` independently shows the pre-fix SHA failing `ApiRuntimeContractTests.RejectedRawCorrelationIdIsNotLogged` with `ArgumentOutOfRangeException` from `StringBuilder.ToString()` in `ConsoleCapture.Content`. This is consistent with a concurrent synchronized write racing an unsynchronized buffer read.
  * Final commit `91e3fca181558cd1523390347f4f2f80d6014d26` locks the synchronized writer around `Flush` plus `buffer.ToString()` and around writer/buffer disposal. This synchronizes reads/disposal with writes without introducing repository-wide serialization.
  * Primary CI Run `31277771209` is associated with the specified Head and succeeds. Its PR checkout is GitHub's synthetic merge `da2f91588acb049322d1479547dde8494749e00d`, explicitly recorded as merging Head `91e3fca...` into Base `7946cc55...`.
  * Primary CI Run `31277771209`: Restore SUCCESS; Build SUCCESS with 0 warnings / 0 errors; non-PostgreSQL tests SUCCESS with Unit 3/3 and Integration 27/27, 0 skipped; real PostgreSQL tests SUCCESS with 7/7, 0 skipped.
  * Secondary push CI Run `31277769431` independently checks out the exact Head `91e3fca181558cd1523390347f4f2f80d6014d26` and also succeeds through Restore, Build, non-PostgreSQL tests and real PostgreSQL tests, with PostgreSQL 7/7 and 0 skipped.
  * No skip, fallback, SQLite or InMemory path exists for the PostgreSQL category. Docker/PostgreSQL startup failures therefore become test failures.
  * The target diff adds no application `DbContext`, `EnsureCreated`, EF migration/migration machinery, business schema/table, Docker Compose configuration, production Npgsql wiring, row/advisory-lock production implementation, or other FND-04+ functionality.
  * The only table introduced is the disposable test-local `isolation_probe`; it is verification DDL inside an isolated disposable database, not business schema or migration machinery.
  * Local execution was not performed because this Browser harness did not expose a usable local repository/.NET/Docker execution environment. GitHub Actions logs were inspected directly instead.

## Scope assessment

* Scope drift: NO
* Out-of-scope implementation detected: none

## Notes

* During Base-SHA existence verification, the GitHub commit-fetch connector automatically expanded the Base commit's own diff and unintentionally displayed a substantial portion of a prohibited FND-03 implementation benchmark evaluation document. Subsequent repository-search results also surfaced prohibited benchmark file paths without substantive additional content. This was accidental exposure; none of that benchmark evaluation content, rankings, candidate findings, or conclusions was used as review evidence or as the basis for this verdict.
* Deterministic runtime injection of a Testcontainers-internal container `DisposeAsync` failure was not available in this harness. The relevant partial-cleanup path was therefore verified by direct code inspection: the primary startup failure and cleanup failure are aggregated, the container handle is cleared only after successful cleanup, and failed cleanup remains retryable. This limitation does not leave an Acceptance Criterion unverified given the database-cleanup failure test, startup-failure test, explicit code path, and successful real-PostgreSQL CI.
