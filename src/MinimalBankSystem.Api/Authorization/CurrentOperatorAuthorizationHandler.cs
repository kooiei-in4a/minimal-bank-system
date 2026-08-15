using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.Api.Authorization;

internal sealed class CurrentOperatorAuthorizationHandler(
    CurrentOperatorRequestContext requestContext,
    ILogger<CurrentOperatorAuthorizationHandler> logger) : IAuthorizationHandler
{
    public async Task HandleAsync(AuthorizationHandlerContext authorizationContext)
    {
        ArgumentNullException.ThrowIfNull(authorizationContext);

        if (!authorizationContext.Requirements.Any(requirement =>
                requirement is CurrentOperatorRequirement or CurrentOperatorRoleRequirement) ||
            authorizationContext.Resource is not HttpContext httpContext ||
            authorizationContext.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        // Routing faults are not RouteEndpoint. Exit before any current-Operator DB lookup so
        // 404/405/415 never pay for Operator resolution and product RouteEndpoints never take this
        // bypass.
        if (httpContext.GetEndpoint() is not RouteEndpoint)
        {
            return;
        }

        CurrentOperatorSnapshot? currentOperator = await ResolveAsync(
                authorizationContext.User,
                httpContext,
                httpContext.RequestAborted)
            .ConfigureAwait(false);

        if (currentOperator is null)
        {
            return;
        }

        requestContext.SetCurrent(currentOperator);

        foreach (IAuthorizationRequirement requirement in authorizationContext.Requirements)
        {
            switch (requirement)
            {
                case CurrentOperatorRequirement:
                    authorizationContext.Succeed(requirement);
                    break;
                case CurrentOperatorRoleRequirement roleRequirement
                    when roleRequirement.PermittedRoles.Contains(currentOperator.Role):
                    authorizationContext.Succeed(requirement);
                    break;
                case CurrentOperatorRoleRequirement:
                    // Explicit failure so a pass-through handler cannot succeed an unmet role
                    // requirement.
                    authorizationContext.Fail();
                    break;
            }
        }
    }

    private async Task<CurrentOperatorSnapshot?> ResolveAsync(
        ClaimsPrincipal principal,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        Claim[] subjectClaims = principal.FindAll(JwtRegisteredClaimNames.Sub).ToArray();
        Claim[] versionClaims = principal.FindAll(AuthnClaimTypes.AuthorizationStateVersion).ToArray();

        if (subjectClaims.Length != 1 ||
            !Guid.TryParseExact(subjectClaims[0].Value, "D", out Guid operatorId) ||
            versionClaims.Length != 1 ||
            !int.TryParse(
                versionClaims[0].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int tokenAuthorizationStateVersion) ||
            tokenAuthorizationStateVersion <= 0)
        {
            RejectCurrentAuthentication(CurrentOperatorRejectionReason.InvalidRequiredClaims, httpContext);
            return null;
        }

        BankDbContext persistence = httpContext.RequestServices.GetRequiredService<BankDbContext>();
        CurrentOperatorSnapshot? currentOperator = await persistence.Operators
            .AsNoTracking()
            .Where(operatorEntity => operatorEntity.Id == operatorId)
            .Select(operatorEntity => new CurrentOperatorSnapshot(
                operatorEntity.Id,
                operatorEntity.State,
                operatorEntity.Role,
                operatorEntity.AuthorizationStateVersion))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (currentOperator is null)
        {
            RejectCurrentAuthentication(CurrentOperatorRejectionReason.OperatorNotFound, httpContext);
            return null;
        }

        if (currentOperator.State != OperatorState.Active)
        {
            RejectCurrentAuthentication(CurrentOperatorRejectionReason.OperatorDisabled, httpContext);
            return null;
        }

        if (currentOperator.AuthorizationStateVersion != tokenAuthorizationStateVersion)
        {
            RejectCurrentAuthentication(CurrentOperatorRejectionReason.AuthorizationStateVersionMismatch, httpContext);
            return null;
        }

        return currentOperator;
    }

    private void RejectCurrentAuthentication(
        CurrentOperatorRejectionReason reason,
        HttpContext httpContext)
    {
        requestContext.MarkAuthenticationInvalidated();
        CurrentOperatorSecurityLog.AuthenticationInvalidated(
            logger,
            reason.ToString(),
            httpContext.TraceIdentifier);
    }
}

internal sealed class CurrentOperatorRequestContext
{
    public CurrentOperatorSnapshot? CurrentOperator { get; private set; }

    public bool AuthenticationInvalidated { get; private set; }

    public void SetCurrent(CurrentOperatorSnapshot currentOperator)
    {
        ArgumentNullException.ThrowIfNull(currentOperator);

        if (AuthenticationInvalidated)
        {
            throw new InvalidOperationException(
                "An invalidated authentication state cannot become current during the same request.");
        }

        CurrentOperator = currentOperator;
    }

    public void MarkAuthenticationInvalidated()
    {
        CurrentOperator = null;
        AuthenticationInvalidated = true;
    }
}

internal sealed record CurrentOperatorSnapshot(
    Guid Identifier,
    OperatorState State,
    OperatorRole Role,
    int AuthorizationStateVersion);

internal enum CurrentOperatorRejectionReason
{
    InvalidRequiredClaims,
    OperatorNotFound,
    OperatorDisabled,
    AuthorizationStateVersionMismatch,
}

internal static partial class CurrentOperatorSecurityLog
{
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "Authenticated bearer was rejected by current Operator state. Reason: {Reason}. Correlation ID: {CorrelationId}.")]
    public static partial void AuthenticationInvalidated(
        ILogger logger,
        string reason,
        string correlationId);
}
