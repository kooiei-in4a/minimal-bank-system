using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MinimalBankSystem.Infrastructure.Persistence;

/// <summary>
/// Design-time context creation for the EF Core CLI. It lives in the migrations assembly and uses
/// the same provider configuration as the API host and the explicit migrator, so
/// <c>dotnet ef</c> never depends on API startup and never sees a different model or provider.
/// </summary>
public sealed class BankDbContextFactory : IDesignTimeDbContextFactory<BankDbContext>
{
    /// <summary>
    /// Builds a design-time <see cref="BankDbContext"/>.
    /// </summary>
    /// <param name="args">EF Core CLI arguments; unused because connection input comes from configuration.</param>
    /// <remarks>
    /// Connection input comes from <see cref="BankPersistence.ConnectionStringEnvironmentVariable"/>
    /// so no credential is embedded in the repository or passed on a command line. When it is not
    /// set, the PostgreSQL provider is still configured, only without a connection string; there is
    /// no SQLite, InMemory or otherwise faked provider fallback.
    /// </remarks>
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
