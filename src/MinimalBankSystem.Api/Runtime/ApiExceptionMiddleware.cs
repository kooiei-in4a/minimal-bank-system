namespace MinimalBankSystem.Api.Runtime;

public sealed class ApiExceptionMiddleware(
    RequestDelegate next,
    ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        IEnumerable<IApiExceptionMapper> exceptionMappers)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                TechnicalLog.RequestFailed(
                    logger,
                    ApiErrorMapping.InternalError.Code,
                    ApiErrorMapping.InternalError.StatusCode,
                    exception.GetType().FullName ?? exception.GetType().Name,
                    context.TraceIdentifier);
                return;
            }

            ApiErrorMapping mapping = Map(
                exception,
                exceptionMappers,
                context.RequestAborted.IsCancellationRequested);

            TechnicalLog.RequestFailed(
                logger,
                mapping.Code,
                mapping.StatusCode,
                exception.GetType().FullName ?? exception.GetType().Name,
                context.TraceIdentifier);

            context.Response.Clear();
            context.Response.StatusCode = mapping.StatusCode;
            context.Response.Headers[CorrelationIdMiddleware.HeaderName] = context.TraceIdentifier;

            await context.Response.WriteAsJsonAsync(
                new ApiErrorEnvelope(mapping.Code, mapping.Message),
                context.RequestAborted);
        }
    }

    private static ApiErrorMapping Map(
        Exception exception,
        IEnumerable<IApiExceptionMapper> exceptionMappers,
        bool requestAborted)
    {
        foreach (IApiExceptionMapper mapper in exceptionMappers)
        {
            try
            {
                ApiErrorMapping? mapping = mapper.TryMap(exception);

                if (mapping is not null)
                {
                    return mapping;
                }
            }
            catch (OperationCanceledException) when (requestAborted)
            {
                throw;
            }
            catch (Exception)
            {
                return ApiErrorMapping.InternalError;
            }
        }

        return ApiErrorMapping.InternalError;
    }
}

internal static partial class TechnicalLog
{
    // Allow-list only. Never pass an exception object, message, stack trace,
    // request body/header/query, credential, token, raw key, or personal data.
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "HTTP request failed with {ErrorCode} ({HttpStatusCode}). Exception type: {ExceptionType}. Correlation ID: {CorrelationId}.")]
    public static partial void RequestFailed(
        ILogger logger,
        string errorCode,
        int httpStatusCode,
        string exceptionType,
        string correlationId);
}
