using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Infrastructure.Authorization;

namespace MinimalBankSystem.Api.Authorization;

/// <summary>
/// AUTHZ (#168) 401/403 mapping. An authenticated-but-state-invalid rejection (missing Operator,
/// disabled Operator, or authorization-state-version mismatch) is re-issued as the same JWT-bearer
/// 401 challenge AUTHN already produces for an invalid token, because the presented authentication
/// state is no longer valid (ADR-0007). An authenticated, state-valid, role-insufficient rejection
/// is the AUTHZ-owned 403: it writes exactly one policy-rejection Product Audit in a separate
/// short transaction before the 403 body is produced, and fails closed with the existing
/// internal-error contract — never an unaudited 403 — when the required resolved-Operator or
/// feature-owned operation/target context is unavailable.
/// </summary>
public sealed class CurrentOperatorAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private static readonly AuthorizationMiddlewareResultHandler FrameworkDefault = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (authorizeResult.Forbidden)
        {
            if (HasReason(authorizeResult, CurrentOperatorAuthorizationReasons.OperatorStateInvalid))
            {
                await context.ChallengeAsync(JwtBearerDefaults.AuthenticationScheme).ConfigureAwait(false);
                return;
            }

            if (HasReason(authorizeResult, CurrentOperatorAuthorizationReasons.OperatorRoleInsufficient))
            {
                await HandlePolicyRejectionAsync(context).ConfigureAwait(false);
                return;
            }
        }

        await FrameworkDefault.HandleAsync(next, context, policy, authorizeResult).ConfigureAwait(false);
    }

    private static bool HasReason(PolicyAuthorizationResult authorizeResult, string reason) =>
        authorizeResult.AuthorizationFailure?.FailureReasons.Any(failureReason =>
            string.Equals(failureReason.Message, reason, StringComparison.Ordinal)) ?? false;

    private static async Task HandlePolicyRejectionAsync(HttpContext context)
    {
        bool hasResolvedOperator = context.Items.TryGetValue(
            CurrentOperatorAuthorizationContextKeys.ResolvedOperator,
            out object? resolvedObject);
        IAuditOperationContext? auditOperationContext =
            context.GetEndpoint()?.Metadata.GetMetadata<IAuditOperationContext>();

        if (hasResolvedOperator && resolvedObject is ResolvedCurrentOperator resolved && auditOperationContext is not null)
        {
            IAuditWriter auditWriter = context.RequestServices.GetRequiredService<IAuditWriter>();
            AuditWriteRequest request = new(
                resolved.OperatorId,
                resolved.Role,
                auditOperationContext.OperationIdentifier,
                auditOperationContext.ResolveTargetIdentifier(context),
                AuditResult.Failure,
                ApiErrorEnvelope.OperationNotPermitted.Code,
                context.TraceIdentifier);

            await auditWriter.AppendInSeparateTransactionBeforeResultAsync(
                request,
                async cancellationToken =>
                {
                    await WriteForbiddenAsync(context, cancellationToken).ConfigureAwait(false);
                    return true;
                },
                context.RequestAborted).ConfigureAwait(false);
            return;
        }

        // The required Audit context (resolved actor, or the feature-owned operation/target
        // metadata) is unavailable. An unaudited 403 is prohibited; fail closed with the existing
        // internal-error contract instead of fabricating a generic AUTHZ operation/target.
        throw new InvalidOperationException(
            "An authenticated AUTHZ policy rejection requires both a resolved current Operator and " +
            $"a registered {nameof(IAuditOperationContext)} endpoint metadata contribution from the owning feature leaf.");
    }

    private static async Task WriteForbiddenAsync(HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(ApiErrorEnvelope.OperationNotPermitted, cancellationToken)
            .ConfigureAwait(false);
    }
}
