# PostgreSQL Integration Tests

These tests use the real PostgreSQL provider through `Testcontainers.PostgreSql`.
They do not use InMemory, SQLite, EF Core, migrations, or business tables.

## Command

Run the provider-specific category from the repository root after restore and build:

```text
dotnet test --no-build --filter "Category=PostgreSql"
```

Docker startup or connection failures are test failures. The command does not skip,
fallback, or substitute another provider when Docker is unavailable.

## Lifecycle and Isolation

- The PostgreSQL fixture test class owns one PostgreSQL 18.4 container using the pinned image digest.
- Each test instance creates a randomly named `test_<guid>` database before the test runs.
- Each database is dropped with `DROP DATABASE ... WITH (FORCE)` after the test.
- Cleanup is strict and raises `PostgreSqlFixtureException` when the database cannot be removed.
- The fixture refuses to drop the admin database or any database not owned by the fixture.

The fixture supports concurrent operations against independent databases. The integration
assembly remains serialized because the existing FND-02 console-capture tests replace the
process-global `Console.Out`; the parallel test proves the safe parallel scope inside the
PostgreSQL fixture without sharing mutable database state.
