using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MinimalBankSystem.Infrastructure.Persistence;

public static class BankPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddBankDbContext(
        this IServiceCollection services,
        Func<string?> connectionStringProvider)
    {
        ArgumentNullException.ThrowIfNull(connectionStringProvider);

        services.AddDbContext<BankDbContext>((_, options) =>
        {
            string connectionString = DatabaseConnectionStrings.Require(connectionStringProvider());
            BankDbContextOptions.Configure(options, connectionString);
        });

        return services;
    }
}
