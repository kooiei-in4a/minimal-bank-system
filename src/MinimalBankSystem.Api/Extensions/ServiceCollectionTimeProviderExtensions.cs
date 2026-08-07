using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MinimalBankSystem.Api.Extensions;

internal static class ServiceCollectionTimeProviderExtensions
{
    public static IServiceCollection TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        return services;
    }
}
