using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Api.OperatorMutation;

[ApiController]
[Route("operators")]
public sealed class OperatorMutationController(
    IOperatorMutationService mutationService,
    IAuditWriter auditWriter) : ControllerBase
{
    [Authorize(Policy = CurrentOperatorPolicyNames.Administrator)]
    [OperatorMutationAuthorizationAuditContext(OperatorMutationKind.Enable)]
    [HttpPost("{operatorIdentifier:guid}/enable")]
    public Task<IActionResult> Enable(Guid operatorIdentifier, CancellationToken cancellationToken) =>
        ExecuteAsync(operatorIdentifier, OperatorMutationKind.Enable, requestedRole: null, cancellationToken);

    [Authorize(Policy = CurrentOperatorPolicyNames.Administrator)]
    [OperatorMutationAuthorizationAuditContext(OperatorMutationKind.Disable)]
    [HttpPost("{operatorIdentifier:guid}/disable")]
    public Task<IActionResult> Disable(Guid operatorIdentifier, CancellationToken cancellationToken) =>
        ExecuteAsync(operatorIdentifier, OperatorMutationKind.Disable, requestedRole: null, cancellationToken);

    [Authorize(Policy = CurrentOperatorPolicyNames.Administrator)]
    [OperatorMutationAuthorizationAuditContext(OperatorMutationKind.ChangeRole)]
    [HttpPost("{operatorIdentifier:guid}/role")]
    public async Task<IActionResult> ChangeRole(
        Guid operatorIdentifier,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] OperatorRoleChangeRequest? request,
        CancellationToken cancellationToken)
    {
        CurrentOperatorSnapshot actor = GetCurrentActor();
        OperatorRole? requestedRole = ParseRoleToken(request?.Role);
        if (requestedRole is null)
        {
            return await RejectAsync(
                    operatorIdentifier,
                    OperatorMutationKind.ChangeRole,
                    ApiErrorEnvelope.ValidationFailed,
                    StatusCodes.Status400BadRequest,
                    actor,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await ExecuteAsync(
                operatorIdentifier,
                OperatorMutationKind.ChangeRole,
                requestedRole,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IActionResult> ExecuteAsync(
        Guid operatorIdentifier,
        OperatorMutationKind operation,
        OperatorRole? requestedRole,
        CancellationToken cancellationToken)
    {
        CurrentOperatorSnapshot actor = GetCurrentActor();
        OperatorMutationResult result = await mutationService
            .ExecuteAsync(
                operatorIdentifier,
                operation,
                requestedRole,
                actor.Identifier,
                actor.Role,
                HttpContext,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.Success is not null)
        {
            return Ok(result.Success);
        }

        return await RejectAsync(
                operatorIdentifier,
                operation,
                result.Error ?? throw new InvalidOperationException("A rejected mutation must have an API error."),
                result.StatusCode,
                actor,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IActionResult> RejectAsync(
        Guid operatorIdentifier,
        OperatorMutationKind operation,
        ApiErrorEnvelope error,
        int statusCode,
        CurrentOperatorSnapshot actor,
        CancellationToken cancellationToken)
    {
        AuditWriteRequest rejectionAudit = new(
            actor.Identifier,
            actor.Role,
            OperatorMutationAudit.GetOperationIdentifier(operation),
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

    private CurrentOperatorSnapshot GetCurrentActor() =>
        HttpContext.RequestServices.GetRequiredService<CurrentOperatorRequestContext>().CurrentOperator
        ?? throw new InvalidOperationException(
            "An authorized Operator mutation requires a current Product-Audit actor.");

    private static OperatorRole? ParseRoleToken(string? token) => token switch
    {
        "administrator" => OperatorRole.Administrator,
        "teller" => OperatorRole.Teller,
        "viewer" => OperatorRole.Viewer,
        _ => null,
    };
}

public sealed record OperatorRoleChangeRequest(string? Role);

/// <summary>
/// Deliberately closed Operator lifecycle success projection. Security material and the
/// authorization-state version are never exposed by the mutation API.
/// </summary>
public sealed record OperatorMutationResponse(Guid OperatorIdentifier, string State, string Role);
