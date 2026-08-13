# Database migration policy and procedure

Schema evolution is owned by EF Core migrations and applied by this one-shot migrator
(ADR-0009, Issue #42). Normal API startup never changes the schema.

## Ownership

| Concern | Owner |
| :--- | :--- |
| `BankDbContext`, provider configuration, migrations, snapshot, design-time factory | `MinimalBankSystem.Infrastructure` |
| Applying migrations | `MinimalBankSystem.Migrator` |
| Business tables, constraints, sequences and triggers | Later schema-owning Issues |
| Compose ordering | FND-05 |

The migrator references Infrastructure and never starts the API host.

## Connection configuration

The canonical key is `ConnectionStrings:Database`; its environment form is:

```text
ConnectionStrings__Database
```

No credential-bearing connection string is committed or passed as a command-line argument.
Missing configuration fails closed; no localhost, ambient, SQLite or InMemory fallback exists.

## Applying migrations

```bash
ConnectionStrings__Database='Host=...;Port=5432;Database=...;Username=...;Password=...' dotnet run --project src/MinimalBankSystem.Migrator
```

| Exit code | Meaning |
| :--- | :--- |
| `0` | All pending migrations were applied |
| `1` | Configuration, connection, authentication or migration failure |
| `2` | The fixed 60-second budget elapsed |

The database command timeout and whole migration budget are both 60 seconds. Failure is never
converted to success, and pending-model warnings are not suppressed.

## Model drift check

```bash
dotnet tool restore
dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/MinimalBankSystem.Infrastructure --startup-project src/MinimalBankSystem.Migrator
```

Exit `0` means the model matches the committed snapshot. Model-only commands can create an Npgsql
context without a destination; connection-required design-time commands still require
`ConnectionStrings__Database` and fail closed when it is absent.

## Idempotent SQL evidence

```bash
dotnet tool run dotnet-ef migrations script --idempotent --project src/MinimalBankSystem.Infrastructure --startup-project src/MinimalBankSystem.Migrator
```

The generated SQL is review and release evidence, not the deployment mechanism.

## Adding a migration in a schema-owning Issue

1. Add the owned entity/configuration in Infrastructure.
2. Generate a named migration:

   ```bash
   dotnet tool run dotnet-ef migrations add <Name> --project src/MinimalBankSystem.Infrastructure --startup-project src/MinimalBankSystem.Migrator
   ```

3. Review generated `Up` and `Down`; destructive changes require an explicit data-preservation and
   restore plan.
4. Run the pending-model check and inspect the idempotent SQL.
5. On real PostgreSQL, verify clean-to-latest and previous-to-latest with representative rows. The
   latter starts from `InitialFoundation` (empty machinery) and then applies the first non-empty
   schema-owning migration, currently `AddOperatorIdentity`.
6. Commit the migration, designer and updated snapshot together.
7. Never substitute `EnsureCreated`, startup DDL, SQLite or InMemory.

## Empty foundation

`InitialFoundation` has zero `Up` and `Down` operations. Applying it creates only
`public.__EFMigrationsHistory`. `AddOperatorIdentity` is the first non-empty schema-owning
migration and creates `public.operators`.
