# Integration test policy

## Commands

After restore and build, use the same category split as CI:

```text
dotnet test MinimalBankSystem.slnx --no-build --filter "Category!=PostgreSqlIntegration"
dotnet test tests/MinimalBankSystem.IntegrationTests/MinimalBankSystem.IntegrationTests.csproj --no-build --filter "Category=PostgreSqlIntegration"
```

The PostgreSQL category requires Docker and the pinned image. Docker startup, image pull,
PostgreSQL connection, and cleanup failures fail the test. There is no InMemory or SQLite
fallback.

## Ownership and isolation

- Container ownership: an xUnit test class using `PostgreSqlContainerFixture` owns one
  digest-pinned PostgreSQL container. xUnit deterministically disposes that class fixture.
- Database ownership: each test instance owns one uniquely named database created before the
  test and dropped after it. Extra database scopes created by a test have the same explicit
  `await using` ownership.
- Isolation boundary: a database, created from `template0`; schemas, sessions, advisory locks,
  and other database-scoped state are not shared between tests.
- Pooling is disabled for fixture connection strings so pooled test sessions cannot outlive a
  database lease.
- Cleanup ownership: `PostgreSqlTestDatabase` performs `DROP DATABASE ... WITH (FORCE)` and only
  becomes disposed after a successful drop. A failed cleanup is reported and remains retryable.
  `PostgreSqlContainerFixture` creates a unique ownership label before the container create
  request and keeps an independent Docker resource owner for that label. The container ID and
  the Testcontainers object are not the cleanup identity, so a partial create remains recoverable
  even when `candidate.Id` is unavailable.

## Container cleanup contract

- Testcontainers 4.13.0 latches its disposed state before Docker removal completes. If removal
  fails, a second `DisposeAsync()` on that same instance can return without retrying removal.
- Native Testcontainers disposal is therefore attempted at most once during the fixture lifetime.
  Regardless of native success, failure, or no-op behavior, the independent owner lists all
  containers with the unique ownership label (`All=true`), force-removes every match, and lists
  again.
- The fixture releases the Testcontainers handle, ownership label, and independent owner only
  after the second label query succeeds and reports zero matching containers. Docker query,
  transport, authentication, daemon, and removal failures are cleanup failures, never evidence
  that a container is absent.
- Native cleanup failures remain visible even when independent cleanup succeeds. If both paths
  fail, both exceptions remain visible and the owner stays retryable; a later retry uses only the
  independent path and never calls the poisoned Testcontainers instance again.
- Startup failure uses the same state machine, preserving the primary startup failure together
  with any cleanup failure. Explicit endpoints and the platform/`DOCKER_HOST` endpoint resolved
  by Testcontainers are shared with the independent Docker client.
- Resource Reaper and process termination are defense-in-depth only, not the final cleanup
  guarantee. Cleanup failures are not swallowed.

This fixture does not provide an application `DbContext`, migrations, business schema, or
business tables. Those remain outside FND-03.

## Migration tests

`PostgreSql/MigrationBaselineTests` reuses the FND-03 fixture and runs the production Migrator
process against independently owned real databases. It verifies clean apply, rerun, missing,
unreachable, rejected credential, malformed history and actual 60-second timeout paths. The
credential failure also proves a sentinel password is absent from stdout and stderr.

API tests compare real PostgreSQL schema state before and after startup and after resolving the
production `BankDbContext`. Model tests use EF's pending-model mechanism and generated SQL rather
than migration-name constants alone.

`Persistence/DesignTimeConnectionSafetyTests` is the C8-M01 regression. It launches repository-
local `dotnet-ef database update` as a child process, removes `ConnectionStrings__Database` only
from that child, and verifies fail-closed behavior without a fabricated localhost, fake provider
or ambient destination.

## Parallel policy

- Parallel-safe scope: work against independently owned databases. The concurrency test verifies
  that two real PostgreSQL operations overlap; it does not claim to prove xUnit scheduling.
- xUnit assembly parallelization is enabled so safe test classes are not globally serialized.
  Test methods in one class remain serialized by xUnit, while different safe classes may run in
  parallel.
- Serialized scope: tests that replace process-global state, or intentionally manipulate shared
  cluster-wide state. `ApiRuntimeContractTests` is in a collection with
  `DisableParallelization = true` because it replaces `Console.Out` and `Console.Error`.
- A future cluster-wide test must use its own `DisableParallelization = true` collection and must
  not reuse an independently owned test database as shared mutable state.
