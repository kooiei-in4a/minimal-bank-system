namespace MinimalBankSystem.Api.Runtime;

/// <summary>
/// The complete set of technical log events emitted by the common API runtime contract.
/// </summary>
/// <remarks>
/// Declaring every event here keeps the logged fields auditable: only the correlation scope, the
/// fixed error code, the HTTP status, the request method and the request path are recorded. The
/// request path deliberately excludes the query string, and no request body, header or configuration
/// value is accepted as a log argument.
/// </remarks>
internal static partial class ApiRuntimeLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Unmapped exception reached the API boundary. ErrorCode={ErrorCode} StatusCode={StatusCode} RequestMethod={RequestMethod} RequestPath={RequestPath}")]
    public static partial void UnmappedException(
        this ILogger logger,
        Exception exception,
        string errorCode,
        int statusCode,
        string requestMethod,
        string? requestPath);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Request failed with a mapped error. ErrorCode={ErrorCode} StatusCode={StatusCode} RequestMethod={RequestMethod} RequestPath={RequestPath}")]
    public static partial void MappedFailure(
        this ILogger logger,
        string errorCode,
        int statusCode,
        string requestMethod,
        string? requestPath);
}
