# Database migration operation

## Scope and ownership

FND-04 owns only the EF Core migration machinery and the empty `InitialFoundation` baseline.
It does not own a business table, sequence, trigger, or constraint. The first non-empty
migration belongs to the schema-owning Issue that introduces those database objects.

The canonical application configuration key is `ConnectionStrings:Database`. For environment
variables, use the .NET configuration form `ConnectionStrings__Database`. Do not commit a
credential-bearing connection string or pass one as a command-line argument.

## Prerequisites

Set the connection string only in the process environment, then restore the repository-local
tool manifest:

```powershell
$env:ConnectionStrings__Database = '<PostgreSQL connection string>'
dotnet tool restore
```

## Standard commands

Apply migrations through the dedicated one-shot Migrator. The API does not apply migrations at
startup.

```powershell
dotnet run --project src/MinimalBankSystem.Migrator
```

The Migrator uses the Infrastructure migrations assembly and exits nonzero when configuration,
connection, migration, command-timeout, or cancellation fails. Both the database command timeout
and overall cancellation budget are fixed at 60 seconds.

Check the actual EF Core model/snapshot relationship before review or release evidence:

```powershell
dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/MinimalBankSystem.Infrastructure --startup-project src/MinimalBankSystem.Migrator
```

Generate idempotent SQL only as review/release evidence; it is not the production deployment
mechanism by itself:

```powershell
dotnet tool run dotnet-ef migrations script --idempotent --project src/MinimalBankSystem.Infrastructure --startup-project src/MinimalBankSystem.Migrator
```

## Procedure for a schema-owning Issue

1. Make only the model changes owned by that Issue in `MinimalBankSystem.Infrastructure`.
2. Set `ConnectionStrings__Database` for the target verification database.
3. Generate a source-controlled migration with the repository-local tool:

   ```powershell
   dotnet tool run dotnet-ef migrations add <DescriptiveMigrationName> --project src/MinimalBankSystem.Infrastructure --startup-project src/MinimalBankSystem.Migrator
   ```

4. Review the generated `Up`, `Down`, and model snapshot. The migration must own every added
   business DDL object and must not absorb unrelated schema work.
5. Verify clean database apply, migration history in `public.__EFMigrationsHistory`, and the
   immediate-previous-schema upgrade with representative rows when a prior business migration
   exists.
6. Run the pending-model command and generate the idempotent SQL evidence shown above.
7. Do not use `EnsureCreated`, ad-hoc startup DDL, or normal API startup to evolve the schema.
