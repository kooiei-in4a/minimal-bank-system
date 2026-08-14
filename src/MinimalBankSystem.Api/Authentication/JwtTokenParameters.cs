using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace MinimalBankSystem.Api.Authentication;

/// <summary>
/// The one implementation-level source of truth for JWT issuance and validation parameters.
/// </summary>
public sealed class JwtTokenParameters
{
    public const string SigningKeyConfigurationKey = "Authentication:Jwt:SigningKey";
    public const string Issuer = "minimal-bank-system";
    public const string Audience = "minimal-bank-system-api";
    public const string AuthorizationStateVersionClaim = "authorization-state-version";
    public const string RoleClaim = "role";
    public const string AllowedAlgorithm = SecurityAlgorithms.HmacSha256;
    public const int AccessTokenLifetimeMinutes = 15;
    public static readonly TimeSpan AccessTokenLifetime =
        TimeSpan.FromMinutes(AccessTokenLifetimeMinutes);

    private readonly SymmetricSecurityKey? signingKey;

    private JwtTokenParameters(string? signingKeyValue)
    {
        if (string.IsNullOrWhiteSpace(signingKeyValue))
        {
            return;
        }

        if (Encoding.UTF8.GetByteCount(signingKeyValue) < 32)
        {
            throw new InvalidOperationException(
                "The JWT signing key must contain at least 256 bits of key material.");
        }

        signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKeyValue));
    }

    public static JwtTokenParameters Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new JwtTokenParameters(configuration[SigningKeyConfigurationKey]);
    }

    public void ConfigureBearer(JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [AllowedAlgorithm],
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = RoleClaim,
        };

        if (signingKey is null)
        {
            options.TokenValidationParameters.IssuerSigningKeyResolver =
                static (_, _, _, _) => [];
        }
    }

    public SigningCredentials CreateSigningCredentials() =>
        signingKey is null
            ? throw new InvalidOperationException(
                $"No JWT signing key was configured. Set '{SigningKeyConfigurationKey}'.")
            : new SigningCredentials(signingKey, AllowedAlgorithm);
}

public sealed class JwtTokenIssuer(
    JwtTokenParameters parameters,
    TimeProvider timeProvider)
{
    public IssuedJwtToken Issue(
        Guid operatorId,
        int authorizationStateVersion,
        string role)
    {
        DateTimeOffset issuedAt = timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = issuedAt.Add(JwtTokenParameters.AccessTokenLifetime);

        JwtSecurityToken token = new(
            issuer: JwtTokenParameters.Issuer,
            audience: JwtTokenParameters.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, operatorId.ToString("D")),
                new Claim(
                    JwtTokenParameters.AuthorizationStateVersionClaim,
                    authorizationStateVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(JwtTokenParameters.RoleClaim, role),
            ],
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: parameters.CreateSigningCredentials());

        string serialized = new JwtSecurityTokenHandler().WriteToken(token);
        return new IssuedJwtToken(serialized, expiresAt);
    }
}

public sealed record IssuedJwtToken(string AccessToken, DateTimeOffset ExpiresAt);
