using Microsoft.AspNetCore.Diagnostics;

namespace MinimalBankSystem.Api.ErrorHandling;

public sealed class GlobalExceptionHandler(
    IEnumerable<IApiExceptionMapper> mappers,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private const string FallbackCode = "internal_error";
    private const string FallbackMessage = "An unexpected error occurred while processing the request.";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ApiError error = Map(exception);

        GlobalExceptionHandlerLog.UnhandledRequestException(logger, exception, error.Code);

        httpContext.Response.StatusCode = error.HttpStatusCode;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(
            new ApiErrorResponse(error.Code, error.Message),
            cancellationToken);

        return true;
    }

    private ApiError Map(Exception exception)
    {
        foreach (IApiExceptionMapper mapper in mappers)
        {
            if (mapper.TryMap(exception, out ApiError? mapped))
            {
                return mapped;
            }
        }

        return new ApiError(StatusCodes.Status500InternalServerError, FallbackCode, FallbackMessage);
    }
}
