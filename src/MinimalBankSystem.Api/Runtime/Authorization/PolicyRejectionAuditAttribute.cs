using Microsoft.AspNetCore.Mvc.Filters;

namespace MinimalBankSystem.Api.Runtime.Authorization;

/// <summary>
/// Feature-owned endpoint metadata that names the Product Audit operation and target for a
/// policy-rejection audit record. Values are static feature contracts, never request input.
/// The owning feature must register the operation identifier with the Audit operation registry.
/// Implements <see cref="IFilterMetadata"/> so MVC propagates it into the endpoint metadata that
/// the authorization result handler reads when it composes the rejection Audit record.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PolicyRejectionAuditAttribute(
    string operationIdentifier,
    string targetIdentifier) : Attribute, IFilterMetadata
{
    public string OperationIdentifier { get; } = operationIdentifier;

    public string TargetIdentifier { get; } = targetIdentifier;
}
