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

- Model: Claude Sonnet 5
- Harness: Claude Code
- Effort: xhigh
- Reviewer slug: claude-sonnet-5-claude-code
- Attempt: 1

## Target verification

- Repository: PASS
- PR: PASS (title, base/head ref, base/head SHA all match `pull_request_read get` on #104)
- Base SHA: PASS (`git cat-file -e` succeeds; is ancestor via `git merge-base`)
- Head SHA: PASS (fetched `agent/issue-41-fnd-03-final-code`, `git cat-file -e` succeeds, matches PR head)
- CI SHA: PASS (Run 31277771209 `headSha` == `91e3fca181558cd1523390347f4f2f80d6014d26`, conclusion `success`)

## Verdict

APPROVE

- Blocker: 0
- Major: 0
- Minor: 2
- Nit: 0

## Findings

### F-01 — Minor — Database cleanup-failure injection never reaches the server; it fails at connection-open time

- Blocking: false
- Affected component: `tests/MinimalBankSystem.IntegrationTests/PostgreSql/PostgreSqlFixtureTests.cs:66-84` (`CleanupFailureIsVisibleAndTheDatabaseRemainsRetryable`)
- Description: The test pre-cancels a `CancellationTokenSource` (`cancellation.Cancel()` at line 74) before calling `temporary.DisposeAsync(cancellation.Token)`. `NpgsqlConnection.OpenAsync(CancellationToken)` observes an already-canceled token synchronously and throws before any network I/O, so the `DROP DATABASE` statement is never sent to PostgreSQL. The test genuinely proves the exception-wrapping/non-swallowing/retry contract (verified: it throws `InvalidOperationException`, the database still exists, and a subsequent retry with a fresh token succeeds — reproduced locally), but it does not exercise a real server-side or mid-command cleanup failure (e.g., a blocking session preventing `DROP DATABASE ... WITH (FORCE)`, or a command that starts and then fails). This is a legitimate but shallow failure-injection technique for the "cleanup failure remains retryable" claim.
- Evidence:
  - `PostgreSqlFixtureTests.cs:74` — `cancellation.Cancel();` executed before the call, guaranteeing the token is already canceled at the first await.
  - Local reproduction: `dotnet test ... --filter "Category=PostgreSqlIntegration"` → 7/7 passed including this test, confirming the mechanism works as coded, but confirming it is a pre-flight cancellation, not a genuine in-flight DB failure.
- Proposed root-cause key: N/A

### F-02 — Minor — Container-level `DisposeAsync` failure/retry path has no dedicated test; relies on code inspection and is self-disclosed as unverified

- Blocking: false
- Affected component: `tests/MinimalBankSystem.IntegrationTests/PostgreSql/PostgreSqlContainerFixture.cs:92-110` (`PostgreSqlContainerFixture.DisposeAsync`)
- Description: On a failed `candidate.DisposeAsync()`, the fixture correctly does not swallow the exception and does not null out `container` (so the handle remains retryable) — confirmed by direct code reading. However, no test deterministically injects a Testcontainers-internal `DisposeAsync` failure to prove this path at runtime, unlike the equivalent database-level path which is proven by `CleanupFailureIsVisibleAndTheDatabaseRemainsRetryable`. The PR body itself discloses this gap under "Unverified." This is a coverage gap, not a functional defect — the code is defensible by inspection — but AC-06/AC-07 container-level guarantees rest on static review rather than executed evidence.
- Evidence:
  - `PostgreSqlContainerFixture.cs:92-110`: `catch (Exception exception) { throw new InvalidOperationException(... "The fixture retains ownership so cleanup can be retried." , exception); }` with no `container = null` on the failure path — correct by inspection, not exercised by a test.
  - PR #104 body, "## Unverified": "Deterministic injection of a Testcontainers-internal container `DisposeAsync` failure is not added; production fixture code does not swallow it and retains the container handle for retry."
- Proposed root-cause key: N/A

No other findings met the bar for Minor or above.

## Acceptance Criteria assessment

- AC-01 Real PostgreSQL 18: PASS — reproduced locally: `testcontainers.org` logs show a real Docker container created/started/`pg_isready`-checked/ready, and `SHOW server_version_num` returned `180004` (PostgreSQL 18.4) against the live container.
- AC-02 Digest pin: PASS — independently verified: local `docker image inspect postgres@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a` matches the pinned `ImageReference` exactly (`RepoDigests` = `postgres:18.4@sha256:3a82e1f...`), and the container fixture pulls/starts that exact reference.
- AC-03 Automatic database lifecycle: PASS — `PostgreSqlDatabaseTestBase.InitializeAsync/DisposeAsync` (`IAsyncLifetime`) create/drop a per-test database automatically; verified via code and passing tests.
- AC-04 Test isolation: PASS — `SeparateDatabasesDoNotShareProbeState` passed locally; `template0`-based unique-name `CREATE DATABASE` confirmed in code and via runtime evidence (isolation probe table invisible from the other database).
- AC-05 Parallel / serialization policy: PASS — `[assembly: CollectionBehavior(DisableTestParallelization = false)]` enables cross-collection parallelism; `[CollectionDefinition(..., DisableParallelization = true)]` on `ConsoleSensitiveTestGroup` (applied only to `ApiRuntimeContractTests`, the sole Console.Out/Error user in the whole solution, confirmed by full-repo grep) serializes exactly that collection against everything else — confirmed against the authoritative xUnit XML doc ("Determines whether tests in this collection runs in parallel with any other collections"). The interval-overlap test is explicitly (and correctly) not claimed as proof of xUnit scheduler parallelism, in both code comments and README.
- AC-06 Cleanup failure visibility: PASS (database level, test-proven) with a coverage gap at the container level (see F-02, Minor, not sufficient to fail the AC since code correctly avoids swallowing).
- AC-07 Startup / connection failure: PASS — `UnreachableDockerEndpointIsAnExplicitStartupFailure` and `UnreachablePostgreSqlEndpointIsAnExplicitConnectionFailure` both reproduced locally and pass; both use a synthetic unreachable endpoint (`127.0.0.1:1`) with bounded timeouts (20s / 2s) and did not touch or disrupt unrelated Docker containers on the shared daemon (verified via `docker ps -a` before/after).
- AC-08 CI real PostgreSQL: PASS — CI run 31277771209 (head == PR head) shows `Restore` → `Build` → `Test (non-PostgreSQL)` → `Test (real PostgreSQL)`, all `success`; the real-PostgreSQL step log shows `Passed! ... Total: 7`. Workflow YAML has no `continue-on-error`, skip, or fallback branches.
- AC-09 No InMemory / SQLite substitute: PASS — full-repo grep for `SQLite`/`InMemory` in the diff finds only README prose stating there is no such fallback; no such package/usage added.
- AC-10 No business table / migration: PASS — full-repo grep for `DbContext`/`EnsureCreated`/`Migrate`/`business` in the diff finds only README prose disclaiming these are out of scope; none implemented.

## Verification performed

- CI independently checked: YES
- Local build/test/probe performed: YES
- Summary:
  - Fetched `agent/issue-41-fnd-03-final-code` and confirmed `91e3fca...` == PR #104 head via GitHub API.
  - Confirmed CI run 31277771209 head SHA match and all 4 steps `success`; downloaded and read the full CI log.
  - Independently pulled and inspected the *failing* prior CI run (31277607769, same-branch push before the fix commit) and confirmed the root cause: `System.ArgumentOutOfRangeException` in `StringBuilder.ToString()` inside `ConsoleCapture.get_Content()`, called from `ApiRuntimeContractTests.RejectedRawCorrelationIdIsNotLogged` — matching the PR's claimed diagnosis exactly, not merely trusting the PR narrative.
  - Confirmed the same-SHA `pull_request` run (31277639955) passed while the `push` run failed, independently corroborating the "flaky" characterization.
  - Wrote and ran a standalone empirical program confirming `TextWriter.Synchronized(...).Write()` and an external `lock(synchronizedWriter)` share the same monitor (a thread holding `lock(sync)` for 2000 ms blocked a concurrent `sync.Write()` for ~2009 ms) — proving the fix's lock object choice is technically correct, not merely asserted.
  - Built a git worktree at the exact head commit in an isolated scratch directory (no changes to the tracked repo), ran: `dotnet restore` (PASS), `dotnet build --no-restore` (0 warnings / 0 errors), `dotnet test --filter "Category!=PostgreSqlIntegration"` (30/30, repeated 3× with no flakiness), `dotnet test ... --filter "Category=PostgreSqlIntegration"` (7/7, real Docker container observed created/ready/deleted via Testcontainers logs), full suite `dotnet test` (37/37).
  - Independently verified the pinned image digest via `docker image inspect` against the locally pulled image.
  - Verified via `docker ps -a` before/after that no PostgreSQL/Ryuk containers leaked and unrelated pre-existing containers on the shared daemon were untouched.
  - Cleaned up the worktree and scratch programs after verification; main repository working tree left untouched (`git status` clean throughout).

## Scope assessment

- Scope drift: NO
- Out-of-scope implementation detected: none. Diff-wide search for `DbContext`, `EnsureCreated`, `Migrate`, `SQLite`, `InMemory`, `docker-compose`, `business` found only README prose explicitly disclaiming these as out of scope; no such code was added. Cross-checked against Issue #41 §6/§7, WP-1 (#33) §7, and ADR-0001/0003/0004/0005/0009 — no conflicts, no scope creep into row locking, advisory locks, idempotency, or migration machinery.

## Notes

- No independence violations occurred: I did not access other reviewers' results, PR #104 review threads, Gold Review, benchmark scoring, finding normalization, or `docs/benchmarks/fnd03-model-comparison/*`. I did read PR #104's body as permitted secondary evidence but treated every claim in it as unverified until independently confirmed against code, CI logs, or local execution.
- The PR head SHA and CI target SHA strings in the benchmark prompt (`91e3fca181558cd1523390347f4f2f80d6014d26`) are valid 40-character SHA-1 hashes and were independently confirmed to exist and match PR #104's actual head via the GitHub API; no target-identity discrepancy was found.
- Local verification required creating a temporary git worktree and a small standalone diagnostic program under the session scratchpad directory; both were removed after use, and the tracked repository was not modified, committed, or pushed.
