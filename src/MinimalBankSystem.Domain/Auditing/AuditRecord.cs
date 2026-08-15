using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Domain.Auditing;

/// <summary>
/// Immutable Product Audit snapshot. The record deliberately has no navigation to the live
/// Operator so later approved role or state changes cannot rewrite historical actor data.
/// </summary>
public sealed class AuditRecord
{
    public const int OperationIdentifierMaxLength = 128;
    public const int TargetIdentifierMaxLength = 256;
    public const int BusinessErrorCodeMaxLength = 128;
    public const int CorrelationIdMaxLength = 64;

    private AuditRecord()
    {
        OperationIdentifier = null!;
        TargetIdentifier = null!;
        CorrelationId = null!;
    }

    public Guid Id { get; private set; }

    public Guid ActorIdentifier { get; private set; }

    public OperatorRole ActorRole { get; private set; }

    public string OperationIdentifier { get; private set; }

    public string TargetIdentifier { get; private set; }

    public AuditResult Result { get; private set; }

    public string? FailureBusinessErrorCode { get; private set; }

    public string CorrelationId { get; private set; }

    public DateTimeOffset AuditTime { get; private set; }

    public static AuditRecord Create(
        Guid actorIdentifier,
        OperatorRole actorRole,
        string operationIdentifier,
        string targetIdentifier,
        AuditResult result,
        string? failureBusinessErrorCode,
        string correlationId,
        DateTimeOffset utcNow)
    {
        if (actorIdentifier == Guid.Empty)
        {
            throw new ArgumentException("The audited actor identifier cannot be empty.", nameof(actorIdentifier));
        }

        if (actorRole is not (OperatorRole.Administrator or OperatorRole.Teller or OperatorRole.Viewer))
        {
            throw new ArgumentOutOfRangeException(nameof(actorRole), actorRole, "Unknown audited actor role.");
        }

        if (result is not (AuditResult.Success or AuditResult.Failure))
        {
            throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown Audit result.");
        }

        string operation = RequireBounded(operationIdentifier, OperationIdentifierMaxLength, nameof(operationIdentifier));
        string target = RequireBounded(targetIdentifier, TargetIdentifierMaxLength, nameof(targetIdentifier));
        string correlation = RequireBounded(correlationId, CorrelationIdMaxLength, nameof(correlationId));
        string? failureCode = NormalizeOptional(
            failureBusinessErrorCode,
            BusinessErrorCodeMaxLength,
            nameof(failureBusinessErrorCode));

        if (result == AuditResult.Success && failureCode is not null)
        {
            throw new ArgumentException(
                "A successful Audit record cannot contain a failure business error code.",
                nameof(failureBusinessErrorCode));
        }

        DateTimeOffset auditTime = utcNow.ToUniversalTime();

        return new AuditRecord
        {
            Id = Guid.CreateVersion7(auditTime),
            ActorIdentifier = actorIdentifier,
            ActorRole = actorRole,
            OperationIdentifier = operation,
            TargetIdentifier = target,
            Result = result,
            FailureBusinessErrorCode = failureCode,
            CorrelationId = correlation,
            AuditTime = auditTime,
        };
    }

    private static string RequireBounded(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        string trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : throw new ArgumentOutOfRangeException(parameterName);
    }

    private static string? NormalizeOptional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : throw new ArgumentOutOfRangeException(parameterName);
    }
}
