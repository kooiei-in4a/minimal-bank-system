using Microsoft.Extensions.Primitives;

namespace MinimalBankSystem.Api.Runtime;

/// <summary>
/// Establishes exactly one correlation identifier per request, publishes it on the response and
/// scopes every technical log written while the request runs.
/// </summary>
/// <remarks>
/// The identifier is stored in <see cref="HttpContext.TraceIdentifier"/> so that application code,
/// the error contract and framework diagnostics all observe the same value. A caller supplied value
/// is reused only when <see cref="CorrelationIdPolicy"/> accepts it; anything else is replaced by a
/// generated identifier.
/// </remarks>
internal sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = Resolve(context.Request);
        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(
            static state =>
            {
                HttpContext started = (HttpContext)state;
                started.Response.Headers[CorrelationIdPolicy.HeaderName] = started.TraceIdentifier;
                return Task.CompletedTask;
            },
            context);

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    private static string Resolve(HttpRequest request)
    {
        StringValues supplied = request.Headers[CorrelationIdPolicy.HeaderName];

        return supplied.Count == 1 && CorrelationIdPolicy.IsAcceptable(supplied[0])
            ? supplied[0]!
            : CorrelationIdPolicy.Create();
    }
}
