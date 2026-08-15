using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Domain.Auditing;

/// <summary>
/// Approved Product Audit field set. The entity deliberately has no payload, credential, token,
/// request-header or personal-information bag that could be serialized accidentally.
/// </summary>
public sealed class AuditRecord
{
    public const int OperationIdentifierMaxLength = 100;
    public const int TargetIdentifierMaxLength = 256;
    public const int FailureBusinessErrorCodeMaxLength = 100;
    public const int CorrelationIdMaxLength = 64;

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
            throw new ArgumentException("The Audit actor identifier must not be empty.", nameof(actorIdentifier));
        }

        if (actorRole is not (OperatorRole.Administrator or OperatorRole.Teller or OperatorRole.Viewer))
        {
            throw new ArgumentOutOfRangeException(nameof(actorRole), actorRole, "Unknown Audit actor role.");
        }

        string operation = ValidateOperationIdentifier(operationIdentifier);
        string target = ValidateStableIdentifier(
            targetIdentifier,
            TargetIdentifierMaxLength,
            nameof(targetIdentifier));
        string correlation = ValidateStableIdentifier(
            correlationId,
            CorrelationIdMaxLength,
            nameof(correlationId));

        string? failureCode = result switch
        {
            AuditResult.Success when failureBusinessErrorCode is null => null,
            AuditResult.Success => throw new ArgumentException(
                "A successful Audit record cannot contain a failure business error code.",
                nameof(failureBusinessErrorCode)),
            AuditResult.Failure => ValidateStableIdentifier(
                failureBusinessErrorCode,
                FailureBusinessErrorCodeMaxLength,
                nameof(failureBusinessErrorCode)),
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown Audit result."),
        };

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

    internal static string ValidateOperationIdentifier(string operationIdentifier) =>
        ValidateStableIdentifier(
            operationIdentifier,
            OperationIdentifierMaxLength,
            nameof(operationIdentifier));

    private static string ValidateStableIdentifier(
        string? value,
        int maximumLength,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        if (value.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        foreach (char character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.' and not ':')
            {
                throw new ArgumentException(
                    "Audit identifiers may contain only ASCII letters, digits, '-', '_', '.' and ':'.",
                    parameterName);
            }
        }

        return value;
    }
}
