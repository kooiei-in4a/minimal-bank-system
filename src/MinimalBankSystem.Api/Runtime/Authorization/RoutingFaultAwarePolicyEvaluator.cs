using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Routing;

namespace MinimalBankSystem.Api.Runtime.Authorization;

/// <summary>
/// Keeps the deny-by-default fallback effective for every routable endpoint while preserving the
/// framework routing error contract for requests that no endpoint can serve. The
/// AuthorizationMiddleware applies the fallback even when routing failed: no endpoint matched
/// (404), or the matched endpoint is one of the framework's synthesized routing-error endpoints
/// (405 from HttpMethodMatcherPolicy, 415 from AcceptsMatcherPolicy), both plain Endpoint
/// instances with empty metadata. Such requests can never reach a product handler, so applying
/// default-deny would only turn framework errors into 401. Every routable endpoint in this
/// application is a RouteEndpoint; this evaluator bypasses only the non-routable cases and
/// delegates everything else to the default evaluator.
/// </summary>
public sealed class RoutingFaultAwarePolicyEvaluator(IPolicyEvaluator inner) : IPolicyEvaluator
{
    public Task<AuthenticateResult> AuthenticateAsync(
        AuthorizationPolicy policy,
        HttpContext context) =>
        inner.AuthenticateAsync(policy, context);

    public Task<PolicyAuthorizationResult> AuthorizeAsync(
        AuthorizationPolicy policy,
        AuthenticateResult authenticationResult,
        HttpContext context,
        object? resource)
    {
        if (context.GetEndpoint() is not RouteEndpoint)
        {
            return Task.FromResult(PolicyAuthorizationResult.Success());
        }

        return inner.AuthorizeAsync(policy, authenticationResult, context, resource);
    }
}
