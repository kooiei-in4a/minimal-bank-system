using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.Migrator;

/// <summary>
/// Explicit one-shot migrator entry point. Does not start the API host and does not
/// convert connection, migration, timeout, or pending-model failures into success.
/// </summary>
public static class MigrationEntryPoint
{
    public static Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        return RunAsync(configuration, cancellationToken);
    }

    public static async Task<int> RunAsync(
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string connectionString;
        try
        {
            connectionString = BankPersistence.RequireConnectionString(configuration);
        }
        catch (InvalidOperationException exception)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            return 1;
        }

        using CancellationTokenSource timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(BankPersistence.MigrationTimeout);

        try
        {
            DbContextOptionsBuilder<BankDbContext> optionsBuilder = new();
            BankPersistence.ConfigureNpgsql(optionsBuilder, connectionString);

            await using BankDbContext dbContext = new(optionsBuilder.Options);

            if (dbContext.Database.HasPendingModelChanges())
            {
                await Console.Error.WriteLineAsync(
                    "Pending EF Core model changes were detected. Refusing to apply migrations.");
                return 1;
            }

            await dbContext.Database.MigrateAsync(timeoutSource.Token);
            return 0;
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            await Console.Error.WriteLineAsync(
                $"Migration failed: timed out or canceled within the {BankPersistence.MigrationTimeout.TotalSeconds:0}-second budget.");
            return 1;
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync(
                $"Migration failed: {exception.GetType().Name}: {exception.Message}");
            return 1;
        }
    }
}
