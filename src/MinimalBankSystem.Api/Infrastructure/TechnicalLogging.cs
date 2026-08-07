using Microsoft.Extensions.Logging;

namespace MinimalBankSystem.Api.Infrastructure;

public static class TechnicalLogging
{
    private static readonly Action<ILogger, string, int, Exception?> RequestCompletedMessage =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(5001, "RequestCompleted"),
            "HTTP request completed. CorrelationId: {CorrelationId}. StatusCode: {StatusCode}.");

    private static readonly Action<ILogger, string, string, string, Exception?> UnhandledExceptionMessage =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Error,
            new EventId(5000, "UnhandledRequest"),
            "Unhandled request. CorrelationId: {CorrelationId}. ErrorCode: {ErrorCode}. ExceptionType: {ExceptionType}.");

    public static void RequestCompleted(ILogger logger, string correlationId, int statusCode)
    {
        RequestCompletedMessage(logger, correlationId, statusCode, null);
    }

    public static void UnhandledException(
        ILogger logger,
        string correlationId,
        string errorCode,
        Exception exception)
    {
        // Do not pass the exception to the logger: its message can contain secrets or personal data.
        UnhandledExceptionMessage(
            logger,
            correlationId,
            errorCode,
            exception.GetType().Name,
            null);
    }
}
