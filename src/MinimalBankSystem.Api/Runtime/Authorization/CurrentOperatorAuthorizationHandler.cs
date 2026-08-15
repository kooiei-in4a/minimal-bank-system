using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Authorization;

namespace MinimalBankSystem.Api.Runtime.Authorization;

/// <summary>
/// Enforces the current-Operator-state and current-role requirements against the persisted
/// Operator snapshot. The JWT is never a role authority (ADR-0007): every authorized request
/// resolves the current DB row once, and the result is cached in the scoped
/// <see cref="CurrentOperatorContext"/> for the result handler.
/// </summary>
/// <remarks>
/// The resolver and the operator context are resolved lazily from the request scope. A ctor
/// dependency would force every request (including anonymous ones and endpoints that never
/// require an Operator) to construct the DbContext through the handler-provider IEnumerable
/// resolution, turning a missing connection string into a 500 for the whole pipeline. It would
/// also capture a root-scoped context instance, so the resolution written by the handler would
/// never be visible to the request-scoped result handler.
/// </remarks>
public sealed class CurrentOperatorAuthorizationHandler(
    ILogger<CurrentOperatorAuthorizationHandler> logger) : IAuthorizationHandler
{
    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        if (context.Resource is not HttpContext httpContext ||
            httpContext.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (!context.PendingRequirements.Any(requirement =>
                requirement is CurrentOperatorRequirement or OperatorRoleRequirement))
        {
            return;
        }

        CurrentOperatorContext operatorContext = httpContext.RequestServices
            .GetRequiredService<CurrentOperatorContext>();

        if (operatorContext.Resolution is null)
        {
            string? subject = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            string? versionClaim =
                httpContext.User.FindFirst(AuthnClaimTypes.AuthorizationStateVersion)?.Value;

            if (!Guid.TryParse(subject, out Guid operatorId) ||
                !int.TryParse(
                    versionClaim,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int presentedVersion))
            {
                AuthorizationTechnicalLog.OperatorResolutionFailed(
                    logger,
                    "invalid-principal-claims",
                    httpContext.TraceIdentifier);
                operatorContext.SetResolution(CurrentOperatorResolution.NotFound);
                context.Fail();
                return;
            }

            CurrentOperatorResolver resolver = httpContext.RequestServices
                .GetRequiredService<CurrentOperatorResolver>();

            operatorContext.SetResolution(await resolver.ResolveAsync(
                operatorId,
                presentedVersion,
                httpContext.RequestAborted));
        }

        CurrentOperatorResolution resolution = operatorContext.Resolution!;
        if (resolution.Status is not CurrentOperatorResolutionStatus.Success)
        {
            AuthorizationTechnicalLog.OperatorResolutionFailed(
                logger,
                resolution.Status.ToString(),
                httpContext.TraceIdentifier);
            context.Fail();
            return;
        }

        Operator currentOperator = resolution.Operator!;

        foreach (IAuthorizationRequirement requirement in context.PendingRequirements.ToList())
        {
            if (requirement is CurrentOperatorRequirement)
            {
                context.Succeed(requirement);
            }
            else if (requirement is OperatorRoleRequirement roleRequirement &&
                     AuthoritativeRole(currentOperator) is { } role &&
                     roleRequirement.AllowedRoles.Contains(role))
            {
                context.Succeed(requirement);
            }
            else if (requirement is OperatorRoleRequirement)
            {
                // The current persisted role lacks the policy roles. Fail explicitly so the
                // framework pass-through handler can never succeed an untested requirement.
                context.Fail();
            }
        }
    }

    private static OperatorRole? AuthoritativeRole(Operator currentOperator) => currentOperator.Role;
}
