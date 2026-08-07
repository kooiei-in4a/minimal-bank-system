using System.Text.Json;
using MinimalBankSystem.Api.CorrelationId;

namespace MinimalBankSystem.Api.ErrorHandling;

public sealed class ExceptionMiddleware
{
    private static readonly Action<ILogger, int, string, string, Exception?> LogExceptionMapped =
        LoggerMessage.Define<int, string, string>(
            LogLevel.Error,
            new EventId(1, "ExceptionMapped"),
            "Unhandled exception mapped to {StatusCode} {ErrorCode}. CorrelationId: {CorrelationId}");

    private readonly RequestDelegate _next;
    private readonly IExceptionMapper _mapper;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ExceptionMiddleware(
        RequestDelegate next,
        IExceptionMapper mapper,
        ILogger<ExceptionMiddleware> logger,
        ICorrelationIdAccessor correlationIdAccessor)
    {
        _next = next;
        _mapper = mapper;
        _logger = logger;
        _correlationIdAccessor = correlationIdAccessor;
    }

    public async Task InvokeAsync(HttpContext context)
    {
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

            (int statusCode, string code) = _mapper.Map(exception);
            string correlationId = _correlationIdAccessor.Current ?? string.Empty;

            LogExceptionMapped(_logger, statusCode, code, correlationId, exception);

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var response = new ErrorResponse(
                code,
                statusCode == 500 ? "An internal error occurred." : exception.Message);

            await context.Response.WriteAsJsonAsync(response, JsonOptions);
        }
    }
}
