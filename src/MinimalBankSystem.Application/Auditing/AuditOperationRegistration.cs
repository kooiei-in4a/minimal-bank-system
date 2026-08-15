using MinimalBankSystem.Domain.Auditing;

namespace MinimalBankSystem.Application.Auditing;

/// <summary>
/// An explicit feature-owned Product Audit operation contribution. AUD-01 contributes no
/// concrete production operation; later feature leaves register their own immutable values.
/// </summary>
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
