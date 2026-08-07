using Microsoft.AspNetCore.Builder;
using MinimalBankSystem.Api.Middleware;

namespace MinimalBankSystem.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseMinimalBankSystemRuntime(this IApplicationBuilder app)
    {
        return app
            .UseMiddleware<CorrelationIdMiddleware>()
            .UseMiddleware<GlobalExceptionMiddleware>();
    }
}
