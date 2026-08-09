using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.Migrator;

public static class MigratorApplication
{
    public static async Task<int> RunAsync(
        IConfiguration configuration,
        TextWriter errorWriter,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string connectionString =
                BankDbContextConfiguration.GetRequiredConnectionString(configuration);
            DbContextOptionsBuilder<BankDbContext> options = new();
            BankDbContextConfiguration.Configure(options, connectionString);

            await using BankDbContext dbContext = new(options.Options);
            using CancellationTokenSource timeout =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(
                TimeSpan.FromSeconds(BankDbContextConfiguration.MigrationCommandTimeoutSeconds));

            await dbContext.Database.MigrateAsync(timeout.Token);
            return 0;
        }
        catch (Exception exception)
        {
            await errorWriter.WriteLineAsync(
                $"Database migration failed: {exception.Message}");
            return 1;
        }
    }
}
