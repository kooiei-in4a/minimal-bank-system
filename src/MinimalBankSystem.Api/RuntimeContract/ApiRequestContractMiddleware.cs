namespace MinimalBankSystem.Api.RuntimeContract;

public sealed class ApiRequestContractMiddleware
{
    private readonly RequestDelegate _next;

    public ApiRequestContractMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(
        HttpContext context,
        TimeProvider timeProvider,
        IApiExceptionMapper exceptionMapper,
        ILogger<ApiRequestContractMiddleware> logger)
    {
        string correlationId = CorrelationId.From(context.Request.Headers[CorrelationId.HeaderName]);
        context.Items[CorrelationId.ContextItemKey] = correlationId;
        context.Response.Headers[CorrelationId.HeaderName] = correlationId;

        using IDisposable? scope = logger.BeginScope(
            new Dictionary<string, object?>
            {
                ["CorrelationId"] = correlationId,
            });

        long startTimestamp = timeProvider.GetTimestamp();

        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            ApiErrorMapping mapping = ApiErrorMapping.InternalError;

            try
            {
                mapping = exceptionMapper.Map(exception) ?? ApiErrorMapping.InternalError;
            }
            catch
            {
                mapping = ApiErrorMapping.InternalError;
            }

            context.Response.Clear();
            context.Response.StatusCode = mapping.StatusCode;
            context.Response.Headers[CorrelationId.HeaderName] = correlationId;
            await context.Response.WriteAsJsonAsync(new ApiErrorResponse(mapping.Code, mapping.Message));

            logger.UnhandledException(correlationId, mapping.Code);
        }
        finally
        {
            long elapsedMilliseconds = 0;
            if (logger.IsEnabled(LogLevel.Information))
            {
                TimeSpan elapsed = timeProvider.GetElapsedTime(startTimestamp);
                elapsedMilliseconds = Math.Max(0, (long)elapsed.TotalMilliseconds);
            }

            logger.RequestCompleted(
                correlationId,
                context.Response.StatusCode,
                elapsedMilliseconds);
        }
    }
}
