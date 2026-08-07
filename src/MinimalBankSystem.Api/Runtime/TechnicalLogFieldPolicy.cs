namespace MinimalBankSystem.Api.Runtime;

public static class TechnicalLogFieldPolicy
{
    private static readonly string[] ProhibitedKeyFragments =
    [
        "password",
        "jwt",
        "signingkey",
        "signing_key",
        "idempotencykey",
        "idempotency_key",
        "connectionstring",
        "connection_string",
    ];

    public static bool IsProhibitedKey(string key)
    {
        string normalized = NormalizeKey(key);
        foreach (string fragment in ProhibitedKeyFragments)
        {
            if (normalized.Contains(fragment, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<KeyValuePair<string, object?>> SanitizeState(
        IEnumerable<KeyValuePair<string, object?>> state)
    {
        List<KeyValuePair<string, object?>> sanitized = [];
        foreach (KeyValuePair<string, object?> entry in state)
        {
            if (!IsProhibitedKey(entry.Key))
            {
                sanitized.Add(entry);
            }
        }

        return sanitized;
    }

    public static string SanitizeMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }

        // Structured logging templates are safe; reject messages that embed sentinel payloads.
        foreach (string fragment in ProhibitedKeyFragments)
        {
            if (message.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return "Technical log message suppressed because it contained a prohibited field name.";
            }
        }

        return message;
    }

    private static string NormalizeKey(string key)
    {
        return key
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
    }
}
