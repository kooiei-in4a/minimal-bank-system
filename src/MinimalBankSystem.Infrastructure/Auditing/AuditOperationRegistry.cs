using MinimalBankSystem.Domain.Auditing;

namespace MinimalBankSystem.Infrastructure.Auditing;

/// <summary>Immutable allowlist of feature-owned Product Audit operation identifiers.</summary>
public sealed class AuditOperationRegistry
{
    private readonly HashSet<string> operations;

    public AuditOperationRegistry(IEnumerable<string> operationIdentifiers)
    {
        ArgumentNullException.ThrowIfNull(operationIdentifiers);
        operations = new HashSet<string>(operationIdentifiers, StringComparer.Ordinal);

        foreach (string operation in operations)
        {
            ValidateOperationIdentifier(operation);
        }
    }

    public int Count => operations.Count;

    public bool IsRegistered(string operationIdentifier) =>
        operations.Contains(operationIdentifier);

    internal void EnsureRegistered(string operationIdentifier)
    {
        if (!IsRegistered(operationIdentifier))
        {
            throw new UnregisteredAuditOperationException(operationIdentifier);
        }
    }

    internal static void ValidateOperationIdentifier(string operationIdentifier)
    {
        _ = AuditRecord.ValidateOperationIdentifier(operationIdentifier);
    }
}

public sealed class UnregisteredAuditOperationException(string operationIdentifier)
    : InvalidOperationException($"Product Audit operation '{operationIdentifier}' is not registered.");
