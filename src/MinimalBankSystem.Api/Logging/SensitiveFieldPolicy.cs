namespace MinimalBankSystem.Api.Logging;

public static class SensitiveFieldPolicy
{
    private static readonly HashSet<string> ProhibitedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "Password",
        "jwt",
        "JWT",
        "token",
        "Token",
        "signing_key",
        "SigningKey",
        "signingKey",
        "idempotency_key",
        "IdempotencyKey",
        "idempotencyKey",
        "connection_string",
        "ConnectionString",
        "connectionString",
    };

    public static bool IsProhibited(string key)
    {
        return ProhibitedKeys.Contains(key);
    }

    public static IReadOnlyCollection<string> ProhibitedKeyNames => ProhibitedKeys;
}
