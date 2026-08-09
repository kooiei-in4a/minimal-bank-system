using MinimalBankSystem.Migrator;

string? connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "Database migration failed: the 'ConnectionStrings__Database' environment variable " +
        "(ConnectionStrings:Database) is not configured. No fallback provider is used.");
    return MigrationExecutor.ExitFailure;
}

using CancellationTokenSource budget = new();
budget.CancelAfter(MigrationExecutor.ExecutionBudget);

return await MigrationExecutor.ExecuteAsync(connectionString, budget.Token);

public static partial class Program
{
}
