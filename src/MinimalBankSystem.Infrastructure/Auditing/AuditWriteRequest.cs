using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Infrastructure.Auditing;

/// <summary>
/// Narrow Product Audit input. It intentionally accepts only approved logical fields and has no
/// arbitrary payload or metadata bag.
/// </summary>
public sealed record AuditWriteRequest
{
    private AuditWriteRequest(
        Guid actorIdentifier,
        OperatorRole actorRole,
        string operationIdentifier,
        string targetIdentifier,
        AuditResult result,
        string? failureBusinessErrorCode,
        string correlationId)
    {
        ActorIdentifier = actorIdentifier;
        ActorRole = actorRole;
        OperationIdentifier = operationIdentifier;
        TargetIdentifier = targetIdentifier;
        Result = result;
        FailureBusinessErrorCode = failureBusinessErrorCode;
        CorrelationId = correlationId;
    }

    public Guid ActorIdentifier { get; }

    public OperatorRole ActorRole { get; }

    public string OperationIdentifier { get; }

    public string TargetIdentifier { get; }

    public AuditResult Result { get; }

    public string? FailureBusinessErrorCode { get; }

    public string CorrelationId { get; }

    public static AuditWriteRequest Success(
        Guid actorIdentifier,
        OperatorRole actorRole,
        string operationIdentifier,
        string targetIdentifier,
        string correlationId) =>
        new(
            actorIdentifier,
            actorRole,
            operationIdentifier,
            targetIdentifier,
            AuditResult.Success,
            failureBusinessErrorCode: null,
            correlationId);

    public static AuditWriteRequest Failure(
        Guid actorIdentifier,
        OperatorRole actorRole,
        string operationIdentifier,
        string targetIdentifier,
        string failureBusinessErrorCode,
        string correlationId) =>
        new(
            actorIdentifier,
            actorRole,
            operationIdentifier,
            targetIdentifier,
            AuditResult.Failure,
            failureBusinessErrorCode,
            correlationId);
}
