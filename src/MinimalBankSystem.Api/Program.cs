using Microsoft.AspNetCore.Mvc;
using MinimalBankSystem.Api.Errors;
using MinimalBankSystem.Api.Filters;
using MinimalBankSystem.Api.Middleware;
using MinimalBankSystem.Api.Testing;

namespace MinimalBankSystem.Api;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
        });
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);

        builder.Services.Configure<ApiBehaviorOptions>(options =>
        {
            options.SuppressModelStateInvalidFilter = true;
        });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IApiErrorMapper, DefaultApiErrorMapper>();
        builder.Services.AddControllers(options =>
        {
            options.Filters.Add<ApiModelStateFilter>();
        });

        WebApplication app = builder.Build();

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ApiErrorHandlingMiddleware>();
        app.MapControllers();

        // Test-only contract verification routes. They are not a product API surface.
        if (app.Environment.IsEnvironment(RuntimeContractTestEnvironment.Name))
        {
            app.MapGet("/test/runtime", (HttpContext context, TimeProvider timeProvider) =>
                Results.Json(new
                {
                    correlationId = context.GetCorrelationId(),
                    serverTimeUtc = timeProvider.GetUtcNow(),
                }));

            app.MapGet("/test/unmapped", (HttpContext _) =>
                throw new InvalidOperationException("Simulated unmapped exception for runtime contract verification."));

            app.MapGet("/test/conflict", (HttpContext _) =>
                throw new ApiException(StatusCodes.Status409Conflict, "concurrent_operation_conflict", "競合により安全に処理できません。"));
        }

        app.Run();
    }
}
