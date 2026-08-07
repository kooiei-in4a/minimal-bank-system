namespace MinimalBankSystem.Api.Logging;

public static class SensitiveLogFieldPolicy
{
    public const string RedactedValue = "[REDACTED]";

    private static readonly HashSet<string> ProhibitedFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "jwt",
        "signingkey",
        "idempotencykey",
        "connectionstring",
    };

    public static IReadOnlyDictionary<string, object?> Sanitize(IReadOnlyDictionary<string, object?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        Dictionary<string, object?> sanitized = new(fields.Count);
        foreach (KeyValuePair<string, object?> field in fields)
        {
            sanitized[field.Key] = IsProhibited(field.Key) ? RedactedValue : field.Value;
        }

        return sanitized;
    }

    private static bool IsProhibited(string fieldName)
    {
        string normalized = new(fieldName.Where(char.IsLetterOrDigit).ToArray());
        return ProhibitedFieldNames.Contains(normalized);
    }
}
