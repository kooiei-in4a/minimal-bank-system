using System.Collections.Frozen;
using Microsoft.AspNetCore.Authorization;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Api.Runtime.Authorization;

/// <summary>
/// Requires the current persisted Operator role (never a JWT role claim) to be one of the
/// allowed product roles.
/// </summary>
public sealed class OperatorRoleRequirement : IAuthorizationRequirement
{
    public OperatorRoleRequirement(IEnumerable<OperatorRole> allowedRoles)
    {
        ArgumentNullException.ThrowIfNull(allowedRoles);

        OperatorRole[] distinctRoles = allowedRoles
            .Where(role => role is not OperatorRole.Unspecified)
            .Distinct()
            .ToArray();

        if (distinctRoles.Length == 0)
        {
            throw new ArgumentException(
                "At least one product role is required.",
                nameof(allowedRoles));
        }

        AllowedRoles = distinctRoles.ToFrozenSet();
    }

    public FrozenSet<OperatorRole> AllowedRoles { get; }
}
