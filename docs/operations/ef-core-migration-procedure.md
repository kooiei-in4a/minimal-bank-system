# EF Core migration procedure

FND-04 owns the EF Core / Npgsql persistence baseline and the explicit migrator.
Business tables are owned by later schema-owning Issues. Do not put Customer,
Account, Operator, Identity, AuditLog, Transaction, Idempotency, or other
business DDL into the FND-04 `InitialFoundation` baseline.

## Fixed versions

| Component | Version |
| :--- | :--- |
| `Microsoft.EntityFrameworkCore` | 10.0.10 |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.10 |
| `Npgsql` | 10.0.3 |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 |
| repository-local `dotnet-ef` | 10.0.10 |

Restore local tools once per clone:

```text
dotnet tool restore
```

## Connection configuration

Canonical key:

```text
ConnectionStrings:Database
```

Environment variable form:

```text
ConnectionStrings__Database
```

Do not commit credential-bearing connection strings. Do not pass passwords as
command-line arguments to the migrator or design-time tools. Design-time and
runtime refuse SQLite / InMemory / fake-provider fallbacks.

## Explicit migrator

Apply pending migrations with the dedicated one-shot project:

```text
dotnet run --project src/MinimalBankSystem.Migrator
```

The migrator:

- uses the Infrastructure `BankDbContext`, Npgsql provider, and migrations assembly
- applies pending migrations with a 60-second timeout / cancellation budget
- exits non-zero on connection, migration, timeout, or pending-model failures
- does not start the API host

Normal API startup must not call `Database.Migrate` / `MigrateAsync`,
`EnsureCreated` / `EnsureCreatedAsync`, or otherwise mutate schema.

## Adding a non-empty business migration (schema-owning Issues)

1. Add entities / configuration in `MinimalBankSystem.Infrastructure` under the
   owning Issue's scope. Do not invent schema outside that Issue.
2. Set `ConnectionStrings__Database` for design-time.
3. Create the migration into the Infrastructure project:

```text
dotnet tool run dotnet-ef migrations add <MigrationName> \
  --project src/MinimalBankSystem.Infrastructure \
  --startup-project src/MinimalBankSystem.Migrator \
  --output-dir Persistence/Migrations
```

4. Review the generated `Up` / `Down` and model snapshot in source control.
5. Verify clean apply on real PostgreSQL via the migrator and FND-03 fixture tests.
6. Verify no pending model changes (see below).
7. For the first non-empty business migration onward, also verify upgrade from the
   previous migration with representative rows (ADR-0009). FND-04 only validates
   clean `0 -> InitialFoundation`.

## Pending model drift check

Standard command:

```text
dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project src/MinimalBankSystem.Infrastructure \
  --startup-project src/MinimalBankSystem.Migrator
```

A temporary model-only change (without a matching migration) must make this
command fail. Discard the temporary change; do not leave synthetic business
entities in the repository.

## Idempotent SQL generation (review / release evidence)

```text
dotnet tool run dotnet-ef migrations script --idempotent \
  --project src/MinimalBankSystem.Infrastructure \
  --startup-project src/MinimalBankSystem.Migrator
```

Treat the generated SQL as review / release evidence, not as the production
deployment mechanism itself. FND-05 owns Compose ordering that runs the migrator
before the API.
