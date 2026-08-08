using Microsoft.AspNetCore.Mvc;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Runtime;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
    options.UseUtcTimestamp = true;
});

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = _ =>
            new BadRequestObjectResult(ApiErrorEnvelope.ValidationFailed);
    });
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<ApplicationTime>();

WebApplication app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ApiExceptionMiddleware>();
app.MapControllers();

app.Run();

public partial class Program;
