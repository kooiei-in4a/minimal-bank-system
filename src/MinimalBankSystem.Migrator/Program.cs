using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Infrastructure.Persistence;

try
{
    string connectionString = BankDbContextConfiguration.GetRequiredEnvironmentConnectionString();
    DbContextOptions<BankDbContext> options =
        BankDbContextConfiguration.CreateOptions(connectionString);

    using CancellationTokenSource timeout = new(
        TimeSpan.FromSeconds(BankDbContextConfiguration.MigrationTimeoutSeconds));
    await using BankDbContext dbContext = new(options);

    await dbContext.Database.MigrateAsync(timeout.Token);
    Console.WriteLine("Database migrations applied successfully.");
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine(
        $"Database migration exceeded the " +
        $"{BankDbContextConfiguration.MigrationTimeoutSeconds}-second execution budget.");
    return 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Database migration failed ({exception.GetType().Name}).");
    return 1;
}
