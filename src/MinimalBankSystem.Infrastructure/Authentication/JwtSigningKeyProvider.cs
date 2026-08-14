using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace MinimalBankSystem.Infrastructure.Authentication;

/// <summary>
/// Resolves the externally-injected JWT signing key. The key is never generated, defaulted, or
/// committed; it must be supplied outside the repository (environment or Docker secret) per
/// ADR-0007. The resolved value must never be logged or otherwise disclosed.
/// </summary>
public static class JwtSigningKeyProvider
{
    public static SymmetricSecurityKey Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? signingKey = configuration[JwtTokenSettings.SigningKeyEnvironmentVariable];
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException(
                $"No JWT signing key was configured. Set '{JwtTokenSettings.SigningKeyEnvironmentVariable}'. " +
                "The API never falls back to a default, generated, or committed key.");
        }

        byte[] keyBytes = Encoding.UTF8.GetBytes(signingKey);
        if (keyBytes.Length < JwtTokenSettings.MinimumSigningKeyLengthBytes)
        {
            throw new InvalidOperationException(
                $"The JWT signing key must be at least {JwtTokenSettings.MinimumSigningKeyLengthBytes} " +
                "bytes for HMAC-SHA256.");
        }

        return new SymmetricSecurityKey(keyBytes);
    }
}
