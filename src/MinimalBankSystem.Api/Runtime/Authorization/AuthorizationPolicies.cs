using Microsoft.AspNetCore.Authorization;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Api.Runtime.Authorization;

/// <summary>
/// The AUTHZ-01 authorization policies. The fallback policy makes every endpoint that carries no
/// explicit authorization metadata deny-by-default for anonymous and stale-authentication
/// requests, while explicitly anonymous endpoints opt out with <c>[AllowAnonymous]</c>.
/// </summary>
public static class AuthorizationPolicies
{
    public const string AdministratorOnly = "AdministratorOnly";

    public const string TellerOrAdministrator = "TellerOrAdministrator";

    public static AuthorizationPolicy Fallback { get; } =
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new CurrentOperatorRequirement())
            .Build();

    public static AuthorizationPolicy CreateRolePolicy(params OperatorRole[] allowedRoles) =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(
                new CurrentOperatorRequirement(),
                new OperatorRoleRequirement(allowedRoles))
            .Build();
}
