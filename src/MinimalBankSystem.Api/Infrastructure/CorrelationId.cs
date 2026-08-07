namespace MinimalBankSystem.Api.Infrastructure;

public static class CorrelationId
{
    public const string HeaderName = "X-Correlation-ID";
    public const string HttpContextItemKey = "MinimalBankSystem.CorrelationId";

    public static string Create() => Guid.NewGuid().ToString("D");

    public static bool TryNormalize(string? candidate, out string normalized)
    {
        normalized = string.Empty;

        if (candidate is null || candidate.Length != 36)
        {
            return false;
        }

        if (!Guid.TryParseExact(candidate, "D", out Guid value) || value == Guid.Empty)
        {
            return false;
        }

        normalized = value.ToString("D");
        return true;
    }
}
