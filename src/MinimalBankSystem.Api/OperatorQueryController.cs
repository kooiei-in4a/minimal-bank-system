using System.Text.Json.Serialization;
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

namespace MinimalBankSystem.Api;

/// <summary>
/// Admin-only Operator query endpoints owned by WP2-OPR-QRY-01. AUTHZ (#168) continues to own
/// authenticated policy-rejection Product Audit; this feature owns query success and
/// missing-detail handler-rejection Product Audit, plus the feature-owned Audit context AUTHZ
/// consumes for policy-rejection Audit on these routes.
/// </summary>
[ApiController]
[Route("operators")]
[Authorize(Policy = CurrentOperatorPolicyNames.Administrator)]
public sealed class OperatorQueryController(
    BankDbContext context,
    IAuditWriter auditWriter) : ControllerBase
{
    [HttpGet]
    [OperatorListAuthorizationAuditContext]
    public async Task<ActionResult<IReadOnlyList<OperatorQueryProjection>>> List(
        CancellationToken cancellationToken)
    {
        CurrentOperatorSnapshot actor = RequireCurrentOperator();

        List<OperatorQueryProjection> operators = await context.Operators
            .AsNoTracking()
            .OrderBy(operatorEntity => operatorEntity.CreatedAt)
            .ThenBy(operatorEntity => operatorEntity.Id)
            .Select(operatorEntity => new OperatorQueryProjection(
                operatorEntity.Id,
                operatorEntity.State,
                operatorEntity.Role,
                operatorEntity.UserName,
                operatorEntity.CreatedAt,
                operatorEntity.UpdatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        AuditWriteRequest audit = new(
            actor.Identifier,
            actor.Role,
            OperatorQueryOperations.List,
            OperatorQueryOperations.ListTarget,
            AuditResult.Success,
            FailureBusinessErrorCode: null,
            HttpContext.TraceIdentifier);

        return await auditWriter
            .AppendInSeparateTransactionBeforeResultAsync<ActionResult<IReadOnlyList<OperatorQueryProjection>>>(
                audit,
                _ => Task.FromResult<ActionResult<IReadOnlyList<OperatorQueryProjection>>>(Ok(operators)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    [HttpGet("{id:guid}")]
    [OperatorDetailAuthorizationAuditContext]
    public async Task<ActionResult<OperatorQueryProjection>> Detail(
        Guid id,
        CancellationToken cancellationToken)
    {
        CurrentOperatorSnapshot actor = RequireCurrentOperator();
        string targetIdentifier = id.ToString("D");

        OperatorQueryProjection? projection = await context.Operators
            .AsNoTracking()
            .Where(operatorEntity => operatorEntity.Id == id)
            .Select(operatorEntity => new OperatorQueryProjection(
                operatorEntity.Id,
                operatorEntity.State,
                operatorEntity.Role,
                operatorEntity.UserName,
                operatorEntity.CreatedAt,
                operatorEntity.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (projection is null)
        {
            AuditWriteRequest rejection = new(
                actor.Identifier,
                actor.Role,
                OperatorQueryOperations.Detail,
                targetIdentifier,
                AuditResult.Failure,
                ApiErrorEnvelope.OperatorNotFound.Code,
                HttpContext.TraceIdentifier);

            return await auditWriter
                .AppendInSeparateTransactionBeforeResultAsync<ActionResult<OperatorQueryProjection>>(
                    rejection,
                    _ => Task.FromResult<ActionResult<OperatorQueryProjection>>(
                        NotFound(ApiErrorEnvelope.OperatorNotFound)),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        AuditWriteRequest success = new(
            actor.Identifier,
            actor.Role,
            OperatorQueryOperations.Detail,
            targetIdentifier,
            AuditResult.Success,
            FailureBusinessErrorCode: null,
            HttpContext.TraceIdentifier);

        return await auditWriter
            .AppendInSeparateTransactionBeforeResultAsync<ActionResult<OperatorQueryProjection>>(
                success,
                _ => Task.FromResult<ActionResult<OperatorQueryProjection>>(Ok(projection)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private CurrentOperatorSnapshot RequireCurrentOperator() =>
        HttpContext.RequestServices
            .GetRequiredService<CurrentOperatorRequestContext>()
            .CurrentOperator
            ?? throw new InvalidOperationException(
                "An authorized Operator query request did not resolve a current Product-Audit actor.");
}

/// <summary>Fixed Product Audit operation identifiers and target semantics owned by WP2-OPR-QRY-01.</summary>
public static class OperatorQueryOperations
{
    public const string List = "operator.query.list";
    public const string Detail = "operator.query.detail";
    public const string ListTarget = "operators";
}

/// <summary>
/// The approved Operator query response projection. Required fields (identifier, state, role) are
/// always present; the remaining fields are the only permitted optional fields under Issue #169.
/// Password, password hash, security stamp, authorization-state version, and all credential
/// material are never part of this projection by construction.
/// </summary>
public sealed record OperatorQueryProjection(
    Guid Id,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] OperatorState State,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] OperatorRole Role,
    string? UserName,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
internal sealed class OperatorListAuthorizationAuditContextAttribute : Attribute, IAuthorizationAuditContext
{
    public string OperationIdentifier => OperatorQueryOperations.List;

    public ValueTask<string?> ResolveTargetIdentifierAsync(HttpContext httpContext) =>
        ValueTask.FromResult<string?>(OperatorQueryOperations.ListTarget);
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
internal sealed class OperatorDetailAuthorizationAuditContextAttribute : Attribute, IAuthorizationAuditContext
{
    public string OperationIdentifier => OperatorQueryOperations.Detail;

    public ValueTask<string?> ResolveTargetIdentifierAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return ValueTask.FromResult(
            httpContext.Request.RouteValues.TryGetValue("id", out object? routeValue) &&
            routeValue is string raw &&
            Guid.TryParse(raw, out Guid identifier)
                ? identifier.ToString("D")
                : null);
    }
}
