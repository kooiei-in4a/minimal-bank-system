using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MinimalBankSystem.Infrastructure.Persistence;

public static class BankDbContextConfiguration
{
    public const string ConnectionStringName = "Database";
    public const int MigrationCommandTimeoutSeconds = 60;

    public static IServiceCollection AddBankDbContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(ConnectionStringName);

        services.AddDbContext<BankDbContext>(options =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "The canonical connection string 'ConnectionStrings:Database' is required.");
            }

            Configure(options, connectionString);
        });

        return services;
    }

    public static void Configure(
        DbContextOptionsBuilder options,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        options.UseNpgsql(
            connectionString,
            npgsqlOptions => npgsqlOptions
                .MigrationsAssembly(typeof(BankDbContext).Assembly.GetName().Name)
                .CommandTimeout(MigrationCommandTimeoutSeconds));
    }

    public static string GetRequiredConnectionString(IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(ConnectionStringName);

        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException(
                "The canonical connection string 'ConnectionStrings:Database' is required.")
            : connectionString;
    }
}
