using Microsoft.AspNetCore.Mvc;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence.Identity;

namespace MinimalBankSystem.Api.OperatorCreate;

/// <summary>
/// Closed create-success projection. Credential, hash, stamp, authorization-state and version
/// fields are unavailable to MVC serialization because they are not members of this type.
/// </summary>
public sealed record OperatorCreateResponse(
    Guid OperatorIdentifier,
    string State,
    string Role);

public sealed record CreateOperatorRequest(
    string? LoginIdentifier,
    string? Password,
    string? Role);

public interface IOperatorCreateExecutor
{
    Task<IActionResult> ExecuteAsync(
        CreateOperatorRequest request,
        Guid actorIdentifier,
        OperatorRole actorRole,
        string correlationId,
        CancellationToken cancellationToken);
}

internal static class OperatorCreateContract
{
    public static bool HasUsableCredential(string? value) =>
        !string.IsNullOrWhiteSpace(value);

    public static bool TryParseRole(string? role, out OperatorRole parsed)
    {
        switch (role)
        {
            case OperatorPersistence.AdministratorRoleToken:
                parsed = OperatorRole.Administrator;
                return true;
            case OperatorPersistence.TellerRoleToken:
                parsed = OperatorRole.Teller;
                return true;
            case OperatorPersistence.ViewerRoleToken:
                parsed = OperatorRole.Viewer;
                return true;
            default:
                parsed = OperatorRole.Unspecified;
                return false;
        }
    }

    public static string ToStateToken(OperatorState state) => state switch
    {
        OperatorState.Active => OperatorPersistence.ActiveStateToken,
        OperatorState.Disabled => OperatorPersistence.DisabledStateToken,
        _ => throw new ArgumentOutOfRangeException(
            nameof(state),
            state,
            "Unknown Operator state cannot be exposed by the create API."),
    };

    public static string ToRoleToken(OperatorRole role) => role switch
    {
        OperatorRole.Administrator => OperatorPersistence.AdministratorRoleToken,
        OperatorRole.Teller => OperatorPersistence.TellerRoleToken,
        OperatorRole.Viewer => OperatorPersistence.ViewerRoleToken,
        _ => throw new ArgumentOutOfRangeException(
            nameof(role),
            role,
            "Unknown Operator role cannot be exposed by the create API."),
    };

    public static OperatorCreateResponse ToResponse(Operator created) =>
        new(
            created.Id,
            ToStateToken(created.State),
            ToRoleToken(created.Role));

    public static string CanonicalOperatorTarget(Guid operatorIdentifier) =>
        operatorIdentifier.ToString("D");

    public static AuditWriteRequest Success(
        Guid actorIdentifier,
        OperatorRole actorRole,
        Guid createdOperatorIdentifier,
        string correlationId) =>
        new(
            actorIdentifier,
            actorRole,
            OperatorCreateAudit.OperationIdentifier,
            CanonicalOperatorTarget(createdOperatorIdentifier),
            AuditResult.Success,
            FailureBusinessErrorCode: null,
            correlationId);

    public static AuditWriteRequest Rejection(
        Guid actorIdentifier,
        OperatorRole actorRole,
        string failureBusinessErrorCode,
        string correlationId) =>
        new(
            actorIdentifier,
            actorRole,
            OperatorCreateAudit.OperationIdentifier,
            OperatorCreateAudit.CollectionTargetIdentifier,
            AuditResult.Failure,
            failureBusinessErrorCode,
            correlationId);
}
