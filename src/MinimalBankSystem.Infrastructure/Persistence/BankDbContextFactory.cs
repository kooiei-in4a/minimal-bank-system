using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace MinimalBankSystem.Infrastructure.Persistence;

public static class BankDbContextFactory
{
    public static BankDbContext Create(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        DbContextOptionsBuilder<BankDbContext> optionsBuilder = new();
        Configure(optionsBuilder, connectionString);

        return new BankDbContext(optionsBuilder.Options);
    }

    public static IServiceCollection AddBankPersistence(
        this IServiceCollection services,
        Func<string?> connectionStringProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connectionStringProvider);

        services.AddDbContext<BankDbContext>((_, optionsBuilder) =>
        {
            string connectionString = connectionStringProvider()
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:Database must be configured before BankDbContext is used.");

            Configure(optionsBuilder, connectionString);
        });

        return services;
    }

    private static void Configure(
        DbContextOptionsBuilder optionsBuilder,
        string connectionString)
    {
        optionsBuilder.UseNpgsql(
            connectionString,
            npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(BankDbContext).Assembly.GetName().Name);
                npgsqlOptions.CommandTimeout(MigrationExecution.CommandTimeoutSeconds);
            });
    }
}

public sealed class BankDbContextDesignTimeFactory : IDesignTimeDbContextFactory<BankDbContext>
{
    public BankDbContext CreateDbContext(string[] args)
    {
        _ = args;

        string? connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__Database must be set for EF Core design-time commands.");
        }

        return BankDbContextFactory.Create(connectionString);
    }
}
