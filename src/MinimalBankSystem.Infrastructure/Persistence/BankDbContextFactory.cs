using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MinimalBankSystem.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so EF Core CLI can create <see cref="BankDbContext"/> without
/// starting the API host. Uses the same provider and migrations assembly as runtime.
/// </summary>
public sealed class BankDbContextFactory : IDesignTimeDbContextFactory<BankDbContext>
{
    public BankDbContext CreateDbContext(string[] args)
    {
        string? connectionString =
            Environment.GetEnvironmentVariable(BankPersistence.ConnectionStringEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Design-time EF Core operations require '{BankPersistence.ConnectionStringEnvironmentVariable}'. " +
                "SQLite, InMemory, and fake-provider fallbacks are not permitted.");
        }

        DbContextOptionsBuilder<BankDbContext> optionsBuilder = new();
        BankPersistence.ConfigureNpgsql(optionsBuilder, connectionString);
        return new BankDbContext(optionsBuilder.Options);
    }
}
