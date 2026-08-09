using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.Migrator;

public static class MigrationCommand
{
    public static Task<int> RunAsync() => RunAsync(
        DatabaseConnectionStrings.FromEnvironment(),
        TimeSpan.FromSeconds(BankDbContextOptions.MigrationTimeoutSeconds));

    public static async Task<int> RunAsync(
        string? connectionString,
        TimeSpan timeout,
        Func<string, CancellationToken, Task>? applyAsync = null)
    {
        try
        {
            string requiredConnectionString = DatabaseConnectionStrings.Require(connectionString);
            using CancellationTokenSource cancellation = new(timeout);
            Func<string, CancellationToken, Task> apply =
                applyAsync ?? MigrationExecutor.ApplyAsync;

            await apply(requiredConnectionString, cancellation.Token);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Database migration failed: {exception.Message}");
            return 1;
        }
    }
}
