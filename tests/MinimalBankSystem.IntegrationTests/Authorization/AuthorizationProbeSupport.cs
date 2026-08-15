extern alias api;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.IntegrationTests.Authorization;

/// <summary>
/// AUTHZ verification endpoints exist only as integration-test application parts. Production
/// Program never registers this assembly, these routes, or the test Audit operation.
/// </summary>
[ApiController]
public sealed class AuthorizationProbeController(AuthorizationProbeSignals signals) : ControllerBase
{
    public const string FallbackRoute = "/__authz-probe/fallback/{targetId}";
    public const string AdministratorRoute = "/__authz-probe/administrator/{targetId}";
    public const string MissingAuditContextRoute = "/__authz-probe/missing-audit-context/{targetId}";

    [HttpGet(FallbackRoute)]
    public IActionResult Fallback(string targetId)
    {
        _ = targetId;
        signals.RecordFallbackHandlerReached();
        return Ok(new { HandlerReached = true });
    }

    [Authorize(Policy = CurrentOperatorPolicyNames.Administrator)]
    [TestAuthorizationAuditContext("targetId")]
    [HttpGet(AdministratorRoute)]
    public IActionResult Administrator(string targetId)
    {
        _ = targetId;
        signals.RecordAdministratorHandlerReached();
        return Ok(new { HandlerReached = true });
    }

    [Authorize(Policy = CurrentOperatorPolicyNames.Administrator)]
    [HttpGet(MissingAuditContextRoute)]
    public IActionResult MissingAuditContext(string targetId)
    {
        _ = targetId;
        signals.RecordMissingAuditContextHandlerReached();
        return Ok(new { HandlerReached = true });
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
internal sealed class TestAuthorizationAuditContextAttribute(string targetRouteValueName)
    : Attribute, IAuthorizationAuditContext
{
    public const string Operation = "test.authz.administrator-probe";

    public string OperationIdentifier => Operation;

    public ValueTask<string?> ResolveTargetIdentifierAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return ValueTask.FromResult(
            Convert.ToString(
                httpContext.Request.RouteValues[targetRouteValueName],
                System.Globalization.CultureInfo.InvariantCulture));
    }
}

public sealed class AuthorizationProbeSignals
{
    private int fallbackHandlerReachCount;
    private int administratorHandlerReachCount;
    private int missingAuditContextHandlerReachCount;

    public int FallbackHandlerReachCount => Volatile.Read(ref fallbackHandlerReachCount);

    public int AdministratorHandlerReachCount => Volatile.Read(ref administratorHandlerReachCount);

    public int MissingAuditContextHandlerReachCount => Volatile.Read(ref missingAuditContextHandlerReachCount);

    public void RecordFallbackHandlerReached() => Interlocked.Increment(ref fallbackHandlerReachCount);

    public void RecordAdministratorHandlerReached() => Interlocked.Increment(ref administratorHandlerReachCount);

    public void RecordMissingAuditContextHandlerReached() =>
        Interlocked.Increment(ref missingAuditContextHandlerReachCount);
}

internal sealed class AuthorizationProbeApiFactory(
    string connectionString,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<api::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(JwtAuthnOptions.SigningKeyConfigurationKey, TestJwtConfiguration.SigningKey);
        builder.UseSetting(
            $"ConnectionStrings:{BankPersistence.ConnectionStringName}",
            connectionString);

        builder.ConfigureServices(services =>
        {
            services
                .AddControllers()
                .AddApplicationPart(typeof(AuthorizationProbeController).Assembly);
            services.AddSingleton<AuthorizationProbeSignals>();
            services.AddSingleton(new AuditOperationRegistration(TestAuthorizationAuditContextAttribute.Operation));
            configureServices?.Invoke(services);
        });
    }
}
