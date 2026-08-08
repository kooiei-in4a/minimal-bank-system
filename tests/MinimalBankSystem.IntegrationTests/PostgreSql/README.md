# PostgreSQL integration test fixture (FND-03)

This folder owns the real PostgreSQL 18 Testcontainers fixture for provider-specific integration tests.

## Ownership and isolation

| Resource | Owner | Lifetime |
| --- | --- | --- |
| PostgreSQL container | `SharedPostgreSqlContainer` (xUnit collection fixture) | One per `PostgreSqlIntegration` collection |
| Test database | `PostgreSqlTestDatabase` | One per test class (`IAsyncLifetime`) or explicit test scope |
| Schema / tables | Individual tests | Created inside the owned test database |

Tests do not share databases. Each `PostgreSqlTestDatabase` creates a unique database name (`test_{guid}`) on the shared container and drops it during disposal.

## Cleanup responsibility

- `PostgreSqlTestDatabase.DisposeAsync` drops the owned database.
- `PostgreSqlDatabaseCleanup.DropDatabaseAsync` terminates active backends (best effort) before `DROP DATABASE`.
- Cleanup failures throw `PostgreSqlTestCleanupException` and are never swallowed.

## Parallel execution policy

| Scope | Parallel? | Reason |
| --- | --- | --- |
| Integration test assembly (API contract tests) | Serialized | `AssemblyInfo` disables test parallelization for shared mutable API test state |
| PostgreSQL container startup | Serialized | Collection fixture starts one shared container |
| PostgreSQL integration tests using distinct databases | Safe to parallelize | Each test owns a separate database; no shared mutable DB state |
| Operations on the same database | Serialized | Required — one owner per database |

Filter command (local and CI use the same `dotnet test` without category filter; this filter is optional for focused runs):

```bash
dotnet test --filter "Category=PostgreSqlIntegration"
```

## Container image

Pinned image reference (digest fixed):

```text
postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a
```

## Out of scope

- Application DbContext / Npgsql application configuration
- EF Core migrations / business schema
- InMemory / SQLite provider substitutes
