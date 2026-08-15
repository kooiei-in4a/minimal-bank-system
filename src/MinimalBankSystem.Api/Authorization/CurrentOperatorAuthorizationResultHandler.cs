using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Routing;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Auditing;

namespace MinimalBankSystem.Api.Authorization;

internal sealed class CurrentOperatorAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
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
        await auditWriter
            .AppendInSeparateTransactionBeforeResultAsync(
                auditRequest,
                _ => Task.FromResult(true),
                httpContext.RequestAborted)
            .ConfigureAwait(false);

        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(
            ApiErrorEnvelope.OperationNotPermitted,
            httpContext.RequestAborted);
    }
}
