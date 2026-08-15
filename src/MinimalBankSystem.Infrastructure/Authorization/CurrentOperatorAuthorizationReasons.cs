namespace MinimalBankSystem.Infrastructure.Authorization;

/// <summary>
/// Well-known <see cref="Microsoft.AspNetCore.Authorization.AuthorizationFailureReason"/> markers
/// that let the AUTHZ result handler choose between the 401 (state-invalid) and 403
/// (role-insufficient) mapping for an authenticated request. Values are process-internal contracts
/// between <see cref="CurrentOperatorAuthorizationHandler"/> and its result handler, never
/// serialized to a client.
/// </summary>
public static class CurrentOperatorAuthorizationReasons
{
    /// <summary>
    /// The bearer token authenticated, but the current Operator could not be resolved as a valid
    /// authorization actor: missing/unparseable claims, no matching Operator, a disabled Operator,
    /// or an authorization-state-version mismatch. Mapped to 401, matching ADR-0007's rule that a
    /// no-longer-valid presented authentication state is an authentication failure, not a
    /// permission failure.
    /// </summary>
    public const string OperatorStateInvalid = "authz.current-operator-state-invalid";

    /// <summary>
    /// The current Operator is active and authorization-state-current, but its current database
    /// role is not one of the policy's allowed roles. Mapped to 403 with exactly-once, fail-closed
    /// Product Audit.
    /// </summary>
    public const string OperatorRoleInsufficient = "authz.current-operator-role-insufficient";
}
