using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;
using MinimalBankSystem.Api.Logging;

namespace MinimalBankSystem.Api.Correlation;

public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        string correlationId = ResolveCorrelationId(context);

        context.Response.OnStarting(static state =>
        {
            (HttpResponse response, string id) = ((HttpResponse, string))state;
            response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        }, (context.Response, correlationId));

        IReadOnlyDictionary<string, object?> scope = SensitiveLogFieldPolicy.Sanitize(
            new Dictionary<string, object?> { ["CorrelationId"] = correlationId });

        using (logger.BeginScope(scope))
        {
            await next(context);
        }
    }

    // Caller-supplied values are only trusted when they match a safe, bounded
    // character set; anything else is replaced so log/response injection or
    // unbounded values from an untrusted caller never propagate downstream.
    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out StringValues values))
        {
            string? candidate = values.ToString();
            if (!string.IsNullOrEmpty(candidate) && SafeCorrelationIdPattern().IsMatch(candidate))
            {
                return candidate;
            }
        }

        return Guid.NewGuid().ToString("n");
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,128}$")]
    private static partial Regex SafeCorrelationIdPattern();
}
