using System.Text.Json;
using MinimalBankSystem.Application.Time;

namespace MinimalBankSystem.Api.Runtime;

public static partial class ApiRuntimeExtensions
{
    public static IServiceCollection AddApiRuntimeContract(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ApplicationTime>();
        services.AddSingleton<ExceptionHttpMapperRegistry>();
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        return services;
    }

    public static IApplicationBuilder UseApiRuntimeContract(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        return app;
    }

    public static void MapApiContractProbes(this WebApplication app)
    {
        if (!app.Configuration.GetValue("EnableApiContractProbes", false))
        {
            return;
        }

        // Test-only paths: enabled solely for API contract verification hosts.
        RouteGroupBuilder probes = app.MapGroup("/__contract__");

        probes.MapGet(
            "/ping",
            (HttpContext httpContext, ILoggerFactory loggerFactory) =>
            {
                string correlationId = (string)httpContext.Items[CorrelationId.HttpContextItemKey]!;
                ILogger logger = loggerFactory.CreateLogger("MinimalBankSystem.Api.ContractProbe");
                LogPing(logger, correlationId);
                return Results.Ok(new { status = "ok", correlationId });
            });

        probes.MapGet(
            "/utc-now",
            (ApplicationTime applicationTime) =>
            {
                DateTimeOffset utcNow = applicationTime.GetUtcNow();
                return Results.Ok(new { utcNow });
            });

        probes.MapGet(
            "/unmapped-exception",
            () =>
            {
                throw new InvalidOperationException(
                    "PROBE_UNMAPPED_DETAIL password=SENTINEL_PASSWORD_VALUE jwt=SENTINEL_JWT_VALUE");
            });

        probes.MapPost(
            "/safe-log",
            (HttpContext httpContext, ILoggerFactory loggerFactory) =>
            {
                ILogger logger = loggerFactory.CreateLogger("MinimalBankSystem.Api.ContractProbe");
                string? requestPath = httpContext.Request.Path.Value;
                string? correlationId = httpContext.Items[CorrelationId.HttpContextItemKey] as string;

                // Intentionally log only safe structural fields. Request bodies and prohibited
                // headers are never copied into technical logs (ADR-0008).
                LogContractProbe(logger, httpContext.Request.Method, requestPath, correlationId);

                return Results.Ok(new { status = "logged" });
            });
    }

    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Information,
        Message = "Contract probe observed {RequestMethod} {RequestPath}. CorrelationId={CorrelationId}")]
    private static partial void LogContractProbe(
        ILogger logger,
        string requestMethod,
        string? requestPath,
        string? correlationId);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Information,
        Message = "Contract probe ping. CorrelationId={CorrelationId}")]
    private static partial void LogPing(ILogger logger, string correlationId);
}
