using System.Globalization;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Application.Auditing;

namespace MinimalBankSystem.Api.OperatorQuery;

public static class OperatorQueryOperations
{
    public const string List = "operator.query.list";
    public const string Detail = "operator.query.detail";
    public const string CollectionTarget = "operators";
    public const string OperatorIdRouteValue = "operatorId";
}

internal static class OperatorQueryServiceCollectionExtensions
{
    public static IServiceCollection AddOperatorQuery(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new AuditOperationRegistration(OperatorQueryOperations.List));
        services.AddSingleton(new AuditOperationRegistration(OperatorQueryOperations.Detail));
        return services;
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
internal sealed class OperatorQueryAuthorizationAuditContextAttribute(
    string operationIdentifier,
    bool detailTarget) : Attribute, IAuthorizationAuditContext
{
    private readonly bool detailTarget = detailTarget;

    public string OperationIdentifier { get; } =
        string.IsNullOrWhiteSpace(operationIdentifier)
            ? throw new ArgumentException("An Audit operation identifier is required.", nameof(operationIdentifier))
            : operationIdentifier;

    public ValueTask<string?> ResolveTargetIdentifierAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!detailTarget)
        {
            return ValueTask.FromResult<string?>(OperatorQueryOperations.CollectionTarget);
        }

        object? routeValue = httpContext.Request.RouteValues[OperatorQueryOperations.OperatorIdRouteValue];
        string? rawIdentifier = Convert.ToString(routeValue, CultureInfo.InvariantCulture);

        return Guid.TryParse(rawIdentifier, out Guid identifier)
            ? ValueTask.FromResult<string?>(identifier.ToString("D"))
            : ValueTask.FromResult<string?>(null);
    }
}
