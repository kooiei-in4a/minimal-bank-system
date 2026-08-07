using System.Text.Json;
using MinimalBankSystem.Api.CorrelationId;
using MinimalBankSystem.Api.ErrorHandling;
using MinimalBankSystem.Api.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";
    options.JsonWriterOptions = new JsonWriterOptions { Indented = false };
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
builder.Services.AddSingleton<IExceptionMapper, DefaultExceptionMapper>();
builder.Services.AddControllers();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();
app.MapControllers();

app.Run();

public partial class Program { }
