namespace MinimalBankSystem.Migrator;

public static class MigrationRunner
{
    public const int TimeoutSeconds = 60;

    public static async Task<int> RunAsync(
        Func<CancellationToken, Task> migrateAsync,
        TextWriter output,
        TextWriter error)
    {
        using CancellationTokenSource timeoutSource = new(TimeSpan.FromSeconds(TimeoutSeconds));

        try
        {
            await migrateAsync(timeoutSource.Token);
            await output.WriteLineAsync("Migration completed successfully.");
            return 0;
        }
        catch (OperationCanceledException exception)
        {
            await error.WriteLineAsync(
                $"Migration did not complete within the {TimeoutSeconds}s bounded execution budget.");
            await error.WriteLineAsync(exception.ToString());
            return 1;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync("Migration failed.");
            await error.WriteLineAsync(exception.ToString());
            return 1;
        }
    }
}
