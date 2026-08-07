namespace MinimalBankSystem.Api.Runtime;

public static class CorrelationId
{
    public const string HeaderName = "X-Correlation-Id";
    public const string HttpContextItemKey = "CorrelationId";
    public const string LogPropertyName = "CorrelationId";

    public const int MaxLength = 128;

    public static string Generate() => Guid.NewGuid().ToString("D");

    public static bool IsValidCallerSupplied(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Length > MaxLength)
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!IsAllowedCharacter(character))
            {
                return false;
            }
        }

        return true;
    }

    public static string Resolve(string? callerSupplied)
    {
        return IsValidCallerSupplied(callerSupplied)
            ? callerSupplied!
            : Generate();
    }

    private static bool IsAllowedCharacter(char character)
    {
        return char.IsAsciiLetterOrDigit(character)
            || character is '-' or '_' or '.';
    }
}
