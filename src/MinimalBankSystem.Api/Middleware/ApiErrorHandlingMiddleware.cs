using MinimalBankSystem.Api.Errors;

namespace MinimalBankSystem.Api.Middleware;

public sealed class ApiErrorHandlingMiddleware
{
    private static readonly Action<ILogger, int, string, Exception?> LogUnhandledException = LoggerMessage.Define<int, string>(
        LogLevel.Error,
        new EventId(1, "UnhandledRequestException"),
        "Unhandled request exception mapped to HTTP {StatusCode} / {ErrorCode}");

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiErrorHandlingMiddleware> _logger;

    public ApiErrorHandlingMiddleware(RequestDelegate next, ILogger<ApiErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IApiErrorMapper errorMapper)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            ApiErrorResult result = errorMapper.Map(exception);
            LogUnhandledException(_logger, result.StatusCode, result.Envelope.Code, exception);

            context.Response.StatusCode = result.StatusCode;
            await context.Response.WriteAsJsonAsync(result.Envelope);
        }
    }
}
