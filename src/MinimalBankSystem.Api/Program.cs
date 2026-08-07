using MinimalBankSystem.Api.Runtime;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "O";
    options.UseUtcTimestamp = true;
});

builder.Services.AddApiRuntimeContract();

WebApplication app = builder.Build();

app.UseApiRuntimeContract();
app.MapControllers();
app.MapApiContractProbes();

app.Run();

public partial class Program;
