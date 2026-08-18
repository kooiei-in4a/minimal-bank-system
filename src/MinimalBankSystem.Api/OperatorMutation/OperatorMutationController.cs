using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence.Identity;

namespace MinimalBankSystem.Api.OperatorMutation;

[ApiController]
[Route("operators")]
public sealed class OperatorMutationController(IAuditWriter auditWriter) : ControllerBase
{
    [Authorize(Policy = CurrentOperatorPolicyNames.Administrator)]
    [OperatorMutationAuthorizationAuditContext(OperatorMutationAudit.EnableOperationIdentifier)]
    [HttpPost("{operatorIdentifier:guid}/enable")]
    public Task<IActionResult> Enable(
        Guid operatorIdentifier,
        CancellationToken cancellationToken) =>
        ExecuteAsync(OperatorMutationKind.Enable, operatorIdentifier, requestedRole: null, cancellationToken);

    [Authorize(Policy = CurrentOperatorPolicyNames.Administrator)]
    [OperatorMutationAuthorizationAuditContext(OperatorMutationAudit.DisableOperationIdentifier)]
    [HttpPost("{operatorIdentifier:guid}/disable")]
    public Task<IActionResult> Disable(
        Guid operatorIdentifier,
        CancellationToken cancellationToken) =>
        ExecuteAsync(OperatorMutationKind.Disable, operatorIdentifier, requestedRole: null, cancellationToken);

    [Authorize(Policy = CurrentOperatorPolicyNames.Administrator)]
    [OperatorMutationAuthorizationAuditContext(OperatorMutationAudit.ChangeRoleOperationIdentifier)]
    [HttpPost("{operatorIdentifier:guid}/role")]
    public async Task<IActionResult> ChangeRole(
        Guid operatorIdentifier,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] OperatorRoleChangeRequest? request,
        CancellationToken cancellationToken)
    {
        OperatorRole? role = ParseRoleToken(request?.Role);
        if (role is null)
        {
            return await RejectAsync(
                    OperatorMutationKind.ChangeRole,
                    operatorIdentifier,
                    ApiErrorEnvelope.ValidationFailed,
                    StatusCodes.Status400BadRequest,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await ExecuteAsync(
                OperatorMutationKind.ChangeRole,
                operatorIdentifier,
                role,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IActionResult> ExecuteAsync(
        OperatorMutationKind kind,
        Guid operatorIdentifier,
        OperatorRole? requestedRole,
        CancellationToken cancellationToken)
    {
        CurrentOperatorSnapshot actor = GetCurrentActor();
        OperatorMutationOutcome outcome = await HttpContext.RequestServices
            .GetRequiredService<IOperatorMutationService>()
            .ExecuteAsync(
                kind,
                operatorIdentifier,
                requestedRole,
                actor,
                HttpContext.TraceIdentifier,
                cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            OperatorMutationOutcome.Success success => Ok(ToResponse(success.Target)),
            OperatorMutationOutcome.Rejection rejection => await RejectAsync(
                    kind,
                    operatorIdentifier,
                    rejection.Error,
                    rejection.StatusCode,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => throw new InvalidOperationException("Unknown Operator mutation outcome."),
        };
    }

    private CurrentOperatorSnapshot GetCurrentActor() =>
        HttpContext.RequestServices.GetRequiredService<CurrentOperatorRequestContext>().CurrentOperator
        ?? throw new InvalidOperationException(
            "An authorized Operator mutation requires a current Product-Audit actor.");

    private async Task<IActionResult> RejectAsync(
        OperatorMutationKind kind,
        Guid operatorIdentifier,
        ApiErrorEnvelope error,
        int statusCode,
        CancellationToken cancellationToken)
    {
        CurrentOperatorSnapshot actor = GetCurrentActor();
        AuditWriteRequest rejectionAudit = new(
            actor.Identifier,
            actor.Role,
            OperatorMutationService.OperationIdentifier(kind),
            operatorIdentifier.ToString("D"),
            AuditResult.Failure,
            error.Code,
            HttpContext.TraceIdentifier);

        return await auditWriter
            .AppendInSeparateTransactionBeforeResultAsync(
                rejectionAudit,
                _ => Task.FromResult<IActionResult>(StatusCode(statusCode, error)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static OperatorMutationResponse ToResponse(Operator target) =>
        new(
            target.Id,
            ToStateToken(target.State),
            ToRoleToken(target.Role));

    private static OperatorRole? ParseRoleToken(string? token) => token switch
    {
        OperatorPersistence.AdministratorRoleToken => OperatorRole.Administrator,
        OperatorPersistence.TellerRoleToken => OperatorRole.Teller,
        OperatorPersistence.ViewerRoleToken => OperatorRole.Viewer,
        _ => null,
    };

    private static string ToStateToken(OperatorState state) => state switch
    {
        OperatorState.Active => OperatorPersistence.ActiveStateToken,
        OperatorState.Disabled => OperatorPersistence.DisabledStateToken,
        _ => throw new ArgumentOutOfRangeException(
            nameof(state),
            state,
            "Unknown Operator state cannot be exposed by the mutation API."),
    };

    private static string ToRoleToken(OperatorRole role) => role switch
    {
        OperatorRole.Administrator => OperatorPersistence.AdministratorRoleToken,
        OperatorRole.Teller => OperatorPersistence.TellerRoleToken,
        OperatorRole.Viewer => OperatorPersistence.ViewerRoleToken,
        _ => throw new ArgumentOutOfRangeException(
            nameof(role),
            role,
            "Unknown Operator role cannot be exposed by the mutation API."),
    };
}

public sealed record OperatorRoleChangeRequest(string? Role);

/// <summary>
/// Deliberately closed Operator mutation success projection. Credential, security and
/// authorization-state fields remain unavailable to MVC serialization because they are not part
/// of this type.
/// </summary>
public sealed record OperatorMutationResponse(Guid OperatorIdentifier, string State, string Role);
