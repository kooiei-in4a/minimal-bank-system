namespace MinimalBankSystem.Api.ErrorHandling;

internal static partial class GlobalExceptionHandlerLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled request exception. {ErrorCode}")]
    public static partial void UnhandledRequestException(ILogger logger, Exception exception, string errorCode);
}
