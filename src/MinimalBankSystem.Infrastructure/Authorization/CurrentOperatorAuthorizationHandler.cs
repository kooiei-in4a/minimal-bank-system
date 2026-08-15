using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.Infrastructure.Authorization;

/// <summary>
/// AUTHZ (#168) request-time authority. Resolves the current Operator named by the JWT subject
/// against the persisted database state and enforces, in order: active/disabled state,
/// authorization-state-version currency and — only when the policy requires specific roles — the
/// current database role. This handler never reads a JWT role claim; the current database role is
/// the sole authorization authority.
/// </summary>
/// <remarks>
/// <see cref="IAuthorizationHandler"/> registrations are constructed by
/// <c>DefaultAuthorizationService</c> for every request that reaches <c>UseAuthorization()</c>,
/// including unmatched routes and <c>[AllowAnonymous]</c> endpoints, because it eagerly resolves
/// <c>IEnumerable&lt;IAuthorizationHandler&gt;</c> regardless of whether a requirement is ever
/// evaluated. A constructor-injected <see cref="BankDbContext"/> would therefore fail closed with
/// an unconfigured-connection-string exception on every request, defeating the framework's own
/// 404/405 contract and AllowAnonymous endpoints alike. <see cref="IServiceProvider"/> is injected
/// instead so <see cref="BankDbContext"/> is resolved lazily, only from inside
/// <see cref="HandleRequirementAsync"/>, which runs only when this requirement is actually
/// evaluated.
/// </remarks>
public sealed class CurrentOperatorAuthorizationHandler(
    IServiceProvider serviceProvider,
    ILogger<CurrentOperatorAuthorizationHandler> logger)
    : AuthorizationHandler<CurrentOperatorAuthorizationRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CurrentOperatorAuthorizationRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (context.Resource is not HttpContext httpContext)
        {
            // Evaluated outside endpoint routing/HTTP (for example a design-time or non-HTTP
            // caller). AUTHZ has nothing to resolve; fail closed rather than guess.
            Fail(context, "no_http_resource");
            return;
        }

        Endpoint? endpoint = httpContext.GetEndpoint();

        // No real endpoint carries protectable behavior here: a fully unmatched route leaves
        // GetEndpoint() null, while ASP.NET Core's routing reports an HTTP-method or media-type
        // mismatch (405/415) through a synthetic placeholder endpoint that carries no metadata at
        // all (confirmed empirically: DisplayName "405 HTTP Method Not Supported" /
        // "415 HTTP Unsupported Media Type", Metadata.Count == 0), distinct from every real
        // controller action or health-check endpoint mapped in this application, all of which
        // always carry non-empty metadata. Deferring here lets the framework's own 404/405/415
        // contract stand instead of masking it behind a 401.
        if (endpoint is null || endpoint.Metadata.Count == 0)
        {
            context.Succeed(requirement);
            return;
        }

        string? subjectClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        string? versionClaim = context.User.FindFirst(AuthnClaimTypes.AuthorizationStateVersion)?.Value;

        if (!Guid.TryParse(subjectClaim, out Guid operatorId) ||
            !int.TryParse(versionClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out int tokenAuthorizationStateVersion))
        {
            // No bearer token, or one lacking the required claims: resolve nothing from the
            // database for an unauthenticated/malformed principal.
            Fail(context, "malformed_token_claims");
            return;
        }

        CancellationToken cancellationToken = httpContext.RequestAborted;
        BankDbContext dbContext = serviceProvider.GetRequiredService<BankDbContext>();
        Operator? current = await dbContext.Operators
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == operatorId, cancellationToken)
            .ConfigureAwait(false);

        if (current is null)
        {
            Fail(context, "operator_not_found");
            return;
        }

        if (current.State is not OperatorState.Active)
        {
            Fail(context, "operator_disabled");
            return;
        }

        if (current.AuthorizationStateVersion != tokenAuthorizationStateVersion)
        {
            Fail(context, "authorization_state_version_mismatch");
            return;
        }

        if (requirement.AllowedRoles.Count > 0 && !requirement.AllowedRoles.Contains(current.Role))
        {
            httpContext.Items[CurrentOperatorAuthorizationContextKeys.ResolvedOperator] =
                new ResolvedCurrentOperator(current.Id, current.Role);
            context.Fail(new AuthorizationFailureReason(this, CurrentOperatorAuthorizationReasons.OperatorRoleInsufficient));
            return;
        }

        context.Succeed(requirement);
    }

    private void Fail(AuthorizationHandlerContext context, string reason)
    {
        // No current authenticated Product-Audit actor exists for these cases (#168 401
        // classification), so only a technical/security log is emitted here; Product Audit is
        // never written for a state-invalid rejection.
        CurrentOperatorAuthorizationLog.OperatorStateInvalid(logger, reason);
        context.Fail(new AuthorizationFailureReason(this, CurrentOperatorAuthorizationReasons.OperatorStateInvalid));
    }
}

internal static partial class CurrentOperatorAuthorizationLog
{
    // Allow-list only: a fixed reason code. Never the token, claims, or Operator identifier.
    [LoggerMessage(
        EventId = 1200,
        Level = LogLevel.Warning,
        Message = "AUTHZ rejected a request because the current Operator authorization state was invalid: {Reason}.")]
    public static partial void OperatorStateInvalid(ILogger logger, string reason);
}
