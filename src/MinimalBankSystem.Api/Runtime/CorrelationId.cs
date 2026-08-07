using System.Diagnostics.CodeAnalysis;

namespace MinimalBankSystem.Api.Runtime;

public static class CorrelationId
{
    public const string HeaderName = "X-Correlation-Id";
    public const string HttpContextItemKey = "CorrelationId";
    public const string LogScopeKey = "CorrelationId";

    public const int MaxLength = 128;

    public static string Create() => Guid.NewGuid().ToString("N");

    public static bool TryNormalize(string? candidate, [NotNullWhen(true)] out string? correlationId)
    {
        correlationId = null;

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        string trimmed = candidate.Trim();
        if (trimmed.Length is 0 or > MaxLength)
        {
            return false;
        }

        foreach (char c in trimmed)
        {
            if (IsAllowed(c))
            {
                continue;
            }

            return false;
        }

        correlationId = trimmed;
        return true;
    }

    public static string Resolve(string? callerSupplied)
    {
        return TryNormalize(callerSupplied, out string? normalized) ? normalized : Create();
    }

    private static bool IsAllowed(char c)
    {
        // Reject control characters and whitespace to avoid log/header injection.
        // Allow a conservative token alphabet only.
        return c is (>= 'A' and <= 'Z')
            or (>= 'a' and <= 'z')
            or (>= '0' and <= '9')
            or '-'
            or '_'
            or '.';
    }
}
