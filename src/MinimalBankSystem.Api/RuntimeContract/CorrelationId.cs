using Microsoft.Extensions.Primitives;

namespace MinimalBankSystem.Api.RuntimeContract;

public static class CorrelationId
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ContextItemKey = "MinimalBankSystem.CorrelationId";

    public static string Create()
    {
        return Guid.NewGuid().ToString("D");
    }

    public static string From(StringValues values)
    {
        if (values.Count == 1 && Guid.TryParseExact(values[0], "D", out Guid parsed))
        {
            return parsed.ToString("D");
        }

        return Create();
    }
}
