using Microsoft.AspNetCore.Http;
using MinimalBankSystem.Api.Logging;
using MinimalBankSystem.Api.Requests;

namespace MinimalBankSystem.Api.Errors;

public sealed class ApiExceptionMiddleware(
    RequestDelegate next,
    IEnumerable<IExceptionToHttpMapper> exceptionMappers,
    ILogger<ApiExceptionMiddleware> logger)
{
    private static readonly ApiErrorMapping UnmappedException = new(
        StatusCodes.Status500InternalServerError,
        "internal_error",
        "An internal error occurred.");

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            ApiErrorMapping mapping = Map(exception);

            TechnicalLog.RequestFailed(
                logger,
                context.TraceIdentifier,
                mapping.Code,
                mapping.StatusCode,
                exception.GetType().FullName ?? exception.GetType().Name);

            context.Response.Clear();
            context.Response.StatusCode = mapping.StatusCode;
            context.Response.Headers[CorrelationIdContract.HeaderName] = context.TraceIdentifier;

            await context.Response.WriteAsJsonAsync(
                new ApiErrorEnvelope(mapping.Code, mapping.Message),
                context.RequestAborted);
        }
    }

    private ApiErrorMapping Map(Exception exception)
    {
        foreach (IExceptionToHttpMapper mapper in exceptionMappers)
        {
            ApiErrorMapping? mapping = mapper.Map(exception);

            if (mapping is not null)
            {
                return mapping;
            }
        }

        return UnmappedException;
    }
}
