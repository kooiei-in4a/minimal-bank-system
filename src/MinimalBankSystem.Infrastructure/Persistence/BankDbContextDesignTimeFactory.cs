using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MinimalBankSystem.Infrastructure.Persistence;

public sealed class BankDbContextDesignTimeFactory : IDesignTimeDbContextFactory<BankDbContext>
{
    public BankDbContext CreateDbContext(string[] args)
    {
        _ = args;

        string connectionString = DatabaseConnectionStrings.Require(
            DatabaseConnectionStrings.FromEnvironment());
        DbContextOptionsBuilder<BankDbContext> options = new();
        BankDbContextOptions.Configure(options, connectionString, useMigrationTimeout: true);
        return new BankDbContext(options.Options);
    }
}
