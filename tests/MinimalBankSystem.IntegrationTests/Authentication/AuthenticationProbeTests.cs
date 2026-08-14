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
public sealed class AuthenticationProbeTests
{
    internal const string TestSigningKey =
        "authn-probe-integration-test-signing-key-32-bytes-minimum!!";

    private static readonly Guid SubjectOperatorId = Guid.NewGuid();

    [Fact]
    public async Task ValidJwtReachesTheAuthenticationOnlyProbeHandler()
    {
        using AuthenticationProbeApiFactory factory = new(TestSigningKey);
        using HttpClient client = factory.CreateClient();
        string token = BuildToken();

        using HttpRequestMessage request = new(HttpMethod.Get, AuthenticationProbeController.ProbePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.GetProperty("handlerReached").GetBoolean());
        Assert.Equal(SubjectOperatorId.ToString(), document.RootElement.GetProperty("subject").GetString());
    }

    [Fact]
    public async Task MissingBearerTokenDoesNotReachTheProbeHandler()
    {
        using AuthenticationProbeApiFactory factory = new(TestSigningKey);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(AuthenticationProbeController.ProbePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public Task InvalidSignatureDoesNotReachTheProbeHandler() =>
        AssertRejectedAsync(BuildToken(signingKey: "a-completely-different-signing-key-value!!"));

    [Fact]
    public Task UnsignedNoneAlgorithmTokenDoesNotReachTheProbeHandler() =>
        AssertRejectedAsync(BuildUnsignedToken());

    [Fact]
    public Task WrongIssuerDoesNotReachTheProbeHandler() =>
        AssertRejectedAsync(BuildToken(issuer: "not-the-expected-issuer"));

    [Fact]
    public Task WrongAudienceDoesNotReachTheProbeHandler() =>
        AssertRejectedAsync(BuildToken(audience: "not-the-expected-audience"));

    [Fact]
    public Task ExpiredTokenDoesNotReachTheProbeHandler() =>
        AssertRejectedAsync(BuildToken(expired: true));

    [Fact]
    public Task DisallowedAlgorithmDoesNotReachTheProbeHandler() =>
        AssertRejectedAsync(BuildToken(algorithm: SecurityAlgorithms.HmacSha384));

    [Fact]
    public void TheProbeControllerDoesNotReferenceOperatorPersistenceOrAuthorizationState()
    {
        // Requirement 13: the AUTHN test-host probe must remain independent of AUTHZ's
        // authenticated request-time current-Operator/version/role authority.
        string controllerSource = File.ReadAllText(Path.Combine(
            RepositoryLayout.RepositoryRoot.FullName,
            "tests",
            "MinimalBankSystem.IntegrationTests",
            "Authentication",
            "AuthenticationProbeSupport.cs"));

        Assert.DoesNotContain("BankDbContext", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Operators", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AuthorizationStateVersion", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OperatorState", controllerSource, StringComparison.Ordinal);
    }

    private static async Task AssertRejectedAsync(string token)
    {
        using AuthenticationProbeApiFactory factory = new(TestSigningKey);
        using HttpClient client = factory.CreateClient();

        using HttpRequestMessage request = new(HttpMethod.Get, AuthenticationProbeController.ProbePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("handlerReached", body, StringComparison.OrdinalIgnoreCase);
    }

    internal static string BuildToken(
        string? signingKey = null,
        string? issuer = null,
        string? audience = null,
        bool expired = false,
        string algorithm = SecurityAlgorithms.HmacSha256)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(signingKey ?? TestSigningKey));
        SigningCredentials credentials = new(key, algorithm);

        DateTime now = DateTime.UtcNow;
        JwtSecurityToken token = new(
            issuer: issuer ?? JwtTokenSettings.Issuer,
            audience: audience ?? JwtTokenSettings.Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, SubjectOperatorId.ToString())],
            notBefore: expired ? now.AddMinutes(-30) : now,
            expires: expired ? now.AddMinutes(-15) : now.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// A classic "alg: none" unsigned token: no <see cref="SigningCredentials"/> at all, still
    /// bearing an otherwise-valid subject/issuer/audience/expiry. AUTHN-JWT-01 targets exactly
    /// this class of otherwise-invalid token: one whose signature is absent rather than merely
    /// mismatched.
    /// </summary>
    internal static string BuildUnsignedToken()
    {
        DateTime now = DateTime.UtcNow;
        JwtSecurityToken token = new(
            issuer: JwtTokenSettings.Issuer,
            audience: JwtTokenSettings.Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, SubjectOperatorId.ToString())],
            notBefore: now,
            expires: now.AddMinutes(15));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
