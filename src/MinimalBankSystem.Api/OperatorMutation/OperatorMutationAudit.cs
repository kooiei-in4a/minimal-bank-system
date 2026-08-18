using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Application.Auditing;

namespace MinimalBankSystem.Api.OperatorMutation;

internal static class OperatorMutationAudit
{
    public const string EnableOperationIdentifier = "operator.command.enable";
    public const string DisableOperationIdentifier = "operator.command.disable";
    public const string ChangeRoleOperationIdentifier = "operator.command.change-role";

    public static string GetOperationIdentifier(OperatorMutationKind operation) => operation switch
    {
        OperatorMutationKind.Enable => EnableOperationIdentifier,
        OperatorMutationKind.Disable => DisableOperationIdentifier,
        OperatorMutationKind.ChangeRole => ChangeRoleOperationIdentifier,
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown Operator mutation."),
    };
}

public static class OperatorMutationServiceCollectionExtensions
{
    public static IServiceCollection AddOperatorMutation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new AuditOperationRegistration(OperatorMutationAudit.EnableOperationIdentifier));
        services.AddSingleton(new AuditOperationRegistration(OperatorMutationAudit.DisableOperationIdentifier));
        services.AddSingleton(new AuditOperationRegistration(OperatorMutationAudit.ChangeRoleOperationIdentifier));
        services.AddScoped<IOperatorMutationService, OperatorMutationService>();
        services.AddScoped<IOperatorMutationSuccessCommitter, AtomicOperatorMutationSuccessCommitter>();
        return services;
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
internal sealed class OperatorMutationAuthorizationAuditContextAttribute(OperatorMutationKind operation)
    : Attribute, IAuthorizationAuditContext
{
    public string OperationIdentifier => OperatorMutationAudit.GetOperationIdentifier(operation);

    public ValueTask<string?> ResolveTargetIdentifierAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        object? rawIdentifier = httpContext.Request.RouteValues["operatorIdentifier"];
        return Guid.TryParse(
                Convert.ToString(rawIdentifier, CultureInfo.InvariantCulture),
                out Guid identifier)
            ? ValueTask.FromResult<string?>(identifier.ToString("D"))
            : ValueTask.FromResult<string?>(null);
    }
}
