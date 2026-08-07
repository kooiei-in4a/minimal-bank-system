using Microsoft.Extensions.Logging;

namespace MinimalBankSystem.Api.RuntimeContract;

internal static partial class TechnicalLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Unhandled exception mapped to a safe API error contract. {CorrelationId} {ErrorCode}")]
    public static partial void UnhandledException(
        this ILogger logger,
        string correlationId,
        string errorCode);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "HTTP request completed. {CorrelationId} {StatusCode} {ElapsedMilliseconds}")]
    public static partial void RequestCompleted(
        this ILogger logger,
        string correlationId,
        int statusCode,
        long elapsedMilliseconds);
}
