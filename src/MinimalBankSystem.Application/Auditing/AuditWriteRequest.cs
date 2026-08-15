using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Application.Auditing;

/// <summary>
/// Fixed Product Audit input shape. It intentionally exposes no arbitrary payload, credential,
/// secret, bearer-token, JWT-token or personal-data field.
/// </summary>
public sealed record AuditWriteRequest(
    Guid ActorIdentifier,
    OperatorRole ActorRole,
    string OperationIdentifier,
    string TargetIdentifier,
    AuditResult Result,
    string? FailureBusinessErrorCode,
    string CorrelationId);
