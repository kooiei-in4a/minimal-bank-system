using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Infrastructure;

namespace MinimalBankSystem.Migrator;

public static class MigrationExecutor
{
    public const int ExitSuccess = 0;
    public const int ExitFailure = 1;

    public static readonly TimeSpan ExecutionBudget = TimeSpan.FromSeconds(60);

    public static async Task<int> ExecuteAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DbContextOptions<BankDbContext> options =
                BankDbContextOptions.Create(connectionString, ExecutionBudget);
            await using BankDbContext context = new(options);
            await context.Database.MigrateAsync(cancellationToken);
            Console.WriteLine("Database migrated successfully to the latest migration.");
            return ExitSuccess;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Console.Error.WriteLine(
                "Database migration failed: execution was canceled or exceeded its 60-second budget.");
            return ExitFailure;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Database migration failed: {exception.Message}");
            return ExitFailure;
        }
    }
}
