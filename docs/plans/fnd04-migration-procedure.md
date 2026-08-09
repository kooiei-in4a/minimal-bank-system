# FND-04 Migration Procedure

FND-04 owns only the EF Core migration machinery and the empty `InitialFoundation`
baseline. A schema-owning Issue owns every non-empty migration it introduces.

## Preconditions

- The schema-owning Issue is implementation-ready and identifies its owned tables,
  constraints, indexes, and rollback behavior.
- `ConnectionStrings__Database` is supplied through the local environment. Do not
  commit credentials or pass passwords as command-line arguments.
- The PostgreSQL database is reachable and the application is stopped or otherwise
  protected from incompatible schema changes.

## Add A Migration

From the repository root, set the design-time connection environment variable and
run the repository-local EF tool:

```bash
export ConnectionStrings__Database='Host=localhost;Database=minimal_bank_system;Username=postgres'
dotnet tool restore
dotnet tool run dotnet-ef migrations add <MigrationName> \
  --project src/MinimalBankSystem.Infrastructure \
  --startup-project src/MinimalBankSystem.Migrator
```

Review the generated migration and snapshot as source-controlled code. The
migration must contain only the schema owned by that Issue and must have a safe
`Down` path, or document why rollback requires backup restore.

## Verify A Migration

```bash
dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project src/MinimalBankSystem.Infrastructure \
  --startup-project src/MinimalBankSystem.Migrator
dotnet tool run dotnet-ef migrations script --idempotent \
  --project src/MinimalBankSystem.Infrastructure \
  --startup-project src/MinimalBankSystem.Migrator
dotnet run --project src/MinimalBankSystem.Migrator
```

The migrator is the only standard apply path. Normal API startup must not call
`Migrate`, `MigrateAsync`, `EnsureCreated`, or execute schema DDL.

## Review Evidence

Record the exact migration name, clean-database apply result, migration history,
pending-model check, idempotent SQL generation, and rollback or backup-restore
evidence in the schema-owning Issue and its PR.
