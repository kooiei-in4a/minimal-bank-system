using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace MinimalBankSystem.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMinimalBankSystemApi(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
