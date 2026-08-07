namespace MinimalBankSystem.Api.Runtime;

public sealed class ApiExceptionHandlingMiddleware(
    RequestDelegate next,
    IEnumerable<IApiExceptionMapper> exceptionMappers,
    ILogger<ApiExceptionHandlingMiddleware> logger)
{
    private static readonly ApiExceptionMapping UnmappedExceptionResponse = new(
        StatusCodes.Status500InternalServerError,
        new ApiErrorResponse("internal_error", "An unexpected error occurred."));
    private static readonly Action<ILogger, string, string, string, Exception?> LogUnhandledApiException =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Error,
            new EventId(1000, "UnhandledApiException"),
            "Unhandled API exception. CorrelationId={CorrelationId} ErrorCode={ErrorCode} ExceptionType={ExceptionType}");

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            ApiExceptionMapping mapping = exceptionMappers
                .Select(mapper => mapper.TryMap(exception))
                .FirstOrDefault(candidate => candidate is not null)
                ?? UnmappedExceptionResponse;
            string correlationId = CorrelationIdMiddleware.GetCorrelationId(context);

            LogUnhandledApiException(
                logger,
                correlationId,
                mapping.Error.Code,
                exception.GetType().Name,
                null);

            context.Response.Clear();
            context.Response.StatusCode = mapping.StatusCode;
            context.Response.ContentType = "application/json";
            context.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;

            await context.Response.WriteAsJsonAsync(mapping.Error);
        }
    }
}
