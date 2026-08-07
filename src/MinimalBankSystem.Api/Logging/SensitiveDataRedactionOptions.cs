namespace MinimalBankSystem.Api.Logging;

public sealed class SensitiveDataRedactionOptions
{
    public static readonly string[] DefaultProhibitedFieldNames =
    [
        "Password",
        "password",
        "JWT",
        "jwt",
        "Token",
        "token",
        "AccessToken",
        "access_token",
        "SigningKey",
        "signing_key",
        "IdempotencyKey",
        "idempotency_key",
        "ConnectionString",
        "connection_string",
    ];

    public ISet<string> ProhibitedFieldNames { get; set; } = new HashSet<string>(DefaultProhibitedFieldNames, StringComparer.OrdinalIgnoreCase);
}
