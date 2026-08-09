using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MinimalBankSystem.Infrastructure.Persistence;

public sealed class BankDbContextDesignTimeFactory : IDesignTimeDbContextFactory<BankDbContext>
{
    public BankDbContext CreateDbContext(string[] args)
    {
        _ = args;

        string? connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The design-time environment variable 'ConnectionStrings__Database' is required.");
        }

        DbContextOptionsBuilder<BankDbContext> options = new();
        BankDbContextConfiguration.Configure(options, connectionString);
        return new BankDbContext(options.Options);
    }
}
