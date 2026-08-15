using MinimalBankSystem.Domain.Auditing;

namespace MinimalBankSystem.Application.Auditing;

/// <summary>Immutable operation allowlist composed explicitly by the production or test host.</summary>
public sealed class AuditOperationRegistry : IAuditOperationRegistry
{
    private readonly HashSet<string> registeredOperations;

    public AuditOperationRegistry(IEnumerable<string> operationIdentifiers)
    {
        ArgumentNullException.ThrowIfNull(operationIdentifiers);

        registeredOperations = new HashSet<string>(StringComparer.Ordinal);

        foreach (string identifier in operationIdentifiers)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
            string normalized = identifier.Trim();

            if (normalized.Length > AuditRecord.OperationIdentifierMaxLength)
            {
                throw new ArgumentOutOfRangeException(nameof(operationIdentifiers));
            }

            if (!registeredOperations.Add(normalized))
            {
                throw new ArgumentException(
                    $"The Audit operation identifier '{normalized}' was registered more than once.",
                    nameof(operationIdentifiers));
            }
        }
    }

    public static AuditOperationRegistry Empty { get; } = new([]);

    public void EnsureRegistered(string operationIdentifier)
    {
        if (string.IsNullOrWhiteSpace(operationIdentifier) ||
            !registeredOperations.Contains(operationIdentifier.Trim()))
        {
            throw new UnregisteredAuditOperationException(operationIdentifier);
        }
    }
}
