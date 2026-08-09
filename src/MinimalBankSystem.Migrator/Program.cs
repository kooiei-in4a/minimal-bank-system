using MinimalBankSystem.Infrastructure.Persistence;

try
{
    string? connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "ConnectionStrings__Database must be set before running database migrations.");
    }

    using CancellationTokenSource cancellationSource = new(MigrationExecution.CancellationBudget);
    await using BankDbContext context = BankDbContextFactory.Create(connectionString);
    await MigrationExecution.MigrateAsync(context, cancellationSource.Token);

    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Database migration failed: {exception.Message}");
    return 1;
}
