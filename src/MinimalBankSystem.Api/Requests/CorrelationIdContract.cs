using Microsoft.Extensions.Primitives;

namespace MinimalBankSystem.Api.Requests;

public static class CorrelationIdContract
{
    public const string HeaderName = "X-Correlation-ID";

    internal static string NormalizeOrCreate(StringValues suppliedValues)
    {
        if (suppliedValues.Count == 1)
        {
            string? suppliedValue = suppliedValues[0];

            if (Guid.TryParseExact(suppliedValue, "N", out Guid parsed) ||
                Guid.TryParseExact(suppliedValue, "D", out parsed))
            {
                return parsed.ToString("N");
            }
        }

        return Guid.NewGuid().ToString("N");
    }
}
