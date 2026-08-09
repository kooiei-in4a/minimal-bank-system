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
  `PostgreSqlContainerFixture` owns the Docker container by id and reclaims it through an
  independent Docker CLI path. A failed Testcontainers `DisposeAsync` can latch disposed state
  before daemon removal finishes; retrying that poisoned instance is not treated as cleanup
  success. Failed-start errors are preserved together with any partial-container cleanup error,
  and unreclaimed container identity stays owned until daemon removal is confirmed.

This fixture does not provide an application `DbContext`, migrations, business schema, or
business tables. Those remain outside FND-03.

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
