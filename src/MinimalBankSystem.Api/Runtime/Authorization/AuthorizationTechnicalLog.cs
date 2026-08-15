using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Api.Runtime.Authorization;

internal static partial class AuthorizationTechnicalLog
{
    // Allow-list only. Never pass an exception object, message, stack trace,
    // request body/header/query, credential, token, raw key, or personal data.

    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Warning,
        Message = "Authenticated request rejected: presented authentication state is no longer valid ({Reason}). Correlation ID: {CorrelationId}.")]
    public static partial void OperatorAuthenticationStateRejected(
        ILogger logger,
        string reason,
        string correlationId);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Warning,
        Message = "Current Operator resolution failed ({Reason}). Correlation ID: {CorrelationId}.")]
    public static partial void OperatorResolutionFailed(
        ILogger logger,
        string reason,
        string correlationId);

    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Information,
        Message = "Policy rejection audited: operator {OperatorId} ({OperatorRole}) was not permitted for operation {OperationIdentifier} on target {TargetIdentifier}. Correlation ID: {CorrelationId}.")]
    public static partial void PolicyRejectionAudited(
        ILogger logger,
        Guid operatorId,
        OperatorRole operatorRole,
        string operationIdentifier,
        string targetIdentifier,
        string correlationId);
}
