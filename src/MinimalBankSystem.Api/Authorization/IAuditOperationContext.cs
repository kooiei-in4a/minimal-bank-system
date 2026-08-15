namespace MinimalBankSystem.Api.Authorization;

/// <summary>
/// Feature-leaf-owned Product Audit operation/target context for an authenticated policy
/// rejection produced by AUTHZ (#168). AUTHZ owns policy-rejection detection, Audit invocation
/// timing, the separate short transaction, exactly-once behavior and fail-closed behavior; it
/// never invents a production operation or target identifier of its own. Attach an implementation
/// as endpoint metadata — for example <see cref="AuditOperationContextAttribute"/> — on any
/// endpoint whose authorization policy can produce an authenticated 403. When an endpoint carries
/// no such metadata, AUTHZ fails closed with the existing internal-error contract rather than
/// return an unaudited 403.
/// </summary>
public interface IAuditOperationContext
{
    /// <summary>The registered, feature-leaf-owned Product Audit operation identifier.</summary>
    string OperationIdentifier { get; }

    /// <summary>Computes the operation-appropriate Product Audit target identifier for this request.</summary>
    string ResolveTargetIdentifier(HttpContext httpContext);
}

/// <summary>
/// Generic, reusable route-value-based <see cref="IAuditOperationContext"/> attachment. AUTHZ
/// supplies only this mechanism; the operation identifier and the route-value name it reads for
/// the target identifier are supplied by the attaching feature leaf.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AuditOperationContextAttribute(string operationIdentifier, string targetRouteValueName)
    : Attribute, IAuditOperationContext
{
    public string OperationIdentifier { get; } =
        string.IsNullOrWhiteSpace(operationIdentifier)
            ? throw new ArgumentException("An operation identifier is required.", nameof(operationIdentifier))
            : operationIdentifier;

    public string TargetRouteValueName { get; } =
        string.IsNullOrWhiteSpace(targetRouteValueName)
            ? throw new ArgumentException("A target route-value name is required.", nameof(targetRouteValueName))
            : targetRouteValueName;

    public string ResolveTargetIdentifier(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.Request.RouteValues.TryGetValue(TargetRouteValueName, out object? value) &&
            value is not null)
        {
            return value.ToString() ?? throw new InvalidOperationException(
                $"Route value '{TargetRouteValueName}' produced a null Product Audit target identifier.");
        }

        throw new InvalidOperationException(
            $"Route value '{TargetRouteValueName}' was not present for the Product Audit target identifier.");
    }
}
