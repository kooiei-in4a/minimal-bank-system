using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Application.Auditing;

/// <summary>
/// The bounded logical Product Audit input. It intentionally accepts no request payload,
/// credential, token, personal name, email address or other arbitrary diagnostic data.
/// </summary>
public sealed record AuditWriteRequest(
    Guid ActorIdentifier,
    OperatorRole ActorRole,
    string OperationIdentifier,
    string TargetIdentifier,
    AuditResult Result,
    string? FailureBusinessErrorCode,
    string CorrelationId);
