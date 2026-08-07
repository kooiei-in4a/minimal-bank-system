namespace MinimalBankSystem.Api.Runtime;

/// <summary>
/// Pipeline registration for the common API runtime contract.
/// </summary>
public static class ApiRuntimeApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the shared request execution contract to the pipeline.
    /// </summary>
    /// <remarks>
    /// Correlation runs first so that a failure handled by the error contract is already correlated
    /// and already reachable from the response header. Call this before any endpoint specific
    /// middleware so that the whole API surface is covered by one contract.
    /// </remarks>
    public static IApplicationBuilder UseApiRuntimeContract(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ApiExceptionHandlingMiddleware>();

        return app;
    }
}
