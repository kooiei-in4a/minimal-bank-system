extern alias api;

using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Authorization;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.IntegrationTests.Authorization;

/// <summary>
/// AUTHZ (#168) owns no production feature endpoint, so it has no production operation/target to
/// audit. This test-host-only verification surface proves the real ASP.NET Core authorization
/// pipeline — default-deny fallback, current-Operator active/disabled resolution,
/// authorization-state-version comparison, current-DB-role policy authorization, the authenticated
/// 403 policy-rejection Product Audit and its fail-closed boundary — using a disposable,
/// feature-leaf-shaped verification operation. It is registered only by
/// <see cref="AuthorizationProbeApiFactory"/>, never by production <c>Program.cs</c>. It is a
/// distinct surface from the AUTHN authentication-only probe (#167,
/// <c>AuthenticationProbeSupport.cs</c>), which this file does not reuse or modify.
/// </summary>
[ApiController]
public sealed class AuthorizationProbeController : ControllerBase
{
    public const string AdministratorOnlyPath = "/__authz-probe/administrator-only/{targetId}";
    public const string AnyCurrentOperatorPath = "/__authz-probe/any-current-operator";
    public const string UnauditedRejectionPath = "/__authz-probe/unaudited-rejection";
    public const string NoExplicitPolicyPath = "/__authz-probe/no-explicit-policy";

    public const string AdministratorOnlyPolicy = "authz-probe.administrator-only";
    public const string AnyCurrentOperatorPolicy = "authz-probe.any-current-operator";
    public const string UnauditedRejectionPolicy = "authz-probe.unaudited-rejection";

    public const string OperationIdentifier = "verification.authz.administrator-only";

    [Authorize(Policy = AdministratorOnlyPolicy)]
    [AuditOperationContext(OperationIdentifier, "targetId")]
    [HttpGet(AdministratorOnlyPath)]
    public IActionResult AdministratorOnly(string targetId)
    {
        _ = targetId;
        return Ok(Reached());
    }

    [Authorize(Policy = AnyCurrentOperatorPolicy)]
    [HttpGet(AnyCurrentOperatorPath)]
    public IActionResult AnyCurrentOperator() => Ok(Reached());

    // Deliberately carries no IAuditOperationContext metadata, proving the AUTHZ fail-closed
    // boundary: a policy rejection here must never become an unaudited 403.
    [Authorize(Policy = UnauditedRejectionPolicy)]
    [HttpGet(UnauditedRejectionPath)]
    public IActionResult UnauditedRejection() => Ok(Reached());

    // Deliberately carries no [Authorize]/[AllowAnonymous] at all, proving default-deny is not an
    // opt-in per endpoint: the production FallbackPolicy alone protects it.
    [HttpGet(NoExplicitPolicyPath)]
    public IActionResult NoExplicitPolicy() => Ok(Reached());

    private AuthorizationProbeResponse Reached() =>
        new(HandlerReached: true, Subject: User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
}

public sealed record AuthorizationProbeResponse(bool HandlerReached, string? Subject);

/// <summary>Throws for every Product Audit write; proves AUTHZ's fail-closed boundary end to end.</summary>
internal sealed class ThrowingAuditWriter : IAuditWriter
{
    public Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default) =>
        throw new AuthorizationVerificationAuditInjectionException();

    public Task<TResult> AppendInSeparateTransactionBeforeResultAsync<TResult>(
        AuditWriteRequest request,
        Func<CancellationToken, Task<TResult>> successResultFactory,
        CancellationToken cancellationToken = default) =>
        throw new AuthorizationVerificationAuditInjectionException();
}

internal sealed class AuthorizationVerificationAuditInjectionException()
    : InvalidOperationException("Deterministic test-only AUTHZ policy-rejection Audit failure.");

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
                .AddApplicationPart(typeof(AuthorizationProbeController).Assembly);

            // Test composition only: registers the disposable verification operation identifier
            // AUTHZ never owns in production, exactly as AUD-01 registers none by default.
            services.AddSingleton(new AuditOperationRegistration(AuthorizationProbeController.OperationIdentifier));

            services.AddAuthorization(options =>
            {
                options.AddPolicy(
                    AuthorizationProbeController.AdministratorOnlyPolicy,
                    policy => policy.AddRequirements(
                        new CurrentOperatorAuthorizationRequirement(OperatorRole.Administrator)));
                options.AddPolicy(
                    AuthorizationProbeController.AnyCurrentOperatorPolicy,
                    policy => policy.AddRequirements(CurrentOperatorAuthorizationRequirement.AnyCurrentOperator));
                options.AddPolicy(
                    AuthorizationProbeController.UnauditedRejectionPolicy,
                    policy => policy.AddRequirements(
                        new CurrentOperatorAuthorizationRequirement(OperatorRole.Administrator)));
            });

            configureServices?.Invoke(services);
        });
    }

    /// <summary>Replaces the production Audit writer with one that always throws, in test composition only.</summary>
    internal static void UseThrowingAuditWriter(IServiceCollection services)
    {
        services.RemoveAll<IAuditWriter>();
        services.AddScoped<IAuditWriter, ThrowingAuditWriter>();
    }
}
