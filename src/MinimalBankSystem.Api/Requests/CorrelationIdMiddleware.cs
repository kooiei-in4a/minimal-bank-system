using Microsoft.AspNetCore.Http;

namespace MinimalBankSystem.Api.Requests;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = CorrelationIdContract.NormalizeOrCreate(
            context.Request.Headers[CorrelationIdContract.HeaderName]);

        context.TraceIdentifier = correlationId;
        context.Response.Headers[CorrelationIdContract.HeaderName] = correlationId;

        using IDisposable? scope = logger.BeginScope(
            new Dictionary<string, object?>
            {
                ["CorrelationId"] = correlationId,
            });

        await next(context);
    }
}
