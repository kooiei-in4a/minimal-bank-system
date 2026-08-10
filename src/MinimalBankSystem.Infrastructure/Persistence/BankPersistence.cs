using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace MinimalBankSystem.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL provider configuration shared by the API, explicit migrator and design-time factory.
/// </summary>
public static class BankPersistence
{
    /// <summary>Canonical application connection string name.</summary>
    public const string ConnectionStringName = "Database";

    /// <summary>Environment-variable form of <c>ConnectionStrings:Database</c>.</summary>
    public const string ConnectionStringEnvironmentVariable = "ConnectionStrings__Database";

    /// <summary>PostgreSQL default EF Core migration history table.</summary>
    public const string MigrationsHistoryTableName = "__EFMigrationsHistory";

    /// <summary>Schema holding the migration history table.</summary>
    public const string MigrationsHistorySchema = "public";

    /// <summary>Bounded command timeout and whole-operation migration budget.</summary>
    public const int MigrationTimeoutSeconds = 60;

    /// <summary>The bounded whole-operation migration budget.</summary>
    public static TimeSpan MigrationTimeout => TimeSpan.FromSeconds(MigrationTimeoutSeconds);

    /// <summary>Configures the production Npgsql provider with an explicit destination.</summary>
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
    /// Configures Npgsql without fabricating a destination for model-only design-time commands.
    /// Connection-required commands fail closed until an explicit connection string is supplied.
    /// </summary>
    public static DbContextOptionsBuilder UseBankPostgreSqlModelOnly(
        this DbContextOptionsBuilder builder)
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
