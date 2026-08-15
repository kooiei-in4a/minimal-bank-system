using Microsoft.AspNetCore.Authorization;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Infrastructure.Authorization;

/// <summary>
/// AUTHZ (#168) fallback/policy requirement. An empty <see cref="AllowedRoles"/> set means "any
/// current, active Operator whose authorization-state version matches the bearer token"; a
/// non-empty set additionally requires the Operator's <em>current database</em> role to be one of
/// the fixed allowed values. The JWT role claim is never consulted by this requirement or its
/// handler.
/// </summary>
public sealed class CurrentOperatorAuthorizationRequirement : IAuthorizationRequirement
{
    /// <summary>The fallback requirement shared by every endpoint with no explicit policy.</summary>
    public static CurrentOperatorAuthorizationRequirement AnyCurrentOperator { get; } = new();

    public CurrentOperatorAuthorizationRequirement(params OperatorRole[] allowedRoles)
    {
        ArgumentNullException.ThrowIfNull(allowedRoles);
        AllowedRoles = new HashSet<OperatorRole>(allowedRoles);
    }

    public IReadOnlySet<OperatorRole> AllowedRoles { get; }
}
