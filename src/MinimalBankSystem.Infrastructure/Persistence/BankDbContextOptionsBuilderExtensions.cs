using Microsoft.EntityFrameworkCore;

namespace MinimalBankSystem.Infrastructure.Persistence;

public static class BankDbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseBankPostgreSql(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return optionsBuilder.UseNpgsql(
            connectionString,
            npgsqlOptions => npgsqlOptions.MigrationsAssembly(typeof(BankDbContext).Assembly.FullName));
    }
}
