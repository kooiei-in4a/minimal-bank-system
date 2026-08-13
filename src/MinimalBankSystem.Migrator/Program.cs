using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Migrator;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
    options.UseUtcTimestamp = true;
});

builder.Services.AddDbContext<BankDbContext>((serviceProvider, options) =>
{
    IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
    string? connectionString = configuration.GetConnectionString(BankPersistence.ConnectionStringName);

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            $"No PostgreSQL connection string was configured. Set '{BankPersistence.ConnectionStringEnvironmentVariable}' " +
            $"(configuration key 'ConnectionStrings:{BankPersistence.ConnectionStringName}') before running the migrator. " +
            "The migrator never falls back to another provider or to an ambient default.");
    }

    options.UseBankPostgreSql(connectionString, BankPersistence.MigrationTimeoutSeconds);
});

using IHost host = builder.Build();
ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(MigratorLog.Category);

using CancellationTokenSource cancellation = new(BankPersistence.MigrationTimeout);

try
{
    await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
    BankDbContext context = scope.ServiceProvider.GetRequiredService<BankDbContext>();

    string[] pendingMigrations =
    [
        .. await context.Database.GetPendingMigrationsAsync(cancellation.Token).ConfigureAwait(false),
    ];
    string pendingDescription = Describe(pendingMigrations);

    MigratorLog.ApplyingMigrations(
        logger,
        pendingMigrations.Length,
        BankPersistence.MigrationTimeoutSeconds,
        pendingDescription);

    await context.Database.MigrateAsync(cancellation.Token).ConfigureAwait(false);

    // The Migrator owns the history table. API readiness may read it, but the runtime role must
    // never be able to mutate migration state or use the Migrator's DDL capability.
    await context.Database.ExecuteSqlRawAsync(
        """
        DO $$
        BEGIN
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'mbs_runtime') THEN
                EXECUTE 'GRANT SELECT ON TABLE public."__EFMigrationsHistory" TO mbs_runtime';
                EXECUTE 'REVOKE INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER
                    ON TABLE public."__EFMigrationsHistory" FROM mbs_runtime';
            END IF;
        END
        $$;
        """,
        cancellation.Token).ConfigureAwait(false);

    string[] appliedMigrations =
    [
        .. await context.Database.GetAppliedMigrationsAsync(cancellation.Token).ConfigureAwait(false),
    ];
    string appliedDescription = Describe(appliedMigrations);

    MigratorLog.MigrationCompleted(logger, appliedDescription);

    return MigratorExitCode.Success;
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    MigratorLog.MigrationTimedOut(logger, BankPersistence.MigrationTimeoutSeconds);
    return MigratorExitCode.Timeout;
}
catch (Exception exception)
{
    MigratorLog.MigrationFailed(logger, exception);
    return MigratorExitCode.Failure;
}

static string Describe(string[] migrations) =>
    migrations.Length == 0 ? "(none)" : string.Join(", ", migrations);
