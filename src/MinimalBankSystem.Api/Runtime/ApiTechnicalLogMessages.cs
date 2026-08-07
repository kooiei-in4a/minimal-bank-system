namespace MinimalBankSystem.Api.Runtime;

internal static partial class ApiTechnicalLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "API exception mapped to fixed error code {ErrorCode}")]
    public static partial void LogMappedApiException(
        ILogger logger,
        Exception exception,
        string errorCode);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Contract time probe")]
    public static partial void LogContractTimeProbe(ILogger logger);
}
