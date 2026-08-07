using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MinimalBankSystem.Api.Runtime;

/// <summary>
/// Service registration for the common API runtime contract.
/// </summary>
public static class ApiRuntimeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services every API endpoint shares: the injected <see cref="TimeProvider"/>
    /// required by ADR-0006 and the framework input validation response that keeps model binding
    /// failures inside the common error envelope.
    /// </summary>
    /// <remarks>
    /// No <see cref="IApiExceptionMapper"/> is registered here. Business error mapping belongs to
    /// the feature that owns the error and is added by the feature itself.
    /// </remarks>
    public static IServiceCollection AddApiRuntimeContract(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        // PostConfigure, because MVC installs its own ProblemDetails defaults from an
        // IConfigureOptions callback and would otherwise win depending on registration order.
        services.PostConfigure<ApiBehaviorOptions>(static options =>
        {
            options.InvalidModelStateResponseFactory = static _ =>
                // The rejected input is not echoed, so invalid payloads cannot carry caller supplied
                // values back into the response.
                new ObjectResult(new ApiErrorResponse(
                    ApiErrorCodes.ValidationFailed,
                    "The request contains invalid input."))
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                };

            // The RFC 7807 problem document contradicts the error envelope of specification section
            // 16.1, so the framework must not synthesise one. A failure that needs an envelope is
            // raised as an exception and mapped through IApiExceptionMapper.
            options.SuppressMapClientErrors = true;
        });

        return services;
    }

    /// <summary>
    /// Registers a feature owned exception mapper on the common exception to HTTP extension point.
    /// </summary>
    public static IServiceCollection AddApiExceptionMapper<TMapper>(this IServiceCollection services)
        where TMapper : class, IApiExceptionMapper
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IApiExceptionMapper, TMapper>();

        return services;
    }
}
