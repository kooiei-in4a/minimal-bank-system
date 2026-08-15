using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Domain.Auditing;

/// <summary>
/// Immutable Product Audit persistence entity. Actor data is a historical snapshot and deliberately
/// has no navigation or foreign-key relationship to the current Operator row.
/// </summary>
public sealed class AuditRecord
{
    public const int OperationIdentifierMaxLength = 128;
    public const int TargetIdentifierMaxLength = 256;
    public const int FailureBusinessErrorCodeMaxLength = 64;
    public const int CorrelationIdMaxLength = 128;

    private AuditRecord()
    {
        OperationIdentifier = null!;
        TargetIdentifier = null!;
        CorrelationId = null!;
    }

    public Guid AuditId { get; private set; }

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
            throw new ArgumentException("An authenticated actor identifier is required.", nameof(actorIdentifier));
        }

        if (actorRole is not (OperatorRole.Administrator or OperatorRole.Teller or OperatorRole.Viewer))
        {
            throw new ArgumentOutOfRangeException(nameof(actorRole), actorRole, "Unknown actor role snapshot.");
        }

        if (result is not (AuditResult.Success or AuditResult.Failure))
        {
            throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown Product Audit result.");
        }

        string operation = ValidateRequiredText(
            operationIdentifier,
            OperationIdentifierMaxLength,
            nameof(operationIdentifier));
        string target = ValidateRequiredText(
            targetIdentifier,
            TargetIdentifierMaxLength,
            nameof(targetIdentifier));
        string correlation = ValidateRequiredText(
            correlationId,
            CorrelationIdMaxLength,
            nameof(correlationId));
        string? failureCode = ValidateOptionalText(
            failureBusinessErrorCode,
            FailureBusinessErrorCodeMaxLength,
            nameof(failureBusinessErrorCode));

        if (result == AuditResult.Success && failureCode is not null)
        {
            throw new ArgumentException(
                "A successful Product Audit record cannot contain a failure business error code.",
                nameof(failureBusinessErrorCode));
        }

        DateTimeOffset auditTime = utcNow.ToUniversalTime();

        return new AuditRecord
        {
            AuditId = Guid.CreateVersion7(auditTime),
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

    private static string ValidateRequiredText(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        if (value.Length > maxLength || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return value;
    }

    private static string? ValidateOptionalText(string? value, int maxLength, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        return ValidateRequiredText(value, maxLength, parameterName);
    }
}
