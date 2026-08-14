using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace MinimalBankSystem.Infrastructure.Authentication;

public sealed class JwtAuthnOptions
{
    public const string SectionName = "Authentication:Jwt";
    public const string SigningKeyConfigurationKey = SectionName + ":SigningKey";
    public const string SigningKeyFileConfigurationKey = SectionName + ":SigningKeyFile";

    public string Issuer { get; set; } = "minimal-bank-system";

    public string Audience { get; set; } = "minimal-bank-system-api";

    public int AccessTokenLifetimeSeconds { get; set; } = 300;

    public string? SigningKey { get; set; }

    public string? SigningKeyFile { get; set; }

    public bool TryGetSigningKeyBytes(out byte[] keyBytes)
    {
        keyBytes = [];
        string? configuredValue = SigningKey;

        try
        {
            if (string.IsNullOrWhiteSpace(configuredValue) && !string.IsNullOrWhiteSpace(SigningKeyFile))
            {
                configuredValue = File.ReadAllText(SigningKeyFile).Trim();
            }

            if (string.IsNullOrWhiteSpace(configuredValue))
            {
                return false;
            }

            keyBytes = Convert.FromBase64String(configuredValue);
            return keyBytes.Length >= 32;
        }
        catch (IOException)
        {
            keyBytes = [];
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            keyBytes = [];
            return false;
        }
        catch (FormatException)
        {
            keyBytes = [];
            return false;
        }
    }

    public TokenValidationParameters CreateValidationParameters()
    {
        if (!TryGetSigningKeyBytes(out byte[] keyBytes))
        {
            throw new InvalidOperationException(
                "An external JWT signing key is not configured or is not a valid base64 secret.");
        }

        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            NameClaimType = JwtRegisteredClaimNames.Sub,
        };
    }

    public SigningCredentials CreateSigningCredentials()
    {
        if (!TryGetSigningKeyBytes(out byte[] keyBytes))
        {
            throw new InvalidOperationException(
                "An external JWT signing key is not configured or is not a valid base64 secret.");
        }

        return new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);
    }
}
