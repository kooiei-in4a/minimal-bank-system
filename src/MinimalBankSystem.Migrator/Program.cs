using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Migrator;

// One-shot explicit migrator (ADR-0009, Issue #42 section 8.6).
//
// It builds a generic host only. It never starts the API host, and the API never starts it.
// Exit code 0 means every pending migration was applied; any connection, migration or timeout
// failure exits non-zero so a deployment step cannot mistake failure for success.

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

ILogger logger = host.Services
    .GetRequiredService<ILoggerFactory>()
    .CreateLogger(MigratorLog.Category);

// The whole migration, including connecting, is bounded. An unbounded migration would hang a
// deployment step instead of failing it.
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
