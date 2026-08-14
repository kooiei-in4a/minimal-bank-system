using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Infrastructure.Authentication;

/// <summary>
/// Issues short-lived access tokens for successfully authenticated, active Operators. No refresh
/// token is introduced. The token contains the Operator subject and the ADR-0007 versioned
/// authorization-state value; any role claim is diagnostic-only and non-authoritative.
/// </summary>
public sealed class JwtTokenIssuer
{
    private readonly SymmetricSecurityKey signingKey;
    private readonly TimeProvider timeProvider;

    public JwtTokenIssuer(IConfiguration configuration, TimeProvider timeProvider)
    {
        signingKey = JwtSigningKeyProvider.Resolve(configuration);
        this.timeProvider = timeProvider;
    }

    public string IssueAccessToken(Operator operatorEntity)
    {
        ArgumentNullException.ThrowIfNull(operatorEntity);

        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        SigningCredentials credentials = new(signingKey, JwtTokenSettings.SigningAlgorithm);

        Claim[] claims =
        [
            new Claim(JwtRegisteredClaimNames.Sub, operatorEntity.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(
                JwtTokenSettings.AuthorizationStateVersionClaimType,
                operatorEntity.AuthorizationStateVersion.ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer),
            new Claim(JwtTokenSettings.RoleClaimType, operatorEntity.Role.ToString()),
        ];

        JwtSecurityToken token = new(
            issuer: JwtTokenSettings.Issuer,
            audience: JwtTokenSettings.Audience,
            claims: claims,
            notBefore: utcNow,
            expires: utcNow.Add(JwtTokenSettings.AccessTokenLifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
