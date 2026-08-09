using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace MinimalBankSystem.Infrastructure.Persistence;

/// <summary>
/// Single PostgreSQL provider configuration shared by the API host, the explicit migrator and the
/// design-time context factory, so runtime and design time cannot drift apart.
/// </summary>
public static class BankPersistence
{
    /// <summary>Canonical application connection string name (<c>ConnectionStrings:Database</c>).</summary>
    public const string ConnectionStringName = "Database";

    /// <summary>Environment variable form of <see cref="ConnectionStringName"/>.</summary>
    public const string ConnectionStringEnvironmentVariable = "ConnectionStrings__Database";

    /// <summary>PostgreSQL default EF Core migration history table.</summary>
    public const string MigrationsHistoryTableName = "__EFMigrationsHistory";

    /// <summary>Schema holding <see cref="MigrationsHistoryTableName"/>.</summary>
    public const string MigrationsHistorySchema = "public";

    /// <summary>Bounded migration command timeout and cancellation budget fixed by Issue #42.</summary>
    public const int MigrationTimeoutSeconds = 60;

    /// <summary>Bounded migration budget as a <see cref="TimeSpan"/>.</summary>
    public static TimeSpan MigrationTimeout => TimeSpan.FromSeconds(MigrationTimeoutSeconds);

    /// <summary>
    /// Configures the Npgsql provider against a real PostgreSQL connection string.
    /// </summary>
    /// <param name="builder">Options builder to configure.</param>
    /// <param name="connectionString">A PostgreSQL connection string; never a fake or file-backed provider.</param>
    /// <param name="commandTimeoutSeconds">
    /// Optional bounded command timeout. The migrator passes <see cref="MigrationTimeoutSeconds"/>;
    /// the API leaves the provider default in place.
    /// </param>
    public static DbContextOptionsBuilder UseBankPostgreSql(
        this DbContextOptionsBuilder builder,
        string connectionString,
        int? commandTimeoutSeconds = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return builder.UseNpgsql(
            connectionString,
            npgsql => ConfigureProvider(npgsql, commandTimeoutSeconds));
    }

    /// <summary>
    /// Configures the same Npgsql provider without a connection string, for design-time operations
    /// that only read the model (<c>migrations add</c>, <c>migrations script</c>,
    /// <c>migrations has-pending-model-changes</c>). This is not a fallback provider: PostgreSQL
    /// remains the only provider, and any operation that actually reaches the database still
    /// requires <see cref="ConnectionStringEnvironmentVariable"/>.
    /// </summary>
    /// <param name="builder">Options builder to configure.</param>
    public static DbContextOptionsBuilder UseBankPostgreSqlModelOnly(this DbContextOptionsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.UseNpgsql(npgsql => ConfigureProvider(npgsql, commandTimeoutSeconds: null));
    }

    private static void ConfigureProvider(
        NpgsqlDbContextOptionsBuilder npgsql,
        int? commandTimeoutSeconds)
    {
        npgsql.MigrationsAssembly(typeof(BankDbContext).Assembly);
        npgsql.MigrationsHistoryTable(MigrationsHistoryTableName, MigrationsHistorySchema);

        if (commandTimeoutSeconds is int seconds)
        {
            npgsql.CommandTimeout(seconds);
        }
    }
}
