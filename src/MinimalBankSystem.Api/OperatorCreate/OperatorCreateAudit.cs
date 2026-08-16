using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Application.Auditing;

namespace MinimalBankSystem.Api.OperatorCreate;

internal static class OperatorCreateAudit
{
    public const string OperationIdentifier = "operator.command.create";
    public const string CollectionTargetIdentifier = "operators";
}

public static class OperatorCreateServiceCollectionExtensions
{
    public static IServiceCollection AddOperatorCreate(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(
            new AuditOperationRegistration(OperatorCreateAudit.OperationIdentifier));
        services.AddScoped<IOperatorCreateExecutor, OperatorCreateExecutor>();
        services.Configure<MvcOptions>(options =>
            options.Conventions.Add(new OperatorCreateAllowHandlerOwnedValidationConvention()));
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
        return ValueTask.FromResult<string?>(OperatorCreateAudit.CollectionTargetIdentifier);
    }
}

/// <summary>
/// Operator create must record its own handler-rejection Audit for missing/invalid input.
/// The automatic [ApiController] 400 filter would otherwise answer before the feature handler.
/// </summary>
internal sealed class OperatorCreateAllowHandlerOwnedValidationConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        ArgumentNullException.ThrowIfNull(application);

        foreach (ActionModel action in application.Controllers
            .SelectMany(controller => controller.Actions)
            .Where(action => action.ActionMethod.DeclaringType == typeof(OperatorCreateController)))
        {
            for (int index = action.Filters.Count - 1; index >= 0; index--)
            {
                string filterName = action.Filters[index].GetType().Name;
                if (filterName.Contains("ModelStateInvalid", StringComparison.Ordinal))
                {
                    action.Filters.RemoveAt(index);
                }
            }
        }
    }
}
