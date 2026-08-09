using Microsoft.EntityFrameworkCore;

namespace MinimalBankSystem.Infrastructure.Persistence;

public static class BankDbContextConfiguration
{
    public const string ConnectionStringName = "Database";
    public const string ConfigurationKey = "ConnectionStrings:Database";
    public const string EnvironmentVariable = "ConnectionStrings__Database";
    public const int MigrationTimeoutSeconds = 60;

    public static DbContextOptionsBuilder UseBankPostgreSql(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.CommandTimeout(MigrationTimeoutSeconds);
            npgsqlOptions.MigrationsAssembly(typeof(BankDbContext).Assembly.GetName().Name);
        });

        return optionsBuilder;
    }

    public static DbContextOptions<BankDbContext> CreateOptions(string connectionString)
    {
        DbContextOptionsBuilder<BankDbContext> optionsBuilder = new();
        optionsBuilder.UseBankPostgreSql(connectionString);
        return optionsBuilder.Options;
    }

    public static string GetRequiredEnvironmentConnectionString()
    {
        string? connectionString = Environment.GetEnvironmentVariable(EnvironmentVariable);

        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException(
                $"Environment variable '{EnvironmentVariable}' is required.")
            : connectionString;
    }
}
