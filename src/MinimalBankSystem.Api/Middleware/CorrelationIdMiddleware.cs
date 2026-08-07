using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MinimalBankSystem.Application.Time;

namespace MinimalBankSystem.Api.Middleware;

public sealed partial class CorrelationIdMiddleware
{
    [GeneratedRegex(@"^[A-Za-z0-9._\-]{1,128}$")]
    private static partial Regex SafeCorrelationIdPattern();

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = ResolveOrGenerate(context);
        context.Items[TimeProviderKeys.CorrelationIdItemKey] = correlationId;
        context.Response.Headers[TimeProviderKeys.CorrelationIdHeader] = correlationId;
        Activity.Current?.SetTag("correlation_id", correlationId);

        ILoggerFactory? loggerFactory = context.RequestServices.GetService<ILoggerFactory>();
        using IDisposable? scope = loggerFactory?.CreateLogger("MinimalBankSystem.Correlation")
            .BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = correlationId });

        await _next(context);
    }

    private static string ResolveOrGenerate(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(TimeProviderKeys.CorrelationIdHeader, out var values))
        {
            string? supplied = values.ToString();
            if (!string.IsNullOrEmpty(supplied) && SafeCorrelationIdPattern().IsMatch(supplied))
            {
                return supplied;
            }
        }

        return Guid.NewGuid().ToString("N");
    }
}
