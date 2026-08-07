using System.Net;
using MinimalBankSystem.Api.Models;
using MinimalBankSystem.Domain.Errors;

namespace MinimalBankSystem.Api.Middleware;

public sealed partial class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
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
            string correlationId = context.Items["CorrelationId"] as string ?? "unknown";

            LogUnhandledException(ex, correlationId, DomainErrors.Common.InternalError);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/problem+json";

                var error = new ErrorResponse(
                    DomainErrors.Common.InternalError,
                    "An internal error occurred. Please try again later.");

                await context.Response.WriteAsJsonAsync(error);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception occurred. CorrelationId={CorrelationId}, ErrorCode={ErrorCode}")]
    private partial void LogUnhandledException(Exception exception, string correlationId, string errorCode);
}
