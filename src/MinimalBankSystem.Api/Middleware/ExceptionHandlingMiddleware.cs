using MinimalBankSystem.Api.Contracts;
using MinimalBankSystem.Api.Errors;
using MinimalBankSystem.Api.Infrastructure;

namespace MinimalBankSystem.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    IEnumerable<IExceptionToHttpMapper> mappers,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            ErrorMapping mapping = MapException(exception);
            string correlationId = (string)context.Items[CorrelationId.HttpContextItemKey]!;

            TechnicalLogging.UnhandledException(logger, correlationId, mapping.Code, exception);

            context.Response.Clear();
            context.Response.StatusCode = mapping.StatusCode;
            context.Response.Headers[CorrelationId.HeaderName] = correlationId;

            await context.Response.WriteAsJsonAsync(new ErrorResponse(mapping.Code, mapping.Message));
        }
    }

    private ErrorMapping MapException(Exception exception)
    {
        foreach (IExceptionToHttpMapper mapper in mappers.Reverse())
        {
            if (mapper.TryMap(exception, out ErrorMapping? mapping))
            {
                return mapping;
            }
        }

        return new(
            StatusCodes.Status500InternalServerError,
            "data_integrity_violation",
            "An internal error occurred.");
    }
}
