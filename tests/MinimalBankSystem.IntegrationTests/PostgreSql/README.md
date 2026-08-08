# PostgreSQL integration test fixture (FND-03)

## Purpose

Provide a reproducible real PostgreSQL 18 Testcontainers fixture so later provider-specific
tests (locks, constraints, migrations) do not use InMemory or SQLite substitutes.

## Image pin

```text
postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a
```

Constant: `PostgreSqlTestImage.Reference`

## Package

- `Testcontainers.PostgreSql` **4.13.0** (Central Package Management)
- `Npgsql` is used only inside tests to open ADO.NET connections. Application DbContext /
  Npgsql configuration / EF migrations are out of scope for FND-03.

## Ownership / lifecycle

| Unit | Owner | Notes |
| --- | --- | --- |
| Container | `SharedPostgreSqlContainer` (process-wide) | Started once; startup is serialized. Start failures throw hard test failures. |
| Database | `PostgreSqlTestDatabase` (per test) | Unique `t_{guid}` database created before the test body and dropped on dispose. |
| Schema / tables | Individual test | Temporary probe objects only; no business schema. |

## Isolation

Each test that needs PostgreSQL calls `PostgreSqlTestDatabase.CreateAsync()` and works only
against that database's connection string. Tests must not write into the shared `postgres`
maintenance database.

## Cleanup

- Normal path: `DisposeAsync` / `CleanupAsync(terminateBackends: true)` terminates backends,
  then `DROP DATABASE`.
- Cleanup exceptions are wrapped in `InvalidOperationException` and **never swallowed**.
- Do not catch-and-ignore dispose failures in callers.

## Parallel execution policy

| Scope | Policy |
| --- | --- |
| Shared container startup | Serialized via process-wide gate |
| PostgreSQL tests with per-test databases | May run in parallel (`MaxParallelThreads = 4`) |
| API runtime contract tests (`ApiRuntimeContract` collection) | Serialized with each other (shared `Console` capture) |
| Tests that mutate container-level settings | Must serialize / avoid shared mutable state |

Category trait: `Category=PostgreSql`

## Commands

Same commands locally and in CI (no SQLite / InMemory fallback):

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

PostgreSQL category only:

```bash
dotnet test --filter "Category=PostgreSql"
```

Docker must be available. Container start or connection failure is a **failed** test, not a skip.

## Out of scope (do not add here)

- Application DbContext
- EF Core migrations / business tables
- Docker Compose runtime
- Production row lock / advisory lock implementation
