using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using MinimalBankSystem.IntegrationTests.Authentication;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Verification Requirements 1-4 and the end-to-end tie between login-issued JWTs and the AUTHN
/// probe (Requirements 1, 5, 11, 14): real credential verification against a real Operator row,
/// login-time disabled-Operator rejection, SuccessRehashNeeded handling, and non-disclosure of the
/// signing key from the login response.
/// </summary>
[Trait("Category", "PostgreSqlIntegration")]
public sealed class LoginTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string SeedPlaintextPassword = "authn01-integration-seed-password-not-for-production";

    private static readonly DateTimeOffset FrozenUtc = new(2032, 5, 6, 7, 8, 9, TimeSpan.Zero);

    [Fact]
    public async Task ValidCredentialForActiveOperatorIssuesAJwtWithSubjectAndAuthorizationStateVersion()
    {
        await MigrateAsync();
        Operator seeded = await SeedOperatorAsync("authn01.active.viewer", OperatorRole.Viewer);

        await using AuthenticationProbeApiFactory factory = new(
            TestJwtConfiguration.SigningKey,
            Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/login",
            new { userName = seeded.UserName, password = SeedPlaintextPassword });
        string body = await response.Content.ReadAsStringAsync();
        AssertNotInternalError(response, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        string accessToken = document.RootElement.GetProperty("accessToken").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        JwtSecurityToken parsed = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        Assert.Equal(seeded.Id.ToString("D"), parsed.Subject);
        Assert.Equal(
            Operator.InitialAuthorizationStateVersion.ToString(CultureInfo.InvariantCulture),
            parsed.Claims.Single(claim => claim.Type == AuthnClaimTypes.AuthorizationStateVersion).Value);
        Assert.Equal("minimal-bank-system", parsed.Issuer);
        Assert.Contains("minimal-bank-system-api", parsed.Audiences);

        using HttpRequestMessage probeRequest = new(HttpMethod.Get, AuthenticationProbeController.ProbePath);
        probeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage probeResponse = await client.SendAsync(probeRequest);
        string probeBody = await probeResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, probeResponse.StatusCode);
        using JsonDocument probeDocument = JsonDocument.Parse(probeBody);
        Assert.True(probeDocument.RootElement.GetProperty("handlerReached").GetBoolean());
        Assert.Equal(seeded.Id.ToString("D"), probeDocument.RootElement.GetProperty("subject").GetString());

        string rawSigningKey = Encoding.UTF8.GetString(Convert.FromBase64String(TestJwtConfiguration.SigningKey));
        Assert.DoesNotContain(TestJwtConfiguration.SigningKey, body, StringComparison.Ordinal);
        Assert.DoesNotContain(rawSigningKey, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidPasswordReturns401AuthenticationRequiredAndIssuesNoJwt()
    {
        await MigrateAsync();
        Operator seeded = await SeedOperatorAsync("authn01.invalid.viewer", OperatorRole.Viewer);

        await using AuthenticationProbeApiFactory factory = new(
            TestJwtConfiguration.SigningKey,
            Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/login",
            new { userName = seeded.UserName, password = "definitely-the-wrong-password" });

        await AssertAuthenticationRequiredAsync(response);
    }

    [Fact]
    public async Task UnknownUserNameReturns401AuthenticationRequiredAndIssuesNoJwt()
    {
        await MigrateAsync();

        await using AuthenticationProbeApiFactory factory = new(
            TestJwtConfiguration.SigningKey,
            Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/login",
            new { userName = "no-such-operator", password = SeedPlaintextPassword });

        await AssertAuthenticationRequiredAsync(response);
    }

    [Fact]
    public async Task DisabledOperatorLoginIsRejectedEvenWithACorrectCredentialAndIssuesNoJwt()
    {
        await MigrateAsync();
        Operator seeded = await SeedOperatorAsync("authn01.disabled.teller", OperatorRole.Teller);

        await ExecuteNonQueryAsync(
            $"""
             UPDATE {OperatorPersistence.TableName}
             SET {OperatorPersistence.StateColumn} = @state
             WHERE {OperatorPersistence.IdColumn} = @id;
             """,
            ("state", OperatorPersistence.DisabledStateToken),
            ("id", seeded.Id));

        await using AuthenticationProbeApiFactory factory = new(
            TestJwtConfiguration.SigningKey,
            Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/login",
            new { userName = seeded.UserName, password = SeedPlaintextPassword });

        await AssertAuthenticationRequiredAsync(response);
    }

    [Fact]
    public async Task SuccessRehashNeededIsAcceptedAsSuccessfulLoginWithoutAuthnRewritingTheStoredHash()
    {
        await MigrateAsync();
        Operator seeded = await SeedOperatorAsync("authn01.rehash.administrator", OperatorRole.Administrator);
        string originalHash = await ReadStoredPasswordHashAsync(seeded.Id);

        string legacyHash = LegacyFormatHashV2(SeedPlaintextPassword);
        await ExecuteNonQueryAsync(
            $"""
             UPDATE {OperatorPersistence.TableName}
             SET {OperatorPersistence.PasswordHashColumn} = @hash
             WHERE {OperatorPersistence.IdColumn} = @id;
             """,
            ("hash", legacyHash),
            ("id", seeded.Id));
        Assert.NotEqual(originalHash, legacyHash);

        await using AuthenticationProbeApiFactory factory = new(
            TestJwtConfiguration.SigningKey,
            Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/login",
            new { userName = seeded.UserName, password = SeedPlaintextPassword });
        string body = await response.Content.ReadAsStringAsync();
        AssertNotInternalError(response, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("accessToken").GetString()));

        string hashAfterLogin = await ReadStoredPasswordHashAsync(seeded.Id);
        Assert.Equal(legacyHash, hashAfterLogin);
    }

    [Fact]
    public async Task MissingCredentialFieldsReturn401AuthenticationRequiredAndIssueNoJwt()
    {
        await MigrateAsync();

        await using AuthenticationProbeApiFactory factory = new(
            TestJwtConfiguration.SigningKey,
            Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync("/auth/login", new { });

        await AssertAuthenticationRequiredAsync(response);
    }

    private async Task MigrateAsync()
    {
        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, TimeSpan.FromSeconds(120));
        Assert.Equal(MigratorExitCode.Success, run.ExitCode);
    }

    private async Task<Operator> SeedOperatorAsync(string userName, OperatorRole role)
    {
        Operator created = OperatorFactory.Create(
            userName,
            SeedPlaintextPassword,
            role,
            FrozenUtc,
            Guid.NewGuid().ToString());

        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        await using BankDbContext context = new(options.Options);
        context.Operators.Add(created);
        await context.SaveChangesAsync();

        return created;
    }

    /// <summary>
    /// ASP.NET Core Identity's IdentityV2 hash, produced by the real <see cref="PasswordHasher{TUser}"/>.
    /// Under the default IdentityV3 compatibility mode this verifies as SuccessRehashNeeded.
    /// </summary>
    private static string LegacyFormatHashV2(string password)
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

    private static async Task AssertAuthenticationRequiredAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        AssertNotInternalError(response, body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal("authentication_required", document.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
        string rawSigningKey = Encoding.UTF8.GetString(Convert.FromBase64String(TestJwtConfiguration.SigningKey));
        Assert.DoesNotContain(TestJwtConfiguration.SigningKey, body, StringComparison.Ordinal);
        Assert.DoesNotContain(rawSigningKey, body, StringComparison.Ordinal);
    }

    private static void AssertNotInternalError(HttpResponseMessage response, string body)
    {
        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            throw new Xunit.Sdk.XunitException(
                $"PostgreSQL AUTHN login returned HTTP 500; production error contract was not weakened. Body: {body}");
        }
    }

    private async Task<string> ReadStoredPasswordHashAsync(Guid operatorId)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"SELECT {OperatorPersistence.PasswordHashColumn} FROM {OperatorPersistence.TableName} " +
            $"WHERE {OperatorPersistence.IdColumn} = @id;",
            connection);
        command.Parameters.AddWithValue("id", operatorId);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private async Task ExecuteNonQueryAsync(string commandText, params (string Name, object Value)[] parameters)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }
}
