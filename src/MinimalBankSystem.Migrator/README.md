# Database migration policy and procedure

Schema evolution is owned by EF Core migrations and applied by this one-shot migrator
(ADR-0009, Issue #42). Normal API startup never changes the schema.

## Ownership

| Concern | Owner |
| :--- | :--- |
| `BankDbContext`, provider configuration, migrations, model snapshot, design-time factory | `MinimalBankSystem.Infrastructure` |
| Migrations assembly | `MinimalBankSystem.Infrastructure` |
| Applying migrations | `MinimalBankSystem.Migrator` (this project) |
| Business tables, constraints, sequences, triggers | later schema-owning Issues |
| Running the migrator before the API in Compose | FND-05 |

The migrator references Infrastructure and never starts the API host. FND-05 connects this
executable to Compose ordering instead of reimplementing migration machinery.

## Connection configuration

The canonical configuration key is `ConnectionStrings:Database`. In an environment variable this is:

```text
ConnectionStrings__Database
```

No credential-bearing connection string is committed, and no password is passed as a command-line
argument. When the key is missing the migrator fails; it never falls back to another provider,
to SQLite or to InMemory.

## Applying migrations

```bash
ConnectionStrings__Database='Host=...;Port=5432;Database=...;Username=...;Password=...' dotnet run --project src/MinimalBankSystem.Migrator
```

The migrator applies every pending migration up to the latest one and then exits.

| Exit code | Meaning |
| :--- | :--- |
| `0` | Every pending migration was applied |
| `1` | Configuration, connection, authentication or migration failure |
| `2` | The fixed 60-second budget elapsed before the migration completed |

Migration execution is bounded by a fixed 60-second command timeout and cancellation budget, so a
blocked migration fails the deployment step instead of hanging it. Failures are never converted to
success, and the EF Core pending-model-changes warning is not suppressed.

## Model drift check

```bash
dotnet tool restore
dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/MinimalBankSystem.Infrastructure --startup-project src/MinimalBankSystem.Migrator
```

Exit code `0` means the committed model snapshot matches the current model; a non-zero exit means a
migration is missing. `MigrationModelTests.TheModelHasNoPendingChangeAgainstTheCommittedSnapshot`
runs the same EF mechanism programmatically.

These design-time commands read the model only and do not open a connection, so they run without
`ConnectionStrings__Database`. Design-time operations that do reach the database require it.

## Idempotent SQL for review and release evidence

```bash
dotnet tool run dotnet-ef migrations script --idempotent --project src/MinimalBankSystem.Infrastructure --startup-project src/MinimalBankSystem.Migrator
```

The generated script guards each migration with its history-table entry. It is review and release
evidence, not the deployment mechanism itself; deployments run the migrator.

## Adding a migration in a schema-owning Issue

1. Add or change entity types and their configuration in `MinimalBankSystem.Infrastructure`.
2. Create the migration, named for the change it makes:

   ```bash
   dotnet tool run dotnet-ef migrations add <Name> --project src/MinimalBankSystem.Infrastructure --startup-project src/MinimalBankSystem.Migrator
   ```

3. Read the generated `Up` and `Down` before committing. Generated code is reviewed source, not a
   build artifact. Keep files on the repository style (file-scoped namespace, LF, no BOM).
4. Provide a meaningful `Down` when reversal preserves the prior contract. A destructive or
   data-rewriting migration needs an explicit data-preservation and restore plan; a superficial
   `Down` is not accepted (ADR-0009).
5. Verify the drift check reports no pending model changes.
6. Verify on a real PostgreSQL database, not SQLite or InMemory:
   - clean database to latest;
   - the immediately previous migration to latest with representative existing rows. This upgrade
     test becomes mandatory with the first non-empty migration, because `InitialFoundation` has no
     previous state and no business rows.
7. Commit the migration, its designer file and the updated `BankDbContextModelSnapshot`.
8. Never add schema through `EnsureCreated`, ad-hoc startup DDL or a manual SQL script.

## Why `InitialFoundation` is empty

FND-04 owns migration machinery, not business schema. `InitialFoundation` therefore declares zero
migration operations. Applying it still proves the machinery end to end: EF Core creates
`public.__EFMigrationsHistory` and records the migration id. The first non-empty migration belongs
to the Issue that introduces the entities.

## Verification

- Real PostgreSQL evidence: `tests/MinimalBankSystem.IntegrationTests/PostgreSql/MigrationBaselineTests.cs`
- Model and generated-SQL evidence: `tests/MinimalBankSystem.IntegrationTests/Persistence/MigrationModelTests.cs`
