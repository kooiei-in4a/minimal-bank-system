using Microsoft.Extensions.DependencyInjection;

namespace MinimalBankSystem.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMinimalBankSystemRuntime(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddSingletonTimeProvider();
        return services;
    }
}
