using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.Migrator;

/// <summary>
/// WP2-DB-01: re-asserts the API runtime's read-only migration-history boundary after every
/// Migrator run. Bootstrap provisioning grants the API runtime role broad DML on every table the
/// Migrator subsequently creates (including the EF migration-history table) so that ordinary
/// future application tables receive runtime DML automatically; this step narrows that grant back
/// down to SELECT-only on the migration-history table specifically, so the API runtime can read
/// migration history for readiness purposes but can never mutate it. It is idempotent and runs on
/// every Migrator invocation, not only the first.
/// </summary>
internal static class MigratorPrivilegeBoundary
{
    /// <summary>
    /// Names the API runtime role this Migrator run must narrow to read-only on the migration
    /// history table. Set by the shipped Compose deployment (WP2-DB-01 privilege-boundary
    /// contract); left unset for ad hoc PostgreSQL targets, such as the FND-04/FND-06 integration
    /// test fixtures, that provision no such role and request no boundary enforcement.
    /// </summary>
    public const string ApiRuntimeRoleEnvironmentVariable = "MBS_API_RUNTIME_ROLE";

    /// <summary>Enforces the boundary when requested and returns the enforced role name, or
    /// <see langword="null"/> when no boundary was requested for this Migrator target.</summary>
    public static async Task<string?> EnforceReadOnlyMigrationHistoryAsync(
        BankDbContext context,
        CancellationToken cancellationToken)
    {
        string? apiRuntimeRoleName = Environment.GetEnvironmentVariable(ApiRuntimeRoleEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(apiRuntimeRoleName))
        {
            return null;
        }

        await EnforceAsync(context, apiRuntimeRoleName, cancellationToken).ConfigureAwait(false);
        return apiRuntimeRoleName;
    }

    private static async Task EnforceAsync(
        BankDbContext context,
        string apiRuntimeRoleName,
        CancellationToken cancellationToken)
    {
        string historyTable =
            $"{QuoteIdentifier(BankPersistence.MigrationsHistorySchema)}.{QuoteIdentifier(BankPersistence.MigrationsHistoryTableName)}";
        string apiRole = QuoteIdentifier(apiRuntimeRoleName);

        // GRANT/REVOKE cannot parameterize identifiers, so this necessarily builds raw SQL text.
        // Both interpolated fragments are quoted identifiers taken from this assembly's own
        // constants and configured deployment identity (never external or user-supplied input),
        // so there is no injectable value.
        string revokeMutation = "REVOKE INSERT, UPDATE, DELETE ON TABLE " + historyTable + " FROM " + apiRole + ";";
        string grantReadOnly = "GRANT SELECT ON TABLE " + historyTable + " TO " + apiRole + ";";

        await context.Database.ExecuteSqlRawAsync(revokeMutation, cancellationToken).ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync(grantReadOnly, cancellationToken).ConfigureAwait(false);
    }

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
