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
        string correlationId = CorrelationId.Resolve(
            context.Request.Headers[CorrelationId.HeaderName].FirstOrDefault());

        context.Items[CorrelationId.HttpContextItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationId.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        ILogger<CorrelationIdMiddleware> logger = context.RequestServices
            .GetRequiredService<ILogger<CorrelationIdMiddleware>>();

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            [CorrelationId.LogPropertyName] = correlationId,
        }))
        {
            await _next(context);
        }
    }
}
