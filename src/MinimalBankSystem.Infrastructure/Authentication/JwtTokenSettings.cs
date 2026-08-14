using Microsoft.IdentityModel.Tokens;

namespace MinimalBankSystem.Infrastructure.Authentication;

/// <summary>
/// The single centralized set of JWT issuance/validation parameters, used consistently by
/// <see cref="JwtTokenIssuer"/>, the JwtBearer validator configured in the API host, and tests.
/// ADR-0007 and Issue WP2-AUTHN-01 fix the short-lived-token and external-key requirements and the
/// required claim set; the concrete issuer/audience/lifetime/algorithm values below are
/// implementation-level selections, not a new product specification or ADR.
/// </summary>
public static class JwtTokenSettings
{
    public const string Issuer = "minimal-bank-system";
    public const string Audience = "minimal-bank-system-api";
    public const string SigningAlgorithm = SecurityAlgorithms.HmacSha256;
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    /// <summary>Environment-variable form of the externally-injected JWT signing key.</summary>
    public const string SigningKeyEnvironmentVariable = "MBS_JWT_SIGNING_KEY";

    /// <summary>Minimum signing-key length in bytes required for HMAC-SHA256.</summary>
    public const int MinimumSigningKeyLengthBytes = 32;

    /// <summary>ADR-0007 versioned authorization-state claim consumed later by AUTHZ.</summary>
    public const string AuthorizationStateVersionClaimType = "mbs_authorization_state_version";

    /// <summary>Diagnostic-only role claim. Never authoritative for authorization decisions.</summary>
    public const string RoleClaimType = "mbs_role";
}
