namespace MinimalBankSystem.Api.Runtime;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string? callerSupplied = context.Request.Headers[CorrelationId.HeaderName].FirstOrDefault();
        string correlationId = CorrelationId.Resolve(callerSupplied);

        context.Items[CorrelationId.HttpContextItemKey] = correlationId;
        context.Response.Headers[CorrelationId.HeaderName] = correlationId;
        context.TraceIdentifier = correlationId;

        using (context.RequestServices
                   .GetRequiredService<ILoggerFactory>()
                   .CreateLogger("MinimalBankSystem.Api.Correlation")
                   .BeginScope(new Dictionary<string, object>
                   {
                       [CorrelationId.LogScopeKey] = correlationId,
                   }))
        {
            await _next(context);
        }
    }
}
