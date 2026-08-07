namespace MinimalBankSystem.Api.Runtime;

/// <summary>
/// Access to values established by the common API runtime contract.
/// </summary>
public static class ApiRuntimeHttpContextExtensions
{
    /// <summary>
    /// Returns the correlation identifier established for the current request.
    /// </summary>
    public static string GetCorrelationId(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.TraceIdentifier;
    }
}
