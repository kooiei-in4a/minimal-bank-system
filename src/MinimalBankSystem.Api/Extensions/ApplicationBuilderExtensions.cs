using MinimalBankSystem.Api.Middleware;
using Microsoft.AspNetCore.Builder;

namespace MinimalBankSystem.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseMinimalBankSystemApi(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<GlobalExceptionMiddleware>();
        return app;
    }
}
