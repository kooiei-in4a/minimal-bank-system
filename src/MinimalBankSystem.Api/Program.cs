using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Runtime;
using MinimalBankSystem.Infrastructure.Authentication;
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
builder.Services.AddSingleton<JwtTokenIssuer>();

// WP2-AUTHN-01: JWT bearer authentication. Every control below is a required Acceptance
// Criterion (signature, issuer, audience, expiry, allowed algorithm) and must not be weakened.
// The signing key is resolved once at startup from an externally-injected value; it is never
// logged, defaulted, or committed.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            // Resolved lazily, only while validating a presented token's signature, so requests
            // without an Authorization header (health checks, unauthenticated endpoints) never
            // require the signing key to be configured.
            IssuerSigningKeyResolver = (_, _, _, _) => [JwtSigningKeyProvider.Resolve(builder.Configuration)],
            ValidateIssuer = true,
            ValidIssuer = JwtTokenSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = JwtTokenSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [JwtTokenSettings.SigningAlgorithm],
        };
    });
builder.Services.AddAuthorization();

// Normal API startup never evolves the schema. The options factory runs only when persistence is
// resolved and fails closed if the canonical PostgreSQL connection is absent.
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

// Operational liveness has no dependency checks. Readiness selects the PostgreSQL and migration
// check explicitly, keeping an unavailable database from affecting process liveness.
builder.Services
    .AddHealthChecks()
    .AddCheck<PostgreSqlReadinessHealthCheck>(
        HealthContract.ReadinessCheckName,
        failureStatus: HealthStatus.Unhealthy,
        tags: [HealthContract.ReadinessTag],
        timeout: HealthContract.ReadinessTimeout);

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
app.MapHealthChecks(HealthContract.LivePath, HealthContract.Liveness)
    .WithMetadata(new HttpMethodMetadata([HttpMethods.Get]));
app.MapHealthChecks(HealthContract.ReadyPath, HealthContract.Readiness)
    .WithMetadata(new HttpMethodMetadata([HttpMethods.Get]));
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
