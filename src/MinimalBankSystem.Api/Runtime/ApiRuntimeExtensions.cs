using MinimalBankSystem.Application.Time;

namespace MinimalBankSystem.Api.Runtime;

public static class ApiRuntimeExtensions
{
    public static WebApplicationBuilder AddApiRuntime(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ApplicationTime>();
        builder.Services.AddSingleton<ExceptionHttpMapperRegistry>(serviceProvider =>
            new ExceptionHttpMapperRegistry(serviceProvider.GetServices<IExceptionHttpMapper>()));

        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddProblemDetails();

        builder.Logging.AddProhibitedFieldSanitizingJsonConsole();

        return builder;
    }

    public static WebApplication UseApiRuntime(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<CorrelationIdMiddleware>();

        if (app.Environment.IsDevelopment()
            || string.Equals(app.Environment.EnvironmentName, "Testing", StringComparison.Ordinal))
        {
            app.MapContractVerificationEndpoints();
        }

        return app;
    }
}
