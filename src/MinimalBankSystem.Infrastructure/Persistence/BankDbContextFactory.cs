using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MinimalBankSystem.Infrastructure.Persistence;

/// <summary>
/// Creates the production Infrastructure context for EF Core design-time commands without
/// starting the API host.
/// </summary>
public sealed class BankDbContextFactory : IDesignTimeDbContextFactory<BankDbContext>
{
    /// <inheritdoc />
    public BankDbContext CreateDbContext(string[] args)
    {
        string? connectionString = Environment.GetEnvironmentVariable(
            BankPersistence.ConnectionStringEnvironmentVariable);

        DbContextOptionsBuilder<BankDbContext> options = new();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            options.UseBankPostgreSqlModelOnly();
        }
        else
        {
            options.UseBankPostgreSql(connectionString, BankPersistence.MigrationTimeoutSeconds);
        }

        return new BankDbContext(options.Options);
    }
}
