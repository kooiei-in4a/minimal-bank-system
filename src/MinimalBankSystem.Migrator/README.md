# MinimalBankSystem.Migrator

`MinimalBankSystem.Migrator` is the only supported way to apply EF Core migrations
(ADR-0009). It is a dedicated one-shot executable that references
`MinimalBankSystem.Infrastructure` and never starts the API host. The normal API startup
path never calls `Database.Migrate`, `EnsureCreated`, or any migration command.

## Connection configuration

The canonical application configuration key is `ConnectionStrings:Database`. Set it as an
environment variable using the .NET double-underscore convention:

```text
ConnectionStrings__Database=Host=...;Port=5432;Database=...;Username=...;Password=...
```

There is no SQLite, InMemory, or other fallback provider. If the variable is not set, the
Migrator and every `dotnet-ef` design-time command fail fast with a clear error instead of
guessing a connection.

## Applying migrations

```bash
ConnectionStrings__Database="Host=localhost;Port=5432;Database=minimalbank;Username=...;Password=..." \
  dotnet run --project src/MinimalBankSystem.Migrator
```

The Migrator applies all pending migrations with a bounded 60-second execution budget
(`MigrationRunner.TimeoutSeconds`). Connection failures, migration failures, and timeouts
all exit with a non-zero process code; only a fully applied migration set exits `0`.

## Adding a schema-owning migration

The `InitialFoundation` migration is an empty baseline: it owns no business table, sequence,
trigger, or constraint. The first non-empty migration is owned by the schema-owning Issue
that needs it. To add one:

1. Restore the repository-local tools once: `dotnet tool restore`.
2. Add the entity/configuration to `MinimalBankSystem.Infrastructure` (owning project for
   `BankDbContext`, provider configuration, and migrations).
3. Generate the migration against a real connection string (required for `--project` /
   `--startup-project` resolution; the command itself does not need to reach the database):

   ```bash
   ConnectionStrings__Database="Host=localhost;Port=5432;Database=minimalbank;Username=...;Password=..." \
     dotnet tool run dotnet-ef migrations add <MigrationName> \
       --project src/MinimalBankSystem.Infrastructure \
       --startup-project src/MinimalBankSystem.Migrator
   ```

4. Review the generated `Up`/`Down` methods; provide a meaningful `Down` unless the change is
   destructive or non-reversible (ADR-0009 rollback rules).
5. Verify there are no pending model changes against the new migration:

   ```bash
   dotnet tool run dotnet-ef migrations has-pending-model-changes \
     --project src/MinimalBankSystem.Infrastructure \
     --startup-project src/MinimalBankSystem.Migrator
   ```

6. Test the upgrade against FND-03's real PostgreSQL fixture: apply to a clean database, and
   for schema-owning changes after `InitialFoundation`, also apply from the immediately
   previous migration with representative existing rows (ADR-0009 forward validation).

## Idempotent SQL for review and release evidence

```bash
dotnet tool run dotnet-ef migrations script --idempotent \
  --project src/MinimalBankSystem.Infrastructure \
  --startup-project src/MinimalBankSystem.Migrator
```

This generates a reviewable SQL script guarded by `__EFMigrationsHistory` checks. It is
evidence for review and release, not a substitute for testing the Migrator itself.
