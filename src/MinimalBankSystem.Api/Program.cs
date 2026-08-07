using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Runtime;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

builder.Services.AddControllers();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<ApplicationClock>();

WebApplication app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ApiExceptionHandlingMiddleware>();
app.MapControllers();

app.Run();

public partial class Program
{
}
