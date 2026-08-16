using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Application.Auditing;

namespace MinimalBankSystem.Api.OperatorCreate;

internal static class OperatorCreateAudit
{
    public const string OperationIdentifier = "operator.command.create";
    public const string TargetIdentifier = "operators";
}

public static class OperatorCreateServiceCollectionExtensions
{
    public static IServiceCollection AddOperatorCreate(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(
            new AuditOperationRegistration(OperatorCreateAudit.OperationIdentifier));
        services.AddScoped<IOperatorCreateSuccessCommitter, AtomicOperatorCreateSuccessCommitter>();
        return services;
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
internal sealed class OperatorCreateAuthorizationAuditContextAttribute
    : Attribute, IAuthorizationAuditContext
{
    public string OperationIdentifier => OperatorCreateAudit.OperationIdentifier;

    public ValueTask<string?> ResolveTargetIdentifierAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return ValueTask.FromResult<string?>(OperatorCreateAudit.TargetIdentifier);
    }
}
