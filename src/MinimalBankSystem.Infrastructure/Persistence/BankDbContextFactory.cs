using Microsoft.EntityFrameworkCore.Design;

namespace MinimalBankSystem.Infrastructure.Persistence;

public sealed class BankDbContextFactory : IDesignTimeDbContextFactory<BankDbContext>
{
    public BankDbContext CreateDbContext(string[] args)
    {
        _ = args;
        string connectionString = BankDbContextConfiguration.GetRequiredEnvironmentConnectionString();
        return new BankDbContext(BankDbContextConfiguration.CreateOptions(connectionString));
    }
}
