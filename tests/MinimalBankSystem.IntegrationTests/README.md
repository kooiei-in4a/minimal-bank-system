# PostgreSQL integration tests

The tests tagged with `Category=PostgreSql` use a real PostgreSQL 18 container. Docker is required; container startup or connection failure is a test failure and is never converted to a skip or a SQLite/InMemory fallback.

Run only this category with:

```text
dotnet test --filter "Category=PostgreSql"
```

Each test class owns one Testcontainers PostgreSQL container. Each test obtains a unique database inside that container and owns its cleanup lease. Tests in different classes may run in parallel because they use separate containers; tests that share a database must remain within one test's lease and are serialized by the database connection/session. Cleanup exceptions propagate from the lease or fixture and fail the test run.

The PostgreSQL image is fixed to:

```text
postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a
```
