using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MinimalBankSystem.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = ResolveCorrelationId(context);

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        Activity.Current?.SetTag("correlation_id", correlationId);

        ILogger<CorrelationIdMiddleware>? logger = context.RequestServices.GetService<ILogger<CorrelationIdMiddleware>>();
        var scope = new Dictionary<string, object?> { ["CorrelationId"] = correlationId };
        using (logger?.BeginScope(scope))
        {
            await _next(context);
        }
    }

    internal static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var values)
            && values.Count > 0
            && values[0] is { Length: > 0 and <= 128 } supplied)
        {
            return supplied;
        }

        return Guid.NewGuid().ToString("N");
    }
}
