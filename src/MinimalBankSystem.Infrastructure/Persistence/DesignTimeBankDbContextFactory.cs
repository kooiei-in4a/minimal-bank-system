using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MinimalBankSystem.Infrastructure.Persistence;

public sealed class DesignTimeBankDbContextFactory : IDesignTimeDbContextFactory<BankDbContext>
{
    public BankDbContext CreateDbContext(string[] args)
    {
        string connectionString = BankDbContextConnectionStringResolver.Resolve();

        DbContextOptions options = new DbContextOptionsBuilder()
            .UseBankPostgreSql(connectionString)
            .Options;

        return new BankDbContext(options);
    }
}
