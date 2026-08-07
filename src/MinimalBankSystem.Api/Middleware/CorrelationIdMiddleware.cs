using Microsoft.Extensions.Primitives;
using MinimalBankSystem.Api.Infrastructure;

namespace MinimalBankSystem.Api.Middleware;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        StringValues suppliedValues = context.Request.Headers[CorrelationId.HeaderName];
        string correlationId = suppliedValues.Count == 1
            && CorrelationId.TryNormalize(suppliedValues[0], out string normalized)
            ? normalized
            : CorrelationId.Create();

        context.Items[CorrelationId.HttpContextItemKey] = correlationId;
        context.Response.Headers[CorrelationId.HeaderName] = correlationId;

        using IDisposable? scope = logger.BeginScope(
            new Dictionary<string, object?> { ["CorrelationId"] = correlationId });

        await next(context);

        TechnicalLogging.RequestCompleted(logger, correlationId, context.Response.StatusCode);
    }
}
