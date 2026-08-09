using Microsoft.EntityFrameworkCore;

namespace MinimalBankSystem.Infrastructure.Persistence;

public static class MigrationExecutor
{
    public static async Task ApplyAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        DbContextOptionsBuilder<BankDbContext> options = new();
        BankDbContextOptions.Configure(options, connectionString, useMigrationTimeout: true);

        await using BankDbContext context = new(options.Options);
        await context.Database.MigrateAsync(cancellationToken);
    }
}
