using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Application.Auditing;

/// <summary>
/// The bounded logical Product Audit input. It intentionally accepts no arbitrary payload,
/// credential, token, personal name, email address or diagnostic-data bag.
/// </summary>
public sealed record AuditWriteRequest(
    Guid ActorIdentifier,
    OperatorRole ActorRole,
    string OperationIdentifier,
    string TargetIdentifier,
    AuditResult Result,
    string? FailureBusinessErrorCode,
    string CorrelationId);
