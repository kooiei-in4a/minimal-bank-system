namespace MinimalBankSystem.Application.Auditing;

/// <summary>Raised before persistence when an operation is absent from the explicit allowlist.</summary>
public sealed class UnregisteredAuditOperationException(string? operationIdentifier)
    : InvalidOperationException(
        $"The Product Audit operation identifier '{operationIdentifier ?? "(null)"}' is not registered.");
