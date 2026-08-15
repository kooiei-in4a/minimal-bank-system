using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authorization;

namespace MinimalBankSystem.Api.Runtime.Authorization;

/// <summary>
/// The AUTHZ-01 result handler. A rejected request whose presented authentication state is no
/// longer valid (unknown, disabled or stale Operator) is answered with HTTP 401 and only a
/// technical/security log entry (ADR-0007, ADR-0008). Every other authenticated non-consuming
/// rejection writes exactly one Product Audit failure record in a short separate transaction
/// before the HTTP 403 / operation_not_permitted response is produced (ADR-0008).
/// </summary>
public sealed class OperatorAuthorizationResultHandler(
    ILogger<OperatorAuthorizationResultHandler> logger) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
        {
            await defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        if (authorizeResult.Forbidden)
        {
            // The handler always records its resolution for authenticated requests with an
            // operator requirement. Explicit Fail() carries no requirements in this framework,
            // so the resolution status is the authoritative stale-state signal.
            CurrentOperatorContext operatorContext =
                context.RequestServices.GetRequiredService<CurrentOperatorContext>();

            if (operatorContext.Resolution?.Status != CurrentOperatorResolutionStatus.Success)
            {
                await RejectStaleAuthenticationStateAsync(context, operatorContext);
                return;
            }

            await RejectAsNotPermittedAsync(context, operatorContext);
            return;
        }

        await defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    private async Task RejectStaleAuthenticationStateAsync(
        HttpContext context,
        CurrentOperatorContext operatorContext)
    {
        string reason = operatorContext.Resolution?.Status.ToString() ?? "not-resolved";

        AuthorizationTechnicalLog.OperatorAuthenticationStateRejected(
            logger,
            reason,
            context.TraceIdentifier);

        await context.ChallengeAsync();
    }

    private async Task RejectAsNotPermittedAsync(
        HttpContext context,
        CurrentOperatorContext operatorContext)
    {
        PolicyRejectionAuditAttribute? audit =
            context.GetEndpoint()?.Metadata.GetMetadata<PolicyRejectionAuditAttribute>();
        Operator? currentOperator = operatorContext.CurrentOperator;

        if (audit is null || currentOperator is null)
        {
            // Fail closed: a forbidden decision without feature-owned Audit context must never
            // surface as an unaudited 403. The exception is mapped to the safe 500 envelope.
            throw new InvalidOperationException(
                "A policy rejection without feature-owned Audit context must not produce an unaudited 403.");
        }

        IAuditWriter auditWriter = context.RequestServices.GetRequiredService<IAuditWriter>();

        await auditWriter.AppendInSeparateTransactionBeforeResultAsync(
            new AuditWriteRequest(
                currentOperator.Id,
                currentOperator.Role,
                audit.OperationIdentifier,
                audit.TargetIdentifier,
                AuditResult.Failure,
                ApiErrorEnvelope.OperationNotPermitted.Code,
                context.TraceIdentifier),
            async _ =>
            {
                AuthorizationTechnicalLog.PolicyRejectionAudited(
                    logger,
                    currentOperator.Id,
                    currentOperator.Role,
                    audit.OperationIdentifier,
                    audit.TargetIdentifier,
                    context.TraceIdentifier);

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(
                    ApiErrorEnvelope.OperationNotPermitted,
                    context.RequestAborted);

                return true;
            });
    }
}
