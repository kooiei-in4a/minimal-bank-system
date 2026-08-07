using System.Net;
using System.Text.Json;
using MinimalBankSystem.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MinimalBankSystem.Api.Middleware;

public sealed partial class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Response already started, cannot write error response")]
    private static partial void LogResponseAlreadyStarted(ILogger<GlobalExceptionMiddleware> logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception. CorrelationId: {CorrelationId}")]
    private static partial void LogUnhandledException(
        ILogger<GlobalExceptionMiddleware> logger,
        Exception exception,
        string? correlationId);

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            LogResponseAlreadyStarted(_logger, exception);
            return;
        }

        string? correlationId = context.Items["CorrelationId"] as string;

        LogUnhandledException(_logger, exception, correlationId);

        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var response = new ApiErrorResponse
        {
            Code = ErrorCodes.InternalError,
            Message = "An unexpected error occurred",
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, SerializerOptions));
    }
}
