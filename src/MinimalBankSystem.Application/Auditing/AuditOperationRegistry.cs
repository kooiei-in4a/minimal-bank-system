using MinimalBankSystem.Domain.Auditing;

namespace MinimalBankSystem.Application.Auditing;

/// <summary>A feature-owned Product Audit operation allowlist entry.</summary>
public sealed class AuditOperationRegistration
{
    public AuditOperationRegistration(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        if (identifier.Length > AuditRecord.OperationIdentifierMaxLength ||
            !string.Equals(identifier, identifier.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(identifier));
        }

        Identifier = identifier;
    }

    public string Identifier { get; }
}

public interface IAuditOperationRegistry
{
    void EnsureRegistered(string operationIdentifier);
}

/// <summary>
/// Immutable fail-closed registry. AUD-01 registers no feature operation itself; later feature
/// leaves contribute explicit <see cref="AuditOperationRegistration"/> instances through DI.
/// </summary>
public sealed class AuditOperationRegistry(
    IEnumerable<AuditOperationRegistration> registrations) : IAuditOperationRegistry
{
    private readonly HashSet<string> registered = BuildRegistry(registrations);

    public void EnsureRegistered(string operationIdentifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationIdentifier);

        if (!registered.Contains(operationIdentifier))
        {
            throw new UnregisteredAuditOperationException(operationIdentifier);
        }
    }

    private static HashSet<string> BuildRegistry(IEnumerable<AuditOperationRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        HashSet<string> result = new(StringComparer.Ordinal);
        foreach (AuditOperationRegistration registration in registrations)
        {
            if (!result.Add(registration.Identifier))
            {
                throw new InvalidOperationException(
                    $"Product Audit operation '{registration.Identifier}' is registered more than once.");
            }
        }

        return result;
    }
}

public sealed class UnregisteredAuditOperationException(string operationIdentifier)
    : InvalidOperationException(
        $"Product Audit operation '{operationIdentifier}' is not registered in the allowlist.");
