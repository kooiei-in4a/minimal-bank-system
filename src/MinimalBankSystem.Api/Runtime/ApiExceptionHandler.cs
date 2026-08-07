using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;

namespace MinimalBankSystem.Api.Runtime;

public sealed class ApiExceptionHandler : IExceptionHandler
{
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
        ApiErrorDefinition error = _mapperRegistry.TryMap(exception, out ApiErrorDefinition mapped)
            ? mapped
            : ApiErrorCatalog.UnmappedException;

        string? correlationId = httpContext.Items[CorrelationId.HttpContextItemKey] as string;
        if (correlationId is not null)
        {
            using (_logger.BeginScope(new Dictionary<string, object?>
            {
                [CorrelationId.LogPropertyName] = correlationId,
            }))
            {
                LogMappedException(error, exception);
            }
        }
        else
        {
            LogMappedException(error, exception);
        }

        httpContext.Response.StatusCode = error.StatusCode;
        httpContext.Response.ContentType = "application/json";

        ApiErrorResponse payload = new(error.Code, error.Message);
        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(payload, JsonSerializerOptions.Web),
            cancellationToken);

        return true;
    }

    private void LogMappedException(ApiErrorDefinition error, Exception exception)
    {
        ApiTechnicalLogMessages.LogMappedApiException(_logger, exception, error.Code);
    }
}
