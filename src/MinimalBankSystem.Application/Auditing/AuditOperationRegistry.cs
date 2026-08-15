using System.Collections.Frozen;

namespace MinimalBankSystem.Application.Auditing;

/// <summary>
/// Immutable fail-closed registry built only from explicit DI contributions. Configuration,
/// environment variables and request input are not registration sources.
/// </summary>
public sealed class AuditOperationRegistry : IAuditOperationRegistry
{
    private readonly FrozenSet<string> registeredOperations;

    public AuditOperationRegistry(IEnumerable<AuditOperationRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        HashSet<string> registered = new(StringComparer.Ordinal);
        foreach (AuditOperationRegistration registration in registrations)
        {
            ArgumentNullException.ThrowIfNull(registration);

            if (!registered.Add(registration.Identifier))
            {
                throw new InvalidOperationException(
                    $"Product Audit operation '{registration.Identifier}' is registered more than once.");
            }
        }

        registeredOperations = registered.ToFrozenSet(StringComparer.Ordinal);
    }

    public void EnsureRegistered(string operationIdentifier)
    {
        if (string.IsNullOrWhiteSpace(operationIdentifier) ||
            !registeredOperations.Contains(operationIdentifier))
        {
            throw new UnregisteredAuditOperationException(operationIdentifier);
        }
    }
}
