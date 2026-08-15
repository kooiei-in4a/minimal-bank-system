namespace MinimalBankSystem.Api.Authorization;

/// <summary>
/// Feature-owned endpoint metadata for policy-rejection Product Audit. AUTHZ owns consuming this
/// metadata, but each feature leaf owns its operation identifier and target semantics.
/// </summary>
public interface IAuthorizationAuditContext
{
    string OperationIdentifier { get; }

    ValueTask<string?> ResolveTargetIdentifierAsync(HttpContext httpContext);
}
