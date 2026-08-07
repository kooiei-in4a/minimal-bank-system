using System.Diagnostics;
using Microsoft.Extensions.Primitives;

namespace MinimalBankSystem.Api.Middleware;

public sealed partial class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private const int MaxCorrelationIdLength = 128;
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        StringValues headerValues = context.Request.Headers[CorrelationIdHeader];
        string? callerSupplied = headerValues.Count > 0 ? headerValues.ToString() : null;
        string correlationId = SanitizeOrGenerate(callerSupplied);

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        using IDisposable? scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        Activity.Current?.SetTag("correlation_id", correlationId);

        await _next(context);
    }

    private static string SanitizeOrGenerate(string? callerSupplied)
    {
        if (!string.IsNullOrWhiteSpace(callerSupplied))
        {
            string trimmed = callerSupplied.Trim();

            if (trimmed.Length <= MaxCorrelationIdLength && IsSafeCorrelationId(trimmed))
            {
                return trimmed;
            }
        }

        return Guid.NewGuid().ToString("D");
    }

    private static bool IsSafeCorrelationId(string value)
    {
        foreach (char c in value)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.')
            {
                return false;
            }
        }

        return true;
    }
}
