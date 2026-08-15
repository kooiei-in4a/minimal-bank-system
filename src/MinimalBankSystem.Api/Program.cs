using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Application.Runtime;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Authorization;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Auditing;

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
// Feature leaves contribute explicit AuditOperationRegistration instances through DI. AUD-01
// contributes none, so production is empty and fail-closed until an owning feature registers one.
builder.Services.AddSingleton<IAuditOperationRegistry, AuditOperationRegistry>();
builder.Services.AddScoped<IAuditWriter, PostgreSqlAuditWriter>();

builder.Services
    .AddOptions<JwtAuthnOptions>()
    .BindConfiguration(JwtAuthnOptions.SectionName)
    .Validate(
        options => options.TryGetSigningKeyBytes(out _),
        "An external JWT signing key must be configured as a base64 secret or secret file.")
    .Validate(
        options => options.AccessTokenLifetimeSeconds is > 0 and <= 900,
        "The JWT access-token lifetime must be between 1 and 900 seconds.")
    .ValidateOnStart();

builder.Services.AddScoped<AuthnLoginService>();
builder.Services.AddSingleton<IJwtAccessTokenIssuer, JwtAccessTokenIssuer>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtAuthnOptions>>((options, jwtOptions) =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = jwtOptions.Value.CreateValidationParameters();
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(
                    ApiErrorEnvelope.AuthenticationRequired,
                    context.HttpContext.RequestAborted);
            },
        };
    });
// AUTHZ (#168) request-time authority. The handler resolves the current Operator from persisted
// database state (never the JWT role claim); the result handler maps its state-invalid failures
// to 401 and its role-insufficient failures to an audited 403.
builder.Services.AddScoped<IAuthorizationHandler, CurrentOperatorAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, CurrentOperatorAuthorizationResultHandler>();
builder.Services.AddAuthorization(options =>
{
    // Default-deny fallback: any endpoint with no explicit authorization metadata requires a
    // current, active Operator whose authorization-state version matches the bearer token. Login
    // and the health endpoints are explicitly anonymous below. ASP.NET Core also runs the
    // fallback policy for requests that match no endpoint at all, so
    // CurrentOperatorAuthorizationHandler explicitly defers (succeeds trivially) whenever no
    // endpoint was matched, preserving the framework's own 404/405 contract. A bare
    // RequireAuthenticatedUser() requirement is intentionally omitted: missing/invalid-bearer 401
    // is already decided independently of policy requirements (by whether authentication itself
    // succeeded), and this handler's own claim/state checks already fail closed for an
    // unauthenticated principal on a real endpoint.
    options.FallbackPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
        .AddRequirements(CurrentOperatorAuthorizationRequirement.AnyCurrentOperator)
        .Build();
});

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
app.UseAuthentication();
app.UseAuthorization();
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
// Explicitly anonymous per the AUTHZ (#168) authorization default: health checks never require a
// current Operator.
app.MapHealthChecks(HealthContract.LivePath, HealthContract.Liveness)
    .WithMetadata(new HttpMethodMetadata([HttpMethods.Get]))
    .AllowAnonymous();
app.MapHealthChecks(HealthContract.ReadyPath, HealthContract.Readiness)
    .WithMetadata(new HttpMethodMetadata([HttpMethods.Get]))
    .AllowAnonymous();
app.MapControllers();

app.Run();

public partial class Program;
