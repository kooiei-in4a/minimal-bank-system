using System.Text.RegularExpressions;

namespace MinimalBankSystem.Api.Middleware;

public sealed partial class CorrelationIdMiddleware
{
    private const int MaxCorrelationIdLength = 128;
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private const string CorrelationIdItemKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);

        context.Response.Headers[CorrelationIdHeader] = correlationId;
        context.Items[CorrelationIdItemKey] = correlationId;

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        await _next(context);
    }

    internal static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var values))
        {
            var value = values.FirstOrDefault();
            if (!string.IsNullOrEmpty(value) && IsValidCallerSupplied(value))
            {
                return Truncate(value);
            }
        }
        return GenerateNew();
    }

    [GeneratedRegex(@"[\p{C}]")]
    private static partial Regex ControlCharRegex();

    internal static bool IsValidCallerSupplied(string value)
    {
        return value.Length <= MaxCorrelationIdLength
            && !ControlCharRegex().IsMatch(value);
    }

    internal static string Truncate(string value)
    {
        return value.Length > MaxCorrelationIdLength ? value[..MaxCorrelationIdLength] : value;
    }

    internal static string GenerateNew()
    {
        return Guid.NewGuid().ToString("N");
    }
}
