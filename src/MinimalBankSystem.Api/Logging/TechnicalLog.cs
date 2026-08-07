namespace MinimalBankSystem.Api.Logging;

internal static partial class TechnicalLog
{
    // Technical events use an allow-list. Never add request headers or bodies,
    // exception messages/data/stack traces, credentials, tokens, signing keys,
    // raw idempotency keys, connection strings, or personal data to this event.
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "HTTP request failed with {ErrorCode} ({HttpStatusCode}). Correlation ID: {CorrelationId}. Exception type: {ExceptionType}.")]
    public static partial void RequestFailed(
        ILogger logger,
        string correlationId,
        string errorCode,
        int httpStatusCode,
        string exceptionType);
}
