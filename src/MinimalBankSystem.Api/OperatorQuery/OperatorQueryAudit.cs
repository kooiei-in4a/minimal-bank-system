using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Application.Auditing;

namespace MinimalBankSystem.Api.OperatorQuery;

internal static class OperatorQueryAudit
{
    public const string ListOperationIdentifier = "operator.query.list";
    public const string DetailOperationIdentifier = "operator.query.detail";
    public const string CollectionTargetIdentifier = "operators";
}

public static class OperatorQueryServiceCollectionExtensions
{
    public static IServiceCollection AddOperatorQuery(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(
            new AuditOperationRegistration(OperatorQueryAudit.ListOperationIdentifier));
        services.AddSingleton(
            new AuditOperationRegistration(OperatorQueryAudit.DetailOperationIdentifier));
        return services;
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
internal sealed class OperatorListAuthorizationAuditContextAttribute
    : Attribute, IAuthorizationAuditContext
{
    public string OperationIdentifier => OperatorQueryAudit.ListOperationIdentifier;

    public ValueTask<string?> ResolveTargetIdentifierAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return ValueTask.FromResult<string?>(OperatorQueryAudit.CollectionTargetIdentifier);
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
internal sealed class OperatorDetailAuthorizationAuditContextAttribute
    : Attribute, IAuthorizationAuditContext
{
    public string OperationIdentifier => OperatorQueryAudit.DetailOperationIdentifier;

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
