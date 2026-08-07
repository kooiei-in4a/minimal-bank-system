namespace MinimalBankSystem.Api.CorrelationId;

public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 128;

    private static readonly Action<ILogger, string, Exception?> LogCallerSupplied =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(1, "CallerCorrelationId"),
            "Using caller-supplied correlation ID {CorrelationId}.");

    private static readonly Action<ILogger, Exception?> LogRejectedCaller =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2, "RejectedCorrelationId"),
            "Rejected caller-supplied correlation ID: unsafe value.");

    private static readonly Action<ILogger, string, Exception?> LogGenerated =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(3, "GeneratedCorrelationId"),
            "Generated new correlation ID {CorrelationId}.");

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationIdAccessor accessor)
    {
        string correlationId = ResolveCorrelationId(context);

        if (accessor is CorrelationIdAccessor concreteAccessor)
        {
            concreteAccessor.Current = correlationId;
        }

        context.Items["CorrelationId"] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await _next(context);
        }
    }

    private string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var supplied) &&
            !string.IsNullOrWhiteSpace(supplied))
        {
            string rawValue = supplied.ToString();

            if (IsValidCorrelationId(rawValue))
            {
                LogCallerSupplied(_logger, rawValue, null);
                return rawValue;
            }

            LogRejectedCaller(_logger, null);
        }

        string generated = Guid.NewGuid().ToString("D");
        LogGenerated(_logger, generated, null);
        return generated;
    }

    internal static bool IsValidCorrelationId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength)
        {
            return false;
        }

        foreach (char c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')
            {
                return false;
            }
        }

        return true;
    }
}
