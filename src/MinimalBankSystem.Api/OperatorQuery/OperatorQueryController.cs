using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.Api.OperatorQuery;

[ApiController]
[Route("operators")]
public sealed class OperatorQueryController(
    BankDbContext persistence,
    IAuditWriter auditWriter) : ControllerBase
{
    [Authorize(Policy = CurrentOperatorPolicyNames.Administrator)]
    [OperatorListAuthorizationAuditContext]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        CurrentOperatorSnapshot actor = GetCurrentActor();
        IReadOnlyList<OperatorQueryRow> rows = await persistence.Operators
            .AsNoTracking()
            .OrderBy(operatorEntity => operatorEntity.Id)
            .Select(operatorEntity => new OperatorQueryRow(
                operatorEntity.Id,
                operatorEntity.State,
                operatorEntity.Role))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<OperatorQueryResponse> projection = rows
            .Select(ToResponse)
            .ToArray();

        AuditWriteRequest audit = new(
            actor.Identifier,
            actor.Role,
            OperatorQueryAudit.ListOperationIdentifier,
            OperatorQueryAudit.CollectionTargetIdentifier,
            AuditResult.Success,
            FailureBusinessErrorCode: null,
            HttpContext.TraceIdentifier);

        return await auditWriter
            .AppendInSeparateTransactionBeforeResultAsync(
                audit,
                _ => Task.FromResult<IActionResult>(Ok(projection)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    [Authorize(Policy = CurrentOperatorPolicyNames.Administrator)]
    [OperatorDetailAuthorizationAuditContext]
    [HttpGet("{operatorIdentifier:guid}")]
    public async Task<IActionResult> Detail(
        Guid operatorIdentifier,
        CancellationToken cancellationToken)
    {
        CurrentOperatorSnapshot actor = GetCurrentActor();
        OperatorQueryRow? row = await persistence.Operators
            .AsNoTracking()
            .Where(operatorEntity => operatorEntity.Id == operatorIdentifier)
            .Select(operatorEntity => new OperatorQueryRow(
                operatorEntity.Id,
                operatorEntity.State,
                operatorEntity.Role))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        string targetIdentifier = operatorIdentifier.ToString("D");
        if (row is null)
        {
            AuditWriteRequest rejectionAudit = new(
                actor.Identifier,
                actor.Role,
                OperatorQueryAudit.DetailOperationIdentifier,
                targetIdentifier,
                AuditResult.Failure,
                ApiErrorEnvelope.OperatorNotFound.Code,
                HttpContext.TraceIdentifier);

            return await auditWriter
                .AppendInSeparateTransactionBeforeResultAsync(
                    rejectionAudit,
                    _ => Task.FromResult<IActionResult>(NotFound(ApiErrorEnvelope.OperatorNotFound)),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        OperatorQueryResponse projection = ToResponse(row);

        AuditWriteRequest audit = new(
            actor.Identifier,
            actor.Role,
            OperatorQueryAudit.DetailOperationIdentifier,
            targetIdentifier,
            AuditResult.Success,
            FailureBusinessErrorCode: null,
            HttpContext.TraceIdentifier);

        return await auditWriter
            .AppendInSeparateTransactionBeforeResultAsync(
                audit,
                _ => Task.FromResult<IActionResult>(Ok(projection)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private CurrentOperatorSnapshot GetCurrentActor() =>
        HttpContext.RequestServices.GetRequiredService<CurrentOperatorRequestContext>().CurrentOperator
        ?? throw new InvalidOperationException(
            "An authorized Operator query requires a current Product-Audit actor.");

    private static OperatorQueryResponse ToResponse(OperatorQueryRow row) =>
        new(
            row.OperatorIdentifier,
            ToStateToken(row.State),
            ToRoleToken(row.Role));

    private static string ToStateToken(OperatorState state) => state switch
    {
        OperatorState.Active => "active",
        OperatorState.Disabled => "disabled",
        _ => throw new ArgumentOutOfRangeException(
            nameof(state),
            state,
            "Unknown Operator state cannot be exposed by the query API."),
    };

    private static string ToRoleToken(OperatorRole role) => role switch
    {
        OperatorRole.Administrator => "administrator",
        OperatorRole.Teller => "teller",
        OperatorRole.Viewer => "viewer",
        _ => throw new ArgumentOutOfRangeException(
            nameof(role),
            role,
            "Unknown Operator role cannot be exposed by the query API."),
    };

    private sealed record OperatorQueryRow(
        Guid OperatorIdentifier,
        OperatorState State,
        OperatorRole Role);
}

/// <summary>
/// Deliberately closed Operator query projection. Credential, security and authorization-state
/// fields remain unavailable to MVC serialization because they are not part of this type.
/// </summary>
public sealed record OperatorQueryResponse(
    Guid OperatorIdentifier,
    string State,
    string Role);
