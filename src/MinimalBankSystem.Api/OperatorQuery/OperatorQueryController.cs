using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    BankDbContext dbContext,
    IAuditWriter auditWriter,
    CurrentOperatorRequestContext requestContext) : ControllerBase
{
    [Authorize(Policy = CurrentOperatorPolicyNames.Administrator)]
    [OperatorQueryAuthorizationAuditContext(OperatorQueryOperations.List, detailTarget: false)]
    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken)
    {
        OperatorQueryRow[] rows = await dbContext.Operators
            .AsNoTracking()
            .OrderBy(operatorEntity => operatorEntity.Id)
            .Select(operatorEntity => new OperatorQueryRow(
                operatorEntity.Id,
                operatorEntity.UserName,
                operatorEntity.State,
                operatorEntity.Role,
                operatorEntity.CreatedAt,
                operatorEntity.UpdatedAt))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        OperatorQueryProjection[] projection = rows.Select(Map).ToArray();

        return await AuditBeforeResultAsync(
                OperatorQueryOperations.List,
                OperatorQueryOperations.CollectionTarget,
                AuditResult.Success,
                failureBusinessErrorCode: null,
                () => Ok(projection),
                cancellationToken)
            .ConfigureAwait(false);
    }

    [Authorize(Policy = CurrentOperatorPolicyNames.Administrator)]
    [OperatorQueryAuthorizationAuditContext(OperatorQueryOperations.Detail, detailTarget: true)]
    [HttpGet("{operatorId:guid}")]
    public async Task<IActionResult> GetDetail(
        Guid operatorId,
        CancellationToken cancellationToken)
    {
        OperatorQueryRow? row = await dbContext.Operators
            .AsNoTracking()
            .Where(operatorEntity => operatorEntity.Id == operatorId)
            .Select(operatorEntity => new OperatorQueryRow(
                operatorEntity.Id,
                operatorEntity.UserName,
                operatorEntity.State,
                operatorEntity.Role,
                operatorEntity.CreatedAt,
                operatorEntity.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return await AuditBeforeResultAsync(
                    OperatorQueryOperations.Detail,
                    operatorId.ToString("D"),
                    AuditResult.Failure,
                    ApiErrorEnvelope.OperatorNotFound.Code,
                    () => NotFound(ApiErrorEnvelope.OperatorNotFound),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        OperatorQueryProjection projection = Map(row);

        return await AuditBeforeResultAsync(
                OperatorQueryOperations.Detail,
                operatorId.ToString("D"),
                AuditResult.Success,
                failureBusinessErrorCode: null,
                () => Ok(projection),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IActionResult> AuditBeforeResultAsync(
        string operationIdentifier,
        string targetIdentifier,
        AuditResult result,
        string? failureBusinessErrorCode,
        Func<IActionResult> resultFactory,
        CancellationToken cancellationToken)
    {
        CurrentOperatorSnapshot actor = requestContext.CurrentOperator
            ?? throw new InvalidOperationException(
                "An Operator query requires a current authenticated Product-Audit actor.");

        AuditWriteRequest request = new(
            actor.Identifier,
            actor.Role,
            operationIdentifier,
            targetIdentifier,
            result,
            failureBusinessErrorCode,
            HttpContext.TraceIdentifier);

        return await auditWriter
            .AppendInSeparateTransactionBeforeResultAsync(
                request,
                _ => Task.FromResult(resultFactory()),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static OperatorQueryProjection Map(OperatorQueryRow row) =>
        new(
            row.Identifier,
            ToStateToken(row.State),
            ToRoleToken(row.Role),
            row.LoginIdentifier,
            row.CreatedAt,
            row.UpdatedAt);

    private static string ToStateToken(OperatorState state) => state switch
    {
        OperatorState.Active => "active",
        OperatorState.Disabled => "disabled",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown Operator state."),
    };

    private static string ToRoleToken(OperatorRole role) => role switch
    {
        OperatorRole.Administrator => "administrator",
        OperatorRole.Teller => "teller",
        OperatorRole.Viewer => "viewer",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown Operator role."),
    };

    private sealed record OperatorQueryRow(
        Guid Identifier,
        string LoginIdentifier,
        OperatorState State,
        OperatorRole Role,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}

public sealed record OperatorQueryProjection(
    Guid OperatorIdentifier,
    string State,
    string Role,
    string LoginIdentifier,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
