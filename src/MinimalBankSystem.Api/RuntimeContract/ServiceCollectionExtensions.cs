using MinimalBankSystem.Application.Runtime;

namespace MinimalBankSystem.Api.RuntimeContract;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiRuntimeContract(this IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<ApplicationClock>();
        services.AddSingleton<IApiExceptionMapper, DefaultApiExceptionMapper>();

        return services;
    }
}
