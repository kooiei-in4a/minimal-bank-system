using MinimalBankSystem.Api.Correlation;
using MinimalBankSystem.Api.ErrorHandling;
using MinimalBankSystem.Api.Logging;
using MinimalBankSystem.Application.Diagnostics;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddTechnicalJsonConsoleLogging();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CurrentTimeReader>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers();

WebApplication app = builder.Build();

// Correlation ID must wrap exception handling (not the other way around) so
// that an unmapped exception is still logged inside the request's
// correlation scope instead of after it has already been disposed.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.MapControllers();

app.Run();

public partial class Program
{
}
