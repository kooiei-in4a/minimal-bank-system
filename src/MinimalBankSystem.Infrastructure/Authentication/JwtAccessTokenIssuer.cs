using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace MinimalBankSystem.Infrastructure.Authentication;

public interface IJwtAccessTokenIssuer
{
    string Issue(AuthnLoginResult login);
}

public sealed class JwtAccessTokenIssuer(
    IOptions<JwtAuthnOptions> options,
    TimeProvider timeProvider) : IJwtAccessTokenIssuer
{
    public string Issue(AuthnLoginResult login)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        JwtAuthnOptions settings = options.Value;
        DateTimeOffset expires = now.AddSeconds(settings.AccessTokenLifetimeSeconds);

        JwtSecurityToken token = new(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, login.OperatorId.ToString("D")),
                new Claim(
                    AuthnClaimTypes.AuthorizationStateVersion,
                    login.AuthorizationStateVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ],
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: settings.CreateSigningCredentials());

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
