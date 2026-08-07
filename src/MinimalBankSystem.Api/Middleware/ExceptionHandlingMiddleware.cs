using MinimalBankSystem.Api.ExceptionMapping;
using MinimalBankSystem.Domain;

namespace MinimalBankSystem.Api.Middleware;

public sealed partial class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IExceptionMapper _exceptionMapper;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception mapped to {StatusCode} {ErrorCode}: {Message}")]
    private static partial void LogMappedException(ILogger logger, int statusCode, string errorCode, string message);

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        IExceptionMapper exceptionMapper,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _exceptionMapper = exceptionMapper;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (statusCode, errorResponse) = MapException(ex);

            LogMappedException(_logger, statusCode, errorResponse.Code, errorResponse.Message);

            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(errorResponse);
        }
    }

    private (int StatusCode, ErrorResponse ErrorResponse) MapException(Exception ex)
    {
        if (_exceptionMapper.TryMap(ex, out var statusCode, out var errorResponse))
        {
            return (statusCode, errorResponse);
        }

        return (
            StatusCodes.Status500InternalServerError,
            new ErrorResponse(ErrorCodes.InternalError, "An internal error occurred."));
    }
}
