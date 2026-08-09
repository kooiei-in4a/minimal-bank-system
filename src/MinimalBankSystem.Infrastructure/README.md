# Application persistence and migrations

`MinimalBankSystem.Infrastructure` owns `BankDbContext`, the Npgsql provider configuration,
source-controlled migrations, the model snapshot, and the design-time factory. The normal API
startup path only registers `BankDbContext`; it never applies migrations or creates schema.

## Connection configuration

Set the canonical .NET configuration key through the environment:

```text
ConnectionStrings__Database=<PostgreSQL connection string>
```

Do not commit credentials or pass them as command-line arguments.

## Apply migrations explicitly

Restore repository-local tools, then run the dedicated one-shot migrator:

```text
dotnet tool restore
dotnet run --project src/MinimalBankSystem.Migrator
```

The migrator returns zero only after successful application. Connection, migration, command
timeout, and cancellation failures return a non-zero exit code. Migration commands and the
overall cancellation budget are fixed at 60 seconds.

## Check model drift

```text
dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project src/MinimalBankSystem.Infrastructure \
  --startup-project src/MinimalBankSystem.Migrator
```

The command must report no pending changes before a pull request is ready. Do not suppress the
pending-model warning or replace this check with a migration-name comparison.

## Generate review SQL

```text
dotnet tool run dotnet-ef migrations script --idempotent \
  --project src/MinimalBankSystem.Infrastructure \
  --startup-project src/MinimalBankSystem.Migrator \
  --output tmp/fnd04-migrations.sql
```

The generated SQL is review and release evidence, not the production deployment mechanism.
`tmp/fnd04-migrations.sql` is ignored local output and must not contain a connection string.

## Add a schema-owning migration

Only an Issue that owns the relevant schema may add a non-empty migration.

1. Confirm the schema-owning Issue, accepted ADRs, and previous migration upgrade requirements.
2. Change the `BankDbContext` model in the owning Issue.
3. Set `ConnectionStrings__Database` to an isolated PostgreSQL database.
4. Generate the migration with a descriptive name:

   ```text
   dotnet tool run dotnet-ef migrations add <MigrationName> \
     --project src/MinimalBankSystem.Infrastructure \
     --startup-project src/MinimalBankSystem.Migrator
   ```

5. Review `Up`, `Down`, and the snapshot; do not use `EnsureCreated` or startup DDL.
6. Verify empty-to-latest and previous-to-latest upgrades against real PostgreSQL. Starting with
   the first non-empty migration, preserve representative existing rows during the upgrade test.
7. Run the model drift check and generate the idempotent SQL evidence.

`InitialFoundation` is intentionally empty. It establishes migration history without pre-creating
Customer, Account, Operator, Identity, AuditLog, Transaction, Idempotency, or other business schema.
