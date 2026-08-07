namespace MinimalBankSystem.Api.Middleware;

public static class CorrelationIdHttpContextExtensions
{
    public static string GetCorrelationId(this HttpContext context)
    {
        if (context.Items.TryGetValue(CorrelationIdMiddleware.ItemsKey, out object? value) && value is string correlationId)
        {
            return correlationId;
        }

        throw new InvalidOperationException($"{nameof(CorrelationIdMiddleware)} must run before the correlation ID is read.");
    }
}
