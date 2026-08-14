using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.IntegrationTests.Persistence;

namespace MinimalBankSystem.IntegrationTests.Authentication;

/// <summary>
/// Verification Requirements 5-10 and 13-14: the authentication-only test-host probe proves the
/// JWT bearer gate (signature, issuer, audience, expiry, algorithm) using a positive
/// handler-reached signal, without depending on any AUTHZ current-Operator-state authority.
/// </summary>
[Collection(TestExecutionCollections.ConsoleSensitive)]
public sealed class AuthnAuthenticationTests
{
    private static readonly Guid SubjectOperatorId =
        Guid.Parse("018f4d25-8f93-7b48-8d85-7d0e7bb4ef01");

    [Fact]
    public async Task ValidJwtReachesTheAuthenticationOnlyProbeHandler()
    {
        using AuthenticationProbeApiFactory factory = new(TestJwtConfiguration.SigningKey);
        using HttpClient client = factory.CreateClient();
        string token = CreateToken();

        using HttpResponseMessage response = await SendProbeAsync(client, token);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.GetProperty("handlerReached").GetBoolean());
        Assert.Equal(SubjectOperatorId.ToString("D"), document.RootElement.GetProperty("subject").GetString());
    }

    [Fact]
    public async Task MissingBearerTokenDoesNotReachTheProbeHandler()
    {
        using AuthenticationProbeApiFactory factory = new(TestJwtConfiguration.SigningKey);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(AuthenticationProbeController.ProbePath);

        await AssertRejectedBeforeProbeHandlerAsync(response, "MISSING");
    }

    [Fact]
    public async Task InvalidSignatureIsRejectedBeforeTheProbeHandler()
    {
        using AuthenticationProbeApiFactory factory = new(TestJwtConfiguration.SigningKey);
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(signingKey: CreateOtherSigningKey());

        using HttpResponseMessage response = await SendProbeAsync(client, token);

        await AssertRejectedBeforeProbeHandlerAsync(response, "AUTHN-JWT-01");
    }

    [Fact]
    public async Task WrongIssuerIsRejectedBeforeTheProbeHandler()
    {
        using AuthenticationProbeApiFactory factory = new(TestJwtConfiguration.SigningKey);
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(issuer: "wrong-issuer");

        using HttpResponseMessage response = await SendProbeAsync(client, token);

        await AssertRejectedBeforeProbeHandlerAsync(response, "AUTHN-JWT-02");
    }

    [Fact]
    public async Task WrongAudienceIsRejectedBeforeTheProbeHandler()
    {
        using AuthenticationProbeApiFactory factory = new(TestJwtConfiguration.SigningKey);
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(audience: "wrong-audience");

        using HttpResponseMessage response = await SendProbeAsync(client, token);

        await AssertRejectedBeforeProbeHandlerAsync(response, "AUDIENCE");
    }

    [Fact]
    public async Task ExpiredTokenIsRejectedBeforeTheProbeHandler()
    {
        using AuthenticationProbeApiFactory factory = new(TestJwtConfiguration.SigningKey);
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(expires: DateTime.UtcNow.AddMinutes(-1));

        using HttpResponseMessage response = await SendProbeAsync(client, token);

        await AssertRejectedBeforeProbeHandlerAsync(response, "AUTHN-JWT-03");
    }

    [Fact]
    public async Task DisallowedSigningAlgorithmIsRejectedBeforeTheProbeHandler()
    {
        using AuthenticationProbeApiFactory factory = new(TestJwtConfiguration.SigningKey);
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(signingAlgorithm: SecurityAlgorithms.HmacSha384);

        using HttpResponseMessage response = await SendProbeAsync(client, token);

        await AssertRejectedBeforeProbeHandlerAsync(response, "ALGORITHM");
    }

    [Fact]
    public async Task JwtAndSigningKeyAreAbsentFromErrorUnrelatedResponseAndLogs()
    {
        using ConsoleCapture capture = new();
        using AuthenticationProbeApiFactory factory = new(TestJwtConfiguration.SigningKey);
        using HttpClient client = factory.CreateClient();
        string validJwt = CreateToken();
        using HttpResponseMessage validTokenResponse = await SendProbeAsync(client, validJwt);
        string validBody = await validTokenResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, validTokenResponse.StatusCode);
        using JsonDocument validDocument = JsonDocument.Parse(validBody);
        Assert.True(validDocument.RootElement.GetProperty("handlerReached").GetBoolean());

        string invalidJwt = validJwt[..^1] + (validJwt[^1] == 'a' ? 'b' : 'a');
        using HttpResponseMessage invalidTokenResponse = await SendProbeAsync(client, invalidJwt);
        using HttpResponseMessage unrelatedResponse = await client.GetAsync("/__contract/does-not-exist");

        string invalidBody = await invalidTokenResponse.Content.ReadAsStringAsync();
        string unrelatedBody = await unrelatedResponse.Content.ReadAsStringAsync();
        string rawSigningKey = Encoding.UTF8.GetString(Convert.FromBase64String(TestJwtConfiguration.SigningKey));

        foreach (string material in new[] { TestJwtConfiguration.SigningKey, rawSigningKey })
        {
            Assert.DoesNotContain(material, invalidBody, StringComparison.Ordinal);
            Assert.DoesNotContain(material, unrelatedBody, StringComparison.Ordinal);
            Assert.DoesNotContain(material, capture.Content, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(validJwt, invalidBody, StringComparison.Ordinal);
        Assert.DoesNotContain(validJwt, unrelatedBody, StringComparison.Ordinal);
        Assert.DoesNotContain(validJwt, capture.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(invalidJwt, invalidBody, StringComparison.Ordinal);
        Assert.DoesNotContain(invalidJwt, capture.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void TheProbeControllerDoesNotReferenceOperatorPersistenceOrAuthorizationState()
    {
        string controllerSource = File.ReadAllText(Path.Combine(
            RepositoryLayout.RepositoryRoot.FullName,
            "tests",
            "MinimalBankSystem.IntegrationTests",
            "Authentication",
            "AuthenticationProbeSupport.cs"));
        string programSource = File.ReadAllText(Path.Combine(
            RepositoryLayout.RepositoryRoot.FullName,
            "src",
            "MinimalBankSystem.Api",
            "Program.cs"));

        Assert.DoesNotContain("BankDbContext", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Operators", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AuthorizationStateVersion", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OperatorState", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("/__authn/probe", programSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsEnvironment(\"Testing\")", programSource, StringComparison.Ordinal);
        Assert.DoesNotContain(AuthenticationProbeController.ProbePath, programSource, StringComparison.Ordinal);
    }

    [Fact]
    public void IssuanceAndValidationUseTheSameCentralizedJwtParameters()
    {
        JwtAuthnOptions options = new()
        {
            SigningKey = TestJwtConfiguration.SigningKey,
        };
        Microsoft.IdentityModel.Tokens.TokenValidationParameters parameters = options.CreateValidationParameters();

        Assert.Equal("minimal-bank-system", options.Issuer);
        Assert.Equal("minimal-bank-system-api", options.Audience);
        Assert.Equal(300, options.AccessTokenLifetimeSeconds);
        Assert.Equal(options.Issuer, parameters.ValidIssuer);
        Assert.Equal(options.Audience, parameters.ValidAudience);
        Assert.Contains(SecurityAlgorithms.HmacSha256, parameters.ValidAlgorithms!);
        Assert.True(parameters.ValidateIssuerSigningKey);
        Assert.True(parameters.ValidateIssuer);
        Assert.True(parameters.ValidateAudience);
        Assert.True(parameters.ValidateLifetime);
        Assert.Equal(TimeSpan.Zero, parameters.ClockSkew);
    }

    private static async Task<HttpResponseMessage> SendProbeAsync(HttpClient client, string token)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, AuthenticationProbeController.ProbePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static async Task AssertRejectedBeforeProbeHandlerAsync(
        HttpResponseMessage response,
        string mutationId)
    {
        string body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode == HttpStatusCode.OK)
        {
            throw new Xunit.Sdk.XunitException(
                $"{mutationId}: invalid token reached the authentication-only handler: {body}");
        }

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("handlerReached", body, StringComparison.OrdinalIgnoreCase);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal("authentication_required", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("Authentication is required.", document.RootElement.GetProperty("message").GetString());
    }

    private static string CreateToken(
        string issuer = "minimal-bank-system",
        string audience = "minimal-bank-system-api",
        DateTime? expires = null,
        string signingAlgorithm = SecurityAlgorithms.HmacSha256,
        byte[]? signingKey = null)
    {
        DateTime now = DateTime.UtcNow;
        DateTime expiration = expires ?? now.AddMinutes(5);
        DateTime notBefore = expiration <= now
            ? expiration.AddMinutes(-1)
            : now.AddMinutes(-1);
        JwtSecurityToken token = new(
            issuer,
            audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, SubjectOperatorId.ToString("D")),
                new Claim(AuthnClaimTypes.AuthorizationStateVersion, "1"),
            ],
            notBefore,
            expiration,
            new SigningCredentials(
                new SymmetricSecurityKey(signingKey ?? Convert.FromBase64String(TestJwtConfiguration.SigningKey)),
                signingAlgorithm));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static byte[] CreateOtherSigningKey() =>
        Enumerable.Repeat((byte)0x5A, 32).ToArray();
}
