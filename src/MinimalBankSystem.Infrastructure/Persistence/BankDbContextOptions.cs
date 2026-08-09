using Microsoft.EntityFrameworkCore;

namespace MinimalBankSystem.Infrastructure.Persistence;

public static class BankDbContextOptions
{
    public const int MigrationTimeoutSeconds = 60;

    public static void Configure(
        DbContextOptionsBuilder options,
        string connectionString,
        bool useMigrationTimeout = false)
    {
        string requiredConnectionString = DatabaseConnectionStrings.Require(connectionString);

        options.UseNpgsql(
            requiredConnectionString,
            npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(BankDbContext).Assembly.GetName().Name);

                if (useMigrationTimeout)
                {
                    npgsqlOptions.CommandTimeout(MigrationTimeoutSeconds);
                }
            });
    }
}
