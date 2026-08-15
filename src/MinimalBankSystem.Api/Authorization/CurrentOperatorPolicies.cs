using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Api.Authorization;

public static class CurrentOperatorPolicyNames
{
    public const string Administrator = "current-operator:administrator";
    public const string AdministratorOrTeller = "current-operator:administrator-or-teller";
}

internal static class CurrentOperatorPolicies
{
    public static AuthorizationPolicy CreateFallbackPolicy() =>
        CreateBuilder()
            .AddRequirements(CurrentOperatorRequirement.Instance)
            .Build();

    public static AuthorizationPolicy CreateRolePolicy(params OperatorRole[] permittedRoles) =>
        CreateBuilder()
            .AddRequirements(
                CurrentOperatorRequirement.Instance,
                new CurrentOperatorRoleRequirement(permittedRoles))
            .Build();

    private static AuthorizationPolicyBuilder CreateBuilder() =>
        new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser();
}

internal sealed class CurrentOperatorRequirement : IAuthorizationRequirement
{
    public static CurrentOperatorRequirement Instance { get; } = new();

    private CurrentOperatorRequirement()
    {
    }
}

internal sealed class CurrentOperatorRoleRequirement : IAuthorizationRequirement
{
    public CurrentOperatorRoleRequirement(IEnumerable<OperatorRole> permittedRoles)
    {
        ArgumentNullException.ThrowIfNull(permittedRoles);

        OperatorRole[] roles = permittedRoles.Distinct().ToArray();
        if (roles.Length == 0 || roles.Any(role =>
                role is not (OperatorRole.Administrator or OperatorRole.Teller or OperatorRole.Viewer)))
        {
            throw new ArgumentOutOfRangeException(nameof(permittedRoles));
        }

        PermittedRoles = roles;
    }

    public IReadOnlyCollection<OperatorRole> PermittedRoles { get; }
}
