namespace MinimalBankSystem.Application.Auditing;

/// <summary>Fail-closed allowlist for feature-owned Product Audit operation identifiers.</summary>
public interface IAuditOperationRegistry
{
    void EnsureRegistered(string operationIdentifier);
}
