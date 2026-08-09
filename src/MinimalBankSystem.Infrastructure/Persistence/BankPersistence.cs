using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MinimalBankSystem.Infrastructure.Persistence;

public static class BankPersistence
{
    public const string ConnectionStringName = "Database";

    public const string ConnectionStringEnvironmentVariable = "ConnectionStrings__Database";

    public static readonly TimeSpan MigrationTimeout = TimeSpan.FromSeconds(60);

    public const int MigrationCommandTimeoutSeconds = 60;

    public static string MigrationsAssemblyName { get; } =
        typeof(BankDbContext).Assembly.GetName().Name
        ?? throw new InvalidOperationException("Infrastructure assembly name is unavailable.");

    public static IServiceCollection AddBankPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<BankDbContext>((_, options) =>
        {
            string connectionString = RequireConnectionString(configuration);
            ConfigureNpgsql(options, connectionString);
        });

        return services;
    }

    public static IServiceCollection AddBankPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                $"A non-empty '{ConnectionStringName}' connection string is required.",
                nameof(connectionString));
        }

        services.AddDbContext<BankDbContext>((_, options) =>
            ConfigureNpgsql(options, connectionString));

        return services;
    }

    public static void ConfigureNpgsql(
        DbContextOptionsBuilder options,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                $"A non-empty '{ConnectionStringName}' connection string is required.",
                nameof(connectionString));
        }

        options.UseNpgsql(
            connectionString,
            npgsql =>
            {
                npgsql.MigrationsAssembly(MigrationsAssemblyName);
                npgsql.CommandTimeout(MigrationCommandTimeoutSeconds);
            });
    }

    public static string RequireConnectionString(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        throw new InvalidOperationException(
            $"Required configuration 'ConnectionStrings:{ConnectionStringName}' is missing. " +
            $"Set '{ConnectionStringEnvironmentVariable}' or the corresponding configuration value. " +
            "SQLite, InMemory, and other non-PostgreSQL fallbacks are not permitted.");
    }
}
