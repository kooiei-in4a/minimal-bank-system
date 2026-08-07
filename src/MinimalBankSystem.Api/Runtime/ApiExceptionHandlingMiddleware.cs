namespace MinimalBankSystem.Api.Runtime;

/// <summary>
/// Converts every exception that reaches the API boundary into the common error envelope.
/// </summary>
/// <remarks>
/// Registered <see cref="IApiExceptionMapper"/> instances are consulted first. An exception that no
/// mapper owns becomes the fixed unmapped failure contract, so internal detail and stack traces stay
/// inside the technical log and never reach the caller.
/// </remarks>
internal sealed class ApiExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ApiExceptionHandlingMiddleware> logger)
{
    private static readonly ApiError UnmappedFailure = new(
        StatusCodes.Status500InternalServerError,
        ApiErrorCodes.InternalError,
        "The request could not be completed because of an internal error.");

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            ApiError? mapped = Map(context, exception);
            ApiError error = mapped ?? UnmappedFailure;

            // Only the fixed diagnostic fields of ApiRuntimeLog are recorded. Request bodies,
            // headers, query strings and configuration values are never part of a log event.
            if (mapped is null)
            {
                logger.UnmappedException(
                    exception,
                    error.Code,
                    error.StatusCode,
                    context.Request.Method,
                    context.Request.Path.Value);
            }
            else
            {
                logger.MappedFailure(
                    error.Code,
                    error.StatusCode,
                    context.Request.Method,
                    context.Request.Path.Value);
            }

            if (context.Response.HasStarted)
            {
                // The envelope can no longer be written, so the request is failed at the server
                // instead of appending unstructured content to a partial response.
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = error.StatusCode;
            await context.Response.WriteAsJsonAsync(error.ToResponse(), context.RequestAborted);
        }
    }

    private static ApiError? Map(HttpContext context, Exception exception)
    {
        foreach (IApiExceptionMapper mapper in context.RequestServices.GetServices<IApiExceptionMapper>())
        {
            ApiError? error = mapper.Map(exception);
            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }
}
