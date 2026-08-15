using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Api.Runtime.Authorization;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Application.Runtime;
using MinimalBankSystem.Domain.Identity;
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
builder.Services.AddScoped<CurrentOperatorResolver>();
builder.Services.AddScoped<CurrentOperatorContext>();
builder.Services.AddScoped<IAuthorizationHandler, CurrentOperatorAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, OperatorAuthorizationResultHandler>();
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
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(AuthorizationPolicies.Fallback)
    .AddPolicy(
        AuthorizationPolicies.AdministratorOnly,
        AuthorizationPolicies.CreateRolePolicy(OperatorRole.Administrator))
    .AddPolicy(
        AuthorizationPolicies.TellerOrAdministrator,
        AuthorizationPolicies.CreateRolePolicy(OperatorRole.Teller, OperatorRole.Administrator));
// The fallback must not convert framework routing errors (404/405/415) into 401 for requests
// that no endpoint matched. The evaluator bypasses only that case and delegates everything else.
builder.Services.AddSingleton<IPolicyEvaluator>(serviceProvider =>
    new RoutingFaultAwarePolicyEvaluator(
        ActivatorUtilities.CreateInstance<PolicyEvaluator>(serviceProvider)));

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
app.MapHealthChecks(HealthContract.LivePath, HealthContract.Liveness)
    .AllowAnonymous()
    .WithMetadata(new HttpMethodMetadata([HttpMethods.Get]));
app.MapHealthChecks(HealthContract.ReadyPath, HealthContract.Readiness)
    .AllowAnonymous()
    .WithMetadata(new HttpMethodMetadata([HttpMethods.Get]));
app.MapControllers();

app.Run();

public partial class Program;
