using Microsoft.Extensions.DependencyInjection;

namespace MinimalBankSystem.Infrastructure.Auditing;

public static class AuditPersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Installs the production Audit writer and transaction primitives. Feature leaves register
    /// only their own operation identifiers; an empty registry fails closed until they do.
    /// </summary>
    public static IServiceCollection AddAuditPersistence(
        this IServiceCollection services,
        params string[] registeredOperationIdentifiers)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registeredOperationIdentifiers);

        services.AddSingleton(new AuditOperationRegistry(registeredOperationIdentifiers));
        services.AddScoped<AuditWriter>();
        services.AddScoped<AuditTransactionRunner>();
        return services;
    }
}
