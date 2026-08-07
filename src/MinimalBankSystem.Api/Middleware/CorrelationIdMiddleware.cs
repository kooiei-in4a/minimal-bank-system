namespace MinimalBankSystem.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemsKey = "CorrelationId";

    private const int MaxCallerSuppliedLength = 64;

    private static readonly Func<ILogger, string, IDisposable?> CorrelationIdScope = LoggerMessage.DefineScope<string>("{CorrelationId}");

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string? callerSupplied = context.Request.Headers[HeaderName].FirstOrDefault();
        string correlationId = IsSafe(callerSupplied) ? callerSupplied! : Guid.NewGuid().ToString("N");

        context.Items[ItemsKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (CorrelationIdScope(_logger, correlationId))
        {
            await _next(context);
        }
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
