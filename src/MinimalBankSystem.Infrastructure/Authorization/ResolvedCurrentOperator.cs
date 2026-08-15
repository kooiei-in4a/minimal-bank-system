using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Infrastructure.Authorization;

/// <summary>
/// The current Operator identity/role snapshot resolved by
/// <see cref="CurrentOperatorAuthorizationHandler"/> for an authenticated, state-valid request.
/// Stashed on <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/> under
/// <see cref="CurrentOperatorAuthorizationContextKeys.ResolvedOperator"/> only when a role check
/// fails, so the result handler can build the exactly-once policy-rejection Product Audit without
/// a second database lookup.
/// </summary>
public sealed record ResolvedCurrentOperator(Guid OperatorId, OperatorRole Role);

/// <summary>Well-known <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/> keys for AUTHZ.</summary>
public static class CurrentOperatorAuthorizationContextKeys
{
    /// <summary>Keys the <see cref="ResolvedCurrentOperator"/> stashed for policy-rejection Audit.</summary>
    public static readonly object ResolvedOperator = new();
}
