namespace MinimalBankSystem.Api.Runtime;

/// <summary>
/// Prohibited technical-log field policy per ADR-0008.
/// Sensitive values must not be written to technical logs at all (not merely masked).
/// </summary>
public static class TechnicalLogFieldPolicy
{
    public static readonly IReadOnlyList<string> ProhibitedCategories =
    [
        "password",
        "jwt",
        "signing key",
        "raw idempotency key",
        "connection string",
        "unnecessary personal data",
    ];

    /// <summary>
    /// Headers that must never be copied into technical logs.
    /// </summary>
    public static readonly IReadOnlyList<string> ProhibitedRequestHeaderNames =
    [
        "Authorization",
        "Cookie",
        "Idempotency-Key",
        "X-Idempotency-Key",
        "X-Signing-Key",
        "X-Connection-String",
    ];
}
