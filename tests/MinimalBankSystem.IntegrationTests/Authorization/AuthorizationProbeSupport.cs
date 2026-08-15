extern alias api;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Api.Runtime.Authorization;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.IntegrationTests.Authorization;

/// <summary>
/// AUTHZ owns a test-host-only authorization verification surface that proves the deny-by-default
/// fallback policy, the current-Operator-state gate and the current-role policies without touching
/// a real business endpoint. The controller is never registered by production <c>Program.cs</c> —
/// it is added as an application part only by <see cref="AuthorizationProbeApiFactory"/>.
/// </summary>
[ApiController]
public sealed class AuthzFeatureController : ControllerBase
{
    public const string DefaultDenyPath = "/__authz/feature/default-deny";
    public const string AdministratorOnlyPath = "/__authz/feature/administrator-only";
    public const string TellerOrAdministratorPath = "/__authz/feature/teller-or-administrator";
    public const string AnonymousPath = "/__authz/feature/anonymous";

    public const string VerificationOperationIdentifier = "verification.operator.authorization";
    public const string VerificationTargetIdentifier = "operator:verification-target";

    [HttpGet(DefaultDenyPath)]
    public IActionResult DefaultDeny() => Ok(new AuthzFeatureResponse(HandlerReached: true));

    [Authorize(Policy = AuthorizationPolicies.AdministratorOnly)]
    [PolicyRejectionAudit(VerificationOperationIdentifier, VerificationTargetIdentifier)]
    [HttpGet(AdministratorOnlyPath)]
    public IActionResult AdministratorOnly() => Ok(new AuthzFeatureResponse(HandlerReached: true));

    [Authorize(Policy = AuthorizationPolicies.TellerOrAdministrator)]
    [PolicyRejectionAudit(VerificationOperationIdentifier, VerificationTargetIdentifier)]
    [HttpGet(TellerOrAdministratorPath)]
    public IActionResult TellerOrAdministrator() => Ok(new AuthzFeatureResponse(HandlerReached: true));

    [AllowAnonymous]
    [HttpGet(AnonymousPath)]
    public IActionResult Anonymous() => Ok(new AuthzFeatureResponse(HandlerReached: true));
}

public sealed record AuthzFeatureResponse(bool HandlerReached);

internal sealed class AuthorizationProbeApiFactory(
    string signingKey,
    string? connectionString = null,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<api::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(JwtAuthnOptions.SigningKeyConfigurationKey, signingKey);

        if (connectionString is not null)
        {
            builder.UseSetting(
                $"ConnectionStrings:{BankPersistence.ConnectionStringName}",
                connectionString);
        }

        builder.ConfigureServices(services =>
        {
            services
                .AddControllers()
                .AddApplicationPart(typeof(AuthzFeatureController).Assembly);
            services.AddSingleton(
                new AuditOperationRegistration(AuthzFeatureController.VerificationOperationIdentifier));

            configureServices?.Invoke(services);
        });
    }
}
