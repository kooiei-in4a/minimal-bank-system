using Microsoft.EntityFrameworkCore.Design;

namespace MinimalBankSystem.Infrastructure;

public sealed class DesignTimeBankDbContextFactory : IDesignTimeDbContextFactory<BankDbContext>
{
    public BankDbContext CreateDbContext(string[] args)
    {
        string? connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // EF Core CLI operations that do not require a live database
            // (migrations add, has-pending-model-changes, migrations script)
            // still create a DbContext through this factory. A local
            // credential-free Npgsql connection string keeps the runtime
            // provider identical without substituting SQLite/InMemory and
            // without embedding a secret. Operations that actually connect
            // must be run with ConnectionStrings__Database set.
            connectionString = "Host=127.0.0.1;Port=5432;Database=design_time;Pooling=false;Timeout=5";
        }

        return new BankDbContext(BankDbContextOptions.Create(connectionString));
    }
}
