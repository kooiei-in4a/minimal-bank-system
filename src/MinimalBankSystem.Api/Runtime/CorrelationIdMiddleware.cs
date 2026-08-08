using Microsoft.Extensions.Primitives;

namespace MinimalBankSystem.Api.Runtime;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    private const int MaxCallerSuppliedLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = Resolve(context.Request.Headers[HeaderName]);

        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using IDisposable? scope = logger.BeginScope(
            new Dictionary<string, object?>
            {
                ["CorrelationId"] = correlationId,
            });

        await next(context);
    }

    private static string Resolve(StringValues suppliedValues)
    {
        if (suppliedValues.Count == 1 && IsSafe(suppliedValues[0]))
        {
            return suppliedValues[0]!;
        }

        return Guid.NewGuid().ToString("N");
    }

    private static bool IsSafe(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxCallerSuppliedLength)
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.')
            {
                return false;
            }
        }

        return true;
    }
}
