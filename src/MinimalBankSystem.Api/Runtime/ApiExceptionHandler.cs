using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;

namespace MinimalBankSystem.Api.Runtime;

public sealed partial class ApiExceptionHandler : IExceptionHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ExceptionHttpMapperRegistry _mapperRegistry;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(
        ExceptionHttpMapperRegistry mapperRegistry,
        ILogger<ApiExceptionHandler> logger)
    {
        _mapperRegistry = mapperRegistry;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        bool mapped = _mapperRegistry.TryMap(exception, out int statusCode, out ApiErrorResponse errorResponse);
        string correlationId = httpContext.Items.TryGetValue(CorrelationId.HttpContextItemKey, out object? value)
            && value is string existing
                ? existing
                : CorrelationId.Create();

        httpContext.Response.Headers[CorrelationId.HeaderName] = correlationId;

        // Technical log: include correlation ID, fixed error code, and exception type.
        // Do not log request bodies, prohibited headers, or exception messages that may carry secrets.
        if (mapped)
        {
            LogMappedException(_logger, exception.GetType().FullName, errorResponse.Code, correlationId);
        }
        else
        {
            LogUnmappedException(_logger, exception.GetType().FullName, errorResponse.Code, correlationId);
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            errorResponse,
            SerializerOptions,
            cancellationToken);

        return true;
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Mapped exception of type {ExceptionType}. ErrorCode={ErrorCode} CorrelationId={CorrelationId}")]
    private static partial void LogMappedException(
        ILogger logger,
        string? exceptionType,
        string errorCode,
        string correlationId);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Unmapped exception of type {ExceptionType}. ErrorCode={ErrorCode} CorrelationId={CorrelationId}")]
    private static partial void LogUnmappedException(
        ILogger logger,
        string? exceptionType,
        string errorCode,
        string correlationId);
}
