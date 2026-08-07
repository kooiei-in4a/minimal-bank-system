using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MinimalBankSystem.Api.Models;
using MinimalBankSystem.Application.Time;

namespace MinimalBankSystem.Api.Middleware;

public sealed partial class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception. code={Code} correlationId={CorrelationId}")]
    private static partial void LogUnhandled(ILogger logger, string code, string? correlationId, Exception ex);

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
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            await WriteInternalErrorAsync(context, ex);
        }
        catch (Exception ex)
        {
            LogUnhandled(_logger, ErrorCodes.InternalError, GetCorrelationId(context), ex);
            throw;
        }
    }

    private async Task WriteInternalErrorAsync(HttpContext context, Exception ex)
    {
        LogUnhandled(_logger, ErrorCodes.InternalError, GetCorrelationId(context), ex);

        context.Response.Clear();
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var body = new ApiErrorResponse
        {
            Code = ErrorCodes.InternalError,
            Message = "An unexpected error occurred.",
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }

    private static string? GetCorrelationId(HttpContext context)
    {
        return context.Items[TimeProviderKeys.CorrelationIdItemKey] as string;
    }
}
