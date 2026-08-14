using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Api.Authentication;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection(TestExecutionCollections.ConsoleSensitive)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class AuthenticationTests(
    PostgreSqlContainerFixture fixture) : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string SigningKey =
        "AUTHN_TEST_SIGNING_KEY_0123456789abcdef0123456789abcdef";
    private const string Password = "authn-test-password-not-for-production";

    [Fact]
    public async Task ActiveLoginIssuesShortLivedJwtWithSubjectAndAuthorizationStateVersion()
    {
        Operator operatorEntity = await SeedOperatorAsync();

        await using AuthenticationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await LoginAsync(client, operatorEntity.UserName, Password);

        LoginResponse login = await ReadSuccessAsync(response);
        Assert.Equal("Bearer", login.TokenType);
        Assert.Equal((long)JwtTokenParameters.AccessTokenLifetime.TotalSeconds, login.ExpiresIn);
        Assert.DoesNotContain("refresh", login.AccessToken, StringComparison.OrdinalIgnoreCase);

        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken);
        Assert.Equal(operatorEntity.Id.ToString("D"), token.Subject);
        Assert.Equal(
            operatorEntity.AuthorizationStateVersion.ToString(CultureInfo.InvariantCulture),
            token.Claims.Single(claim => claim.Type == JwtTokenParameters.AuthorizationStateVersionClaim).Value);
        Assert.Equal(JwtTokenParameters.Issuer, token.Issuer);
        Assert.Contains(JwtTokenParameters.Audience, token.Audiences);
        Assert.Equal(
            JwtTokenParameters.AccessTokenLifetime,
            token.ValidTo.ToUniversalTime() - token.ValidFrom.ToUniversalTime());
        Assert.Equal(
            "viewer",
            token.Claims.Single(claim => claim.Type == JwtTokenParameters.RoleClaim).Value);
    }

    [Fact]
    public async Task InvalidCredentialReturnsAuthenticationRequiredWithoutToken()
    {
        Operator operatorEntity = await SeedOperatorAsync();

        await using AuthenticationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await LoginAsync(
            client,
            operatorEntity.UserName,
            "wrong-authn-password");

        await AssertAuthenticationRequiredAsync(response);
    }

    [Fact]
    public async Task DisabledOperatorCannotLoginAndHasNoProductionMutationPath()
    {
        Operator operatorEntity = await SeedOperatorAsync();
        await SetStateAsync(operatorEntity.Id, OperatorState.Disabled, authorizationStateVersion: 2);

        await using AuthenticationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await LoginAsync(client, operatorEntity.UserName, Password);

        await AssertAuthenticationRequiredAsync(response);
    }

    [Fact]
    public async Task SuccessRehashNeededLogsInWithoutRewritingThePasswordHash()
    {
        Operator operatorEntity = await SeedOperatorAsync();
        PasswordHasher<Operator> oldHasher = new(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2,
        }));
        string oldHash = oldHasher.HashPassword(operatorEntity, Password);
        await ReplacePasswordHashAsync(operatorEntity.Id, oldHash);

        await using AuthenticationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await LoginAsync(client, operatorEntity.UserName, Password);

        LoginResponse login = await ReadSuccessAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.Equal(oldHash, await ReadPasswordHashAsync(operatorEntity.Id));
    }

    [Fact]
    public async Task ValidJwtReachesTheAuthenticationOnlyTestHostHandler()
    {
        Operator operatorEntity = await SeedOperatorAsync();
        await using AuthenticationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        LoginResponse login = await ReadSuccessAsync(await LoginAsync(client, operatorEntity.UserName, Password));

        using HttpResponseMessage response = await ProbeAsync(client, login.AccessToken);

        await AssertProbeReachedAsync(response);
    }

    [Fact]
    public async Task InvalidSignatureIsRejectedBeforeTheAuthenticationOnlyHandler()
    {
        await AssertProbeRejectedAsync(CreateToken(SigningKey + "wrong"));
    }

    [Fact]
    public async Task WrongIssuerIsRejectedBeforeTheAuthenticationOnlyHandler()
    {
        await AssertProbeRejectedAsync(CreateToken(SigningKey, issuer: "wrong-issuer"));
    }

    [Fact]
    public async Task WrongAudienceIsRejectedBeforeTheAuthenticationOnlyHandler()
    {
        await AssertProbeRejectedAsync(CreateToken(SigningKey, audience: "wrong-audience"));
    }

    [Fact]
    public async Task ExpiredTokenIsRejectedBeforeTheAuthenticationOnlyHandler()
    {
        await AssertProbeRejectedAsync(
            CreateToken(SigningKey, expiresAt: DateTime.UtcNow.AddMinutes(-1)));
    }

    [Fact]
    public async Task DisallowedSigningAlgorithmIsRejectedBeforeTheAuthenticationOnlyHandler()
    {
        await AssertProbeRejectedAsync(
            CreateToken(SigningKey, algorithm: SecurityAlgorithms.HmacSha384));
    }

    [Fact]
    public async Task SigningKeyIsExternallyConfiguredAndNeverReturnedByTheLoginContract()
    {
        Operator operatorEntity = await SeedOperatorAsync();

        await using AuthenticationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await LoginAsync(client, operatorEntity.UserName, Password);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(SigningKey, body, StringComparison.Ordinal);
        Assert.DoesNotContain(SigningKey, factory.Services.GetRequiredService<JwtTokenParameters>().ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticationOnlyProbeDoesNotResolveCurrentOperatorAuthorizationState()
    {
        Operator operatorEntity = await SeedOperatorAsync();
        await using AuthenticationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        LoginResponse login = await ReadSuccessAsync(await LoginAsync(client, operatorEntity.UserName, Password));

        // This fixture mutation is test-only SQL. AUTHN must not add a production state-mutation
        // endpoint, and the authentication-only probe must not implement AUTHZ's request-time
        // active/version/role checks.
        await SetStateAsync(operatorEntity.Id, OperatorState.Disabled, authorizationStateVersion: 2);

        using HttpResponseMessage response = await ProbeAsync(client, login.AccessToken);

        await AssertProbeReachedAsync(response);
    }

    [Fact]
    public async Task IssuanceAndValidationUseTheSameCentralizedTokenParameters()
    {
        await using AuthenticationWebApplicationFactory factory = CreateFactory();
        TokenValidationParameters validation = factory.Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme)
            .TokenValidationParameters;

        Assert.True(validation.ValidateIssuerSigningKey);
        Assert.True(validation.ValidateIssuer);
        Assert.True(validation.ValidateAudience);
        Assert.True(validation.ValidateLifetime);
        Assert.Equal(JwtTokenParameters.Issuer, validation.ValidIssuer);
        Assert.Equal(JwtTokenParameters.Audience, validation.ValidAudience);
        Assert.Equal([JwtTokenParameters.AllowedAlgorithm], validation.ValidAlgorithms);
        Assert.Equal(TimeSpan.Zero, validation.ClockSkew);
        Assert.Equal(15, JwtTokenParameters.AccessTokenLifetimeMinutes);
    }

    private AuthenticationWebApplicationFactory CreateFactory() =>
        new(Database.ConnectionString, SigningKey);

    private async Task<Operator> SeedOperatorAsync()
    {
        await MigrateAsync();

        Operator operatorEntity = OperatorFactory.Create(
            $"authn.{Guid.NewGuid():N}",
            Password,
            OperatorRole.Viewer,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));

        await using BankDbContext context = CreateContext();
        context.Operators.Add(operatorEntity);
        await context.SaveChangesAsync();
        return operatorEntity;
    }

    private async Task MigrateAsync()
    {
        MigratorRun run = await MigratorProcess.RunAsync(
            Database.ConnectionString,
            TimeSpan.FromSeconds(120));
        Assert.Equal(MigratorExitCode.Success, run.ExitCode);
    }

    private BankDbContext CreateContext()
    {
        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        return new BankDbContext(options.Options);
    }

    private async Task SetStateAsync(
        Guid operatorId,
        OperatorState state,
        int authorizationStateVersion)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"UPDATE {OperatorPersistence.TableName} SET {OperatorPersistence.StateColumn} = $1, " +
            $"{OperatorPersistence.AuthorizationStateVersionColumn} = $2 WHERE {OperatorPersistence.IdColumn} = $3",
            connection);
        command.Parameters.AddWithValue(
            state == OperatorState.Active
                ? OperatorPersistence.ActiveStateToken
                : OperatorPersistence.DisabledStateToken);
        command.Parameters.AddWithValue(authorizationStateVersion);
        command.Parameters.AddWithValue(operatorId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private async Task ReplacePasswordHashAsync(Guid operatorId, string passwordHash)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"UPDATE {OperatorPersistence.TableName} SET {OperatorPersistence.PasswordHashColumn} = $1 " +
            $"WHERE {OperatorPersistence.IdColumn} = $2",
            connection);
        command.Parameters.AddWithValue(passwordHash);
        command.Parameters.AddWithValue(operatorId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private async Task<string> ReadPasswordHashAsync(Guid operatorId)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"SELECT {OperatorPersistence.PasswordHashColumn} FROM {OperatorPersistence.TableName} " +
            $"WHERE {OperatorPersistence.IdColumn} = $1",
            connection);
        command.Parameters.AddWithValue(operatorId);
        return Assert.IsType<string>(await command.ExecuteScalarAsync());
    }

    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string userName,
        string password) =>
        await client.PostAsJsonAsync(
            "/api/auth/login",
            new { userName, password });

    private static async Task<LoginResponse> ReadSuccessAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        LoginResponse? login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        return login!;
    }

    private static async Task AssertAuthenticationRequiredAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        ApiErrorEnvelope? error = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        Assert.NotNull(error);
        Assert.Equal("authentication_required", error!.Code);
        Assert.DoesNotContain("accessToken", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpResponseMessage> ProbeAsync(HttpClient client, string token)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/__authn/probe");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static async Task AssertProbeReachedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ProbeResponse? probe = await response.Content.ReadFromJsonAsync<ProbeResponse>();
        Assert.NotNull(probe);
        Assert.True(probe!.Reached);
    }

    private static async Task AssertProbeRejectedAsync(string token)
    {
        await using AuthenticationWebApplicationFactory factory = new(
            connectionString: null,
            signingKey: SigningKey);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await ProbeAsync(client, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("reached", body, StringComparison.OrdinalIgnoreCase);
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

[ApiController]
public sealed class AuthenticationProbeController : ControllerBase
{
    [Authorize]
    [HttpGet("/__authn/probe")]
    public ActionResult<ProbeResponse> Probe() =>
        Ok(new ProbeResponse(true));
}

public sealed record ProbeResponse(bool Reached);

internal sealed class AuthenticationWebApplicationFactory(
    string? connectionString,
    string signingKey) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(
            $"ConnectionStrings:{BankPersistence.ConnectionStringName}",
            connectionString ?? string.Empty);
        builder.UseSetting(JwtTokenParameters.SigningKeyConfigurationKey, signingKey);
        builder.ConfigureServices(services =>
        {
            services
                .AddControllers()
                .AddApplicationPart(typeof(AuthenticationProbeController).Assembly);
        });
    }
}
