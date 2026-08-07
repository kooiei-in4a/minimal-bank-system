using MinimalBankSystem.Api.RuntimeContract;
using MinimalBankSystem.Application.Runtime;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
});

builder.Services.AddApiRuntimeContract();

WebApplication app = builder.Build();

app.UseMiddleware<ApiRequestContractMiddleware>();

app.MapGet(
    "/runtime-contract/ping",
    static (ApplicationClock clock) => Results.Ok(new RuntimeContractProbeResponse(clock.UtcNow)));

if (app.Environment.IsEnvironment("Testing"))
{
    app.MapGet(
        "/__contract/error",
        static () => Results.BadRequest(
            new ApiErrorResponse("validation_failed", "The request is invalid.")));

    app.MapGet(
        "/__contract/unmapped-exception",
        static () => UnmappedException());
}

app.Run();

static IResult UnmappedException()
{
    throw new InvalidOperationException("unmapped exception internal detail");
}

public partial class Program;
