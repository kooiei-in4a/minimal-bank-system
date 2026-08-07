using Microsoft.Extensions.Primitives;

namespace MinimalBankSystem.Api.Runtime;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    private const string ItemKey = "MinimalBankSystem.CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = CreateCorrelationId(context.Request.Headers[HeaderName]);

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    public static string GetCorrelationId(HttpContext context)
    {
        return context.Items.TryGetValue(ItemKey, out object? value) && value is string correlationId
            ? correlationId
            : throw new InvalidOperationException("A correlation ID must be established before handling API errors.");
    }

    private static string CreateCorrelationId(StringValues suppliedValues)
    {
        if (suppliedValues.Count == 1
            && Guid.TryParseExact(suppliedValues[0], "D", out Guid suppliedCorrelationId))
        {
            return suppliedCorrelationId.ToString("D");
        }

        return Guid.NewGuid().ToString("D");
    }
}
