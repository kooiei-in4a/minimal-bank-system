using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Runtime;
using MinimalBankSystem.Infrastructure.Persistence;

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

// Registration only: DI construction of BankDbContext never applies migrations or creates
// schema. Schema evolution runs exclusively through MinimalBankSystem.Migrator (ADR-0009).
builder.Services.AddDbContext<BankDbContext>(options => options.UseBankPostgreSql(
    builder.Configuration.GetConnectionString("Database")
        ?? throw new InvalidOperationException(
            $"The '{BankDbContextConnectionStringResolver.ConnectionStringConfigurationKey}' " +
            "connection string is not configured.")));

WebApplication app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseStatusCodePages(async statusCodeContext =>
{
    HttpContext context = statusCodeContext.HttpContext;
    ApiErrorMapping? mapping = ApiErrorMapping.FromFrameworkStatusCode(context.Response.StatusCode);

    if (mapping is not null)
    {
        await context.Response.WriteAsJsonAsync(
            new ApiErrorEnvelope(mapping.Code, mapping.Message),
            context.RequestAborted);
    }
});
app.MapControllers();

app.Run();

public partial class Program;
