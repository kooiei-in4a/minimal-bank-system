# Integration test policy

## Commands

After `dotnet restore` and `dotnet build --no-restore`, run the same categories used by CI:

```text
dotnet test --no-build --filter "Category!=PostgreSqlIntegration"
dotnet test tests/MinimalBankSystem.IntegrationTests/MinimalBankSystem.IntegrationTests.csproj --no-build --filter "Category=PostgreSqlIntegration"
```

The PostgreSQL category requires an available Docker engine. An unavailable engine, an image pull failure, or a connection failure is a test failure; there is no InMemory or SQLite fallback.

## PostgreSQL ownership and isolation

- xUnit owns one `PostgreSqlContainerFixture` and therefore one digest-pinned PostgreSQL container per test class.
- Each test owns a `PostgreSqlTestDatabase` created with a unique database name.
- Disposing the database scope terminates remaining sessions and drops that database. Cleanup exceptions are propagated to the test.
- Disposing the class fixture removes the container. Startup, connection, and container cleanup exceptions include the failing lifecycle operation and image reference.
- No application `DbContext`, migration, business schema, or business table is part of this fixture.

## Parallel execution

- Test methods in one xUnit class remain serialized and share only that class's container; their databases never overlap in ownership.
- Different test classes may run in parallel. Each class has its own container, and each test still has its own database.
- Concurrent work inside one test is allowed only across independently owned database scopes.
- Tests that mutate process-wide state or intentionally share a database must use a collection with `DisableParallelization = true`. `ApiRuntimeContractTests` uses this policy because it temporarily replaces `Console.Out` and `Console.Error`.
