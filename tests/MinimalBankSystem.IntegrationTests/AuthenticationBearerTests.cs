using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Api.Authentication;
using MinimalBankSystem.IntegrationTests.PostgreSql;

namespace MinimalBankSystem.IntegrationTests;

public sealed class AuthenticationBearerTests
{
    private const string SigningKey =
        "AUTHN_TEST_SIGNING_KEY_0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task ValidTokenReachesTheAuthenticationOnlyProbe()
    {
        await using AuthenticationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await SendProbeAsync(
            client,
            CreateToken(SigningKey));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("reached", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidSignatureDoesNotReachTheAuthenticationOnlyProbe()
    {
        await AssertRejectedAsync(CreateToken(SigningKey + "wrong"));
    }

    [Fact]
    public async Task WrongIssuerDoesNotReachTheAuthenticationOnlyProbe()
    {
        await AssertRejectedAsync(CreateToken(SigningKey, issuer: "wrong-issuer"));
    }

    [Fact]
    public async Task ExpiredTokenDoesNotReachTheAuthenticationOnlyProbe()
    {
        await AssertRejectedAsync(
            CreateToken(SigningKey, expiresAt: DateTime.UtcNow.AddMinutes(-1)));
    }

    [Fact]
    public async Task WrongAudienceDoesNotReachTheAuthenticationOnlyProbe()
    {
        await AssertRejectedAsync(CreateToken(SigningKey, audience: "wrong-audience"));
    }

    [Fact]
    public async Task DisallowedAlgorithmDoesNotReachTheAuthenticationOnlyProbe()
    {
        await AssertRejectedAsync(
            CreateToken(SigningKey, algorithm: SecurityAlgorithms.HmacSha384));
    }

    private static AuthenticationWebApplicationFactory CreateFactory() =>
        new(connectionString: null, signingKey: SigningKey);

    private static async Task AssertRejectedAsync(string token)
    {
        await using AuthenticationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendProbeAsync(client, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(
            "reached",
            await response.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpResponseMessage> SendProbeAsync(
        HttpClient client,
        string token)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/__authn/probe");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static string CreateToken(
        string signingKey,
        string issuer = JwtTokenParameters.Issuer,
        string audience = JwtTokenParameters.Audience,
        DateTime? expiresAt = null,
        string algorithm = JwtTokenParameters.AllowedAlgorithm)
    {
        DateTime now = DateTime.UtcNow;
        JwtSecurityToken token = new(
            issuer,
            audience,
            [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString("D"))],
            now.AddMinutes(-2),
            expiresAt ?? now.AddMinutes(5),
            new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                algorithm));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
