using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Application.Auditing;

namespace MinimalBankSystem.Api.OperatorMutation;

internal static class OperatorMutationAudit
{
    public const string EnableOperationIdentifier = "operator.command.enable";
    public const string DisableOperationIdentifier = "operator.command.disable";
    public const string RoleChangeOperationIdentifier = "operator.command.change-role";
}

public static class OperatorMutationServiceCollectionExtensions
{
    public static IServiceCollection AddOperatorMutation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new AuditOperationRegistration(OperatorMutationAudit.EnableOperationIdentifier));
        services.AddSingleton(new AuditOperationRegistration(OperatorMutationAudit.DisableOperationIdentifier));
        services.AddSingleton(new AuditOperationRegistration(OperatorMutationAudit.RoleChangeOperationIdentifier));
        services.AddScoped<IOperatorMutationSuccessCommitter, AtomicOperatorMutationSuccessCommitter>();
        services.AddScoped<IOperatorMutationLockStrategy, ActiveAdministratorSetLockStrategy>();
        return services;
    }
}

/// <summary>
/// Every mutation endpoint's Product-Audit target is the route-bound Operator canonical Guid,
/// resolved identically whether the eventual outcome is a policy 403 (AUTHZ-owned), a handler
/// rejection, or success. Login identifiers are never used as an Audit target.
/// </summary>
internal static class OperatorMutationAuditTargetResolver
{
    public static ValueTask<string?> ResolveAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        object? rawIdentifier = httpContext.Request.RouteValues["operatorIdentifier"];
        return Guid.TryParse(Convert.ToString(rawIdentifier, CultureInfo.InvariantCulture), out Guid identifier)
            ? ValueTask.FromResult<string?>(identifier.ToString("D"))
            : ValueTask.FromResult<string?>(null);
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
internal sealed class OperatorEnableAuthorizationAuditContextAttribute : Attribute, IAuthorizationAuditContext
{
    public string OperationIdentifier => OperatorMutationAudit.EnableOperationIdentifier;

    public ValueTask<string?> ResolveTargetIdentifierAsync(HttpContext httpContext) =>
        OperatorMutationAuditTargetResolver.ResolveAsync(httpContext);
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
internal sealed class OperatorDisableAuthorizationAuditContextAttribute : Attribute, IAuthorizationAuditContext
{
    public string OperationIdentifier => OperatorMutationAudit.DisableOperationIdentifier;

    public ValueTask<string?> ResolveTargetIdentifierAsync(HttpContext httpContext) =>
        OperatorMutationAuditTargetResolver.ResolveAsync(httpContext);
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
internal sealed class OperatorRoleChangeAuthorizationAuditContextAttribute : Attribute, IAuthorizationAuditContext
{
    public string OperationIdentifier => OperatorMutationAudit.RoleChangeOperationIdentifier;

    public ValueTask<string?> ResolveTargetIdentifierAsync(HttpContext httpContext) =>
        OperatorMutationAuditTargetResolver.ResolveAsync(httpContext);
}
