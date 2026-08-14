extern alias api;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Persistence.Identity;

namespace MinimalBankSystem.IntegrationTests.Authentication;

[Collection(TestExecutionCollections.ConsoleSensitive)]
public sealed class AuthnAuthenticationTests
{
    private const string ActiveUserName = "active.operator";
    private const string ActivePassword = "active-password";
    private static readonly Guid ActiveOperatorId =
        Guid.Parse("018f4d25-8f93-7b48-8d85-7d0e7bb4ef01");

    [Fact]
    public async Task ActiveLoginReturnsJwtClaimsAndAuthenticationOnlyProbeIsReached()
    {
        FixtureOperatorStore store = new();
        store.Add(
            ActiveUserName,
            ActivePassword,
            OperatorState.Active,
            authorizationStateVersion: 7,
            ActiveOperatorId);

        using AuthnWebApplicationFactory factory = new(store);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage login = await LoginAsync(client, ActiveUserName, ActivePassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        string token = await ReadAccessTokenAsync(login);

        JwtSecurityToken decoded = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(ActiveOperatorId.ToString("D"), decoded.Subject);
        Assert.Equal("7", decoded.Claims.Single(claim => claim.Type == AuthnClaimTypes.AuthorizationStateVersion).Value);
        Assert.True(decoded.ValidTo > DateTime.UtcNow);
        Assert.DoesNotContain(TestJwtConfiguration.SigningKey, token, StringComparison.Ordinal);

        using HttpResponseMessage probe = await SendProbeAsync(client, token);
        Assert.Equal(HttpStatusCode.OK, probe.StatusCode);
        string probeBody = await probe.Content.ReadAsStringAsync();
        Assert.Contains("authenticationHandlerReached", probeBody, StringComparison.Ordinal);
        Assert.Contains(ActiveOperatorId.ToString("D"), probeBody, StringComparison.Ordinal);
        Assert.Contains("\"authorizationStateVersion\":\"7\"", probeBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidCredentialReturns401WithoutIssuingOrReturningJwt()
    {
        FixtureOperatorStore store = new();
        store.Add(ActiveUserName, ActivePassword, OperatorState.Active, 1, ActiveOperatorId);
        CountingTokenIssuer issuer = new();

        using AuthnWebApplicationFactory factory = new(store, issuer);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await LoginAsync(client, ActiveUserName, "wrong-password");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertErrorEnvelope(body);
        Assert.Equal(0, issuer.IssueCount);
        Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TestJwtConfiguration.SigningKey, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisabledCredentialReturns401WithoutIssuingOrReturningJwt()
    {
        FixtureOperatorStore store = new();
        store.Add(ActiveUserName, ActivePassword, OperatorState.Disabled, 3, ActiveOperatorId);
        CountingTokenIssuer issuer = new();

        using AuthnWebApplicationFactory factory = new(store, issuer);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await LoginAsync(client, ActiveUserName, ActivePassword);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertErrorEnvelope(body);
        Assert.Equal(0, issuer.IssueCount);
        Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TestJwtConfiguration.SigningKey, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuccessRehashNeededAuthenticatesWithoutWritingThePasswordHash()
    {
        const string password = "rehash-password";
        FixtureOperatorStore store = new();
        string legacyHash = CreateLegacyIdentityV2Hash(password);
        store.AddHash(
            "rehash.operator",
            legacyHash,
            OperatorState.Active,
            authorizationStateVersion: 4,
            ActiveOperatorId);

        using AuthnWebApplicationFactory factory = new(store);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await LoginAsync(client, "rehash.operator", password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(legacyHash, store.GetHash("rehash.operator"));
        Assert.NotEmpty(await ReadAccessTokenAsync(response));
    }

    [Fact]
    public async Task InvalidSignatureIsRejectedBeforeTheProbeHandler()
    {
        using AuthnWebApplicationFactory factory = new(new FixtureOperatorStore());
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(signingKey: CreateOtherSigningKey());

        using HttpResponseMessage response = await SendProbeAsync(client, token);

        await AssertRejectedBeforeProbeHandlerAsync(response, "AUTHN-JWT-01");
    }

    [Fact]
    public async Task WrongIssuerIsRejectedBeforeTheProbeHandler()
    {
        using AuthnWebApplicationFactory factory = new(new FixtureOperatorStore());
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(issuer: "wrong-issuer");

        using HttpResponseMessage response = await SendProbeAsync(client, token);

        await AssertRejectedBeforeProbeHandlerAsync(response, "AUTHN-JWT-02");
    }

    [Fact]
    public async Task WrongAudienceIsRejectedBeforeTheProbeHandler()
    {
        using AuthnWebApplicationFactory factory = new(new FixtureOperatorStore());
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(audience: "wrong-audience");

        using HttpResponseMessage response = await SendProbeAsync(client, token);

        await AssertRejectedBeforeProbeHandlerAsync(response, "AUDIENCE");
    }

    [Fact]
    public async Task ExpiredTokenIsRejectedBeforeTheProbeHandler()
    {
        using AuthnWebApplicationFactory factory = new(new FixtureOperatorStore());
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(expires: DateTime.UtcNow.AddMinutes(-1));

        using HttpResponseMessage response = await SendProbeAsync(client, token);

        await AssertRejectedBeforeProbeHandlerAsync(response, "AUTHN-JWT-03");
    }

    [Fact]
    public async Task DisallowedSigningAlgorithmIsRejectedBeforeTheProbeHandler()
    {
        using AuthnWebApplicationFactory factory = new(new FixtureOperatorStore());
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(signingAlgorithm: SecurityAlgorithms.HmacSha384);

        using HttpResponseMessage response = await SendProbeAsync(client, token);

        await AssertRejectedBeforeProbeHandlerAsync(response, "ALGORITHM");
    }

    [Fact]
    public async Task JwtAndSigningKeyAreAbsentFromErrorUnrelatedResponseAndLogs()
    {
        FixtureOperatorStore store = new();
        store.Add(ActiveUserName, ActivePassword, OperatorState.Active, 1, ActiveOperatorId);
        using ConsoleCapture capture = new();

        using AuthnWebApplicationFactory factory = new(store);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage login = await LoginAsync(client, ActiveUserName, ActivePassword);
        string token = await ReadAccessTokenAsync(login);
        using HttpResponseMessage invalidTokenResponse = await SendProbeAsync(client, token[..^1] + (token[^1] == 'a' ? 'b' : 'a'));
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

        Assert.DoesNotContain(token, invalidBody, StringComparison.Ordinal);
        Assert.DoesNotContain(token, unrelatedBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticationProbeDoesNotResolveCurrentOperatorStateOrRole()
    {
        FixtureOperatorStore store = new();
        store.Add(ActiveUserName, ActivePassword, OperatorState.Active, 9, ActiveOperatorId);

        using AuthnWebApplicationFactory factory = new(store);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage login = await LoginAsync(client, ActiveUserName, ActivePassword);
        string token = await ReadAccessTokenAsync(login);

        store.Clear();
        using HttpResponseMessage probe = await SendProbeAsync(client, token);

        Assert.Equal(HttpStatusCode.OK, probe.StatusCode);
        Assert.Contains("authenticationHandlerReached", await probe.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(1, store.LookupCount);
    }

    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string? userName,
        string? password) =>
        await client.PostAsJsonAsync("/auth/login", new { userName, password });

    private static async Task<string> ReadAccessTokenAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string? token = document.RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    private static async Task<HttpResponseMessage> SendProbeAsync(HttpClient client, string token)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/__authn/probe");
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
        AssertErrorEnvelope(body);
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
                new Claim(JwtRegisteredClaimNames.Sub, ActiveOperatorId.ToString("D")),
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

    private static string CreateLegacyIdentityV2Hash(string password)
    {
        Operator probe = OperatorFactory.Create(
            "rehash.probe",
            password,
            OperatorRole.Viewer,
            DateTimeOffset.UnixEpoch,
            "rehash-probe-stamp");
        PasswordHasher<Operator> legacyHasher = new(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2,
        }));
        return legacyHasher.HashPassword(probe, password);
    }

    private static void AssertErrorEnvelope(string body)
    {
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal("authentication_required", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("Authentication is required.", document.RootElement.GetProperty("message").GetString());
    }

    private sealed class FixtureOperatorStore : IAuthnOperatorStore
    {
        private readonly Dictionary<string, AuthnOperatorCredential> credentials = new(StringComparer.Ordinal);

        public int LookupCount { get; private set; }

        public void Add(
            string userName,
            string password,
            OperatorState state,
            int authorizationStateVersion,
            Guid id) =>
            AddHash(
                userName,
                OperatorFactory.Create(
                    userName,
                    password,
                    OperatorRole.Viewer,
                    DateTimeOffset.UnixEpoch,
                    "fixture-security-stamp").PasswordHash,
                state,
                authorizationStateVersion,
                id);

        public void AddHash(
            string userName,
            string passwordHash,
            OperatorState state,
            int authorizationStateVersion,
            Guid id)
        {
            credentials[userName.Trim().ToUpperInvariant()] = new AuthnOperatorCredential(
                id,
                passwordHash,
                state,
                authorizationStateVersion);
        }

        public string GetHash(string userName) => credentials[userName.Trim().ToUpperInvariant()].PersistedPasswordHash;

        public void Clear() => credentials.Clear();

        public Task<AuthnOperatorCredential?> FindByNormalizedUserNameAsync(
            string normalizedUserName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LookupCount++;
            credentials.TryGetValue(normalizedUserName, out AuthnOperatorCredential? credential);
            return Task.FromResult(credential);
        }
    }

    private sealed class CountingTokenIssuer : IJwtAccessTokenIssuer
    {
        public int IssueCount { get; private set; }

        public string Issue(AuthnLoginResult login)
        {
            _ = login;
            IssueCount++;
            return "test-issued-token";
        }
    }
}

internal sealed class AuthnWebApplicationFactory(
    IAuthnOperatorStore store,
    IJwtAccessTokenIssuer? tokenIssuer = null,
    TimeProvider? timeProvider = null) : WebApplicationFactory<api::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(JwtAuthnOptions.SigningKeyConfigurationKey, TestJwtConfiguration.SigningKey);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthnOperatorStore>();
            services.AddSingleton(store);

            if (tokenIssuer is not null)
            {
                services.RemoveAll<IJwtAccessTokenIssuer>();
                services.AddSingleton(tokenIssuer);
            }

            if (timeProvider is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(timeProvider);
            }
        });
    }
}
