using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Routing;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Auditing;

namespace MinimalBankSystem.Api.Authorization;

internal sealed class CurrentOperatorAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    // Mandatory authenticated-403 Product Audit persistence must not depend on
    // HttpContext.RequestAborted: a client disconnect must not be able to suppress a required
    // Audit record. The write is a single short Audit-only transaction (begin + SaveChanges +
    // commit), so it is bounded by its own timeout instead of an unbounded CancellationToken.None.
    // The bound mirrors the existing PostgreSQL-dependent wait convention used for readiness
    // checks (see HealthContract.ReadinessTimeout) so this fix does not invent a new timeout
    // policy for the codebase.
    private static readonly TimeSpan MandatoryAuditPersistenceTimeout = TimeSpan.FromSeconds(10);

    private readonly AuthorizationMiddlewareResultHandler defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext httpContext,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        // Framework routing faults (404 unmatched, 405 method, 415 media type) are plain Endpoint
        // instances, not RouteEndpoint. Bypass authorization result rewriting so the existing API
        // error contract remains intact. Every product endpoint in this application is a
        // RouteEndpoint.
        if (httpContext.GetEndpoint() is not RouteEndpoint)
        {
            await next(httpContext).ConfigureAwait(false);
            return;
        }

        if (!authorizeResult.Forbidden)
        {
            await defaultHandler
                .HandleAsync(next, httpContext, policy, authorizeResult)
                .ConfigureAwait(false);
            return;
        }

        CurrentOperatorRequestContext requestContext = httpContext.RequestServices
            .GetRequiredService<CurrentOperatorRequestContext>();

        if (requestContext.AuthenticationInvalidated)
        {
            await defaultHandler
                .HandleAsync(next, httpContext, policy, PolicyAuthorizationResult.Challenge())
                .ConfigureAwait(false);
            return;
        }

        CurrentOperatorSnapshot currentOperator = requestContext.CurrentOperator
            ?? throw new InvalidOperationException(
                "An authenticated policy rejection did not resolve a current Product-Audit actor.");

        Endpoint endpoint = httpContext.GetEndpoint()
            ?? throw new InvalidOperationException(
                "An authenticated policy rejection requires a matched RouteEndpoint.");

        IReadOnlyList<IAuthorizationAuditContext> auditContexts = endpoint.Metadata
            .GetOrderedMetadata<IAuthorizationAuditContext>();
        if (auditContexts.Count != 1)
        {
            throw new InvalidOperationException(
                "An authenticated policy rejection requires exactly one feature-owned Product-Audit context.");
        }

        IAuthorizationAuditContext auditContext = auditContexts[0];
        string? targetIdentifier = await auditContext
            .ResolveTargetIdentifierAsync(httpContext)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(auditContext.OperationIdentifier) ||
            string.IsNullOrWhiteSpace(targetIdentifier))
        {
            throw new InvalidOperationException(
                "The feature-owned Product-Audit operation or target context was unavailable.");
        }

        AuditWriteRequest auditRequest = new(
            currentOperator.Identifier,
            currentOperator.Role,
            auditContext.OperationIdentifier,
            targetIdentifier,
            AuditResult.Failure,
            ApiErrorEnvelope.OperationNotPermitted.Code,
            httpContext.TraceIdentifier);

        IAuditWriter auditWriter = httpContext.RequestServices.GetRequiredService<IAuditWriter>();

        // A dedicated, bounded token — not linked to RequestAborted — so a client disconnect
        // cannot cancel the mandatory 403 Audit write. Audit persistence failure (including a
        // timeout here) still propagates and fails closed via the existing exception handling.
        using CancellationTokenSource auditCancellation = new(MandatoryAuditPersistenceTimeout);
        await auditWriter
            .AppendInSeparateTransactionBeforeResultAsync(
                auditRequest,
                _ => Task.FromResult(true),
                auditCancellation.Token)
            .ConfigureAwait(false);

        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(
            ApiErrorEnvelope.OperationNotPermitted,
            httpContext.RequestAborted);
    }
}
