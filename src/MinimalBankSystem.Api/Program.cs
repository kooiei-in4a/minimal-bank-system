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

// ADR-0009 / Issue #42 section 8.7: the API may resolve the application DbContext, but normal
// startup never evolves the schema. There is no Migrate, MigrateAsync, EnsureCreated or ad-hoc DDL
// on this path; schema changes belong to the explicit MinimalBankSystem.Migrator process. The
// options factory below runs on first DbContext resolution, not during startup, so a host without
// a configured database still starts and simply cannot use persistence.
builder.Services.AddDbContext<BankDbContext>((serviceProvider, options) =>
{
    IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
    string? connectionString = configuration.GetConnectionString(BankPersistence.ConnectionStringName);

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            $"No PostgreSQL connection string was configured. Set '{BankPersistence.ConnectionStringEnvironmentVariable}' " +
            $"(configuration key 'ConnectionStrings:{BankPersistence.ConnectionStringName}'). " +
            "The API never falls back to another provider.");
    }

    options.UseBankPostgreSql(connectionString);
});

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
