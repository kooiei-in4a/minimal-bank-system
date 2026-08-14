using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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
    private const string SigningKey = AuthenticationProbeTests.TestSigningKey;
    private const string SeedPlaintextPassword = "authn01-integration-seed-password-not-for-production";

    private static readonly DateTimeOffset FrozenUtc = new(2032, 5, 6, 7, 8, 9, TimeSpan.Zero);

    [Fact]
    public async Task ValidCredentialForActiveOperatorIssuesAJwtWithSubjectAndAuthorizationStateVersion()
    {
        await MigrateAsync();
        Operator seeded = await SeedOperatorAsync("authn01.active.viewer", OperatorRole.Viewer);

        await using AuthenticationProbeApiFactory factory = new(SigningKey, Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/login",
            new { userName = seeded.UserName, password = SeedPlaintextPassword });
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        string accessToken = document.RootElement.GetProperty("accessToken").GetString()!;
        Assert.Equal("Bearer", document.RootElement.GetProperty("tokenType").GetString());
        Assert.True(document.RootElement.GetProperty("expiresInSeconds").GetInt32() > 0);

        JwtSecurityToken parsed = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        Assert.Equal(seeded.Id.ToString(), parsed.Subject);
        Assert.Equal(
            Operator.InitialAuthorizationStateVersion.ToString(CultureInfo.InvariantCulture),
            parsed.Claims.Single(claim => claim.Type == JwtTokenSettings.AuthorizationStateVersionClaimType).Value);
        Assert.Equal(JwtTokenSettings.Issuer, parsed.Issuer);
        Assert.Contains(JwtTokenSettings.Audience, parsed.Audiences);

        // Requirement 5/14: the token issued here validates against the same centralized
        // parameters the AUTHN probe validator uses.
        using HttpRequestMessage probeRequest = new(HttpMethod.Get, AuthenticationProbeController.ProbePath);
        probeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage probeResponse = await client.SendAsync(probeRequest);
        string probeBody = await probeResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, probeResponse.StatusCode);
        using JsonDocument probeDocument = JsonDocument.Parse(probeBody);
        Assert.True(probeDocument.RootElement.GetProperty("handlerReached").GetBoolean());
        Assert.Equal(seeded.Id.ToString(), probeDocument.RootElement.GetProperty("subject").GetString());

        Assert.DoesNotContain(SigningKey, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidPasswordReturns401AuthenticationRequiredAndIssuesNoJwt()
    {
        await MigrateAsync();
        Operator seeded = await SeedOperatorAsync("authn01.invalid.viewer", OperatorRole.Viewer);

        await using AuthenticationProbeApiFactory factory = new(SigningKey, Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/login",
            new { userName = seeded.UserName, password = "definitely-the-wrong-password" });

        await AssertAuthenticationRequiredAsync(response);
    }

    [Fact]
    public async Task UnknownUserNameReturns401AuthenticationRequiredAndIssuesNoJwt()
    {
        await MigrateAsync();

        await using AuthenticationProbeApiFactory factory = new(SigningKey, Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/login",
            new { userName = "no-such-operator", password = SeedPlaintextPassword });

        await AssertAuthenticationRequiredAsync(response);
    }

    [Fact]
    public async Task DisabledOperatorLoginIsRejectedEvenWithACorrectCredentialAndIssuesNoJwt()
    {
        await MigrateAsync();
        Operator seeded = await SeedOperatorAsync("authn01.disabled.teller", OperatorRole.Teller);

        // Test-only fixture: a direct raw-SQL state flip. AUTHN does not own or introduce a
        // production Operator enable/disable mutation path for this Issue.
        await ExecuteNonQueryAsync(
            $"""
             UPDATE {OperatorPersistence.TableName}
             SET {OperatorPersistence.StateColumn} = '{OperatorPersistence.DisabledStateToken}'
             WHERE {OperatorPersistence.IdColumn} = '{seeded.Id}';
             """);

        await using AuthenticationProbeApiFactory factory = new(SigningKey, Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/login",
            new { userName = seeded.UserName, password = SeedPlaintextPassword });

        await AssertAuthenticationRequiredAsync(response);
    }

    [Fact]
    public async Task SuccessRehashNeededIsAcceptedAsSuccessfulLoginWithoutAuthnRewritingTheStoredHash()
    {
        await MigrateAsync();
        Operator seeded = await SeedOperatorAsync("authn01.rehash.administrator", OperatorRole.Administrator);
        string originalHash = await ReadStoredPasswordHashAsync(seeded.Id);

        // Force PasswordHasher into its legacy IdentityV2 format, which reports
        // SuccessRehashNeeded on a correct verification under the default IdentityV3 compatibility
        // mode. This is test-only seeding: AUTHN never writes this format itself and must not
        // rewrite the hash it reads.
        string legacyHash = LegacyFormatHashV2(SeedPlaintextPassword);
        await ExecuteNonQueryAsync(
            $"""
             UPDATE {OperatorPersistence.TableName}
             SET {OperatorPersistence.PasswordHashColumn} = '{legacyHash}'
             WHERE {OperatorPersistence.IdColumn} = '{seeded.Id}';
             """);
        Assert.NotEqual(originalHash, legacyHash);

        await using AuthenticationProbeApiFactory factory = new(SigningKey, Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/login",
            new { userName = seeded.UserName, password = SeedPlaintextPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string hashAfterLogin = await ReadStoredPasswordHashAsync(seeded.Id);
        Assert.Equal(legacyHash, hashAfterLogin);
    }

    [Fact]
    public async Task MissingCredentialFieldsReturn401AuthenticationRequiredAndIssueNoJwt()
    {
        await MigrateAsync();

        await using AuthenticationProbeApiFactory factory = new(SigningKey, Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync("/login", new { });

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
    /// ASP.NET Core Identity's legacy IdentityV2 hash format: 1-byte format marker (0x00) +
    /// 16-byte salt + 32-byte PBKDF2-HMACSHA1 subkey (1000 iterations). <c>PasswordHasher&lt;T&gt;</c>
    /// recognizes this format and reports <see cref="Microsoft.AspNetCore.Identity.PasswordVerificationResult.SuccessRehashNeeded"/>
    /// for it under the default IdentityV3 compatibility mode.
    /// </summary>
    private static string LegacyFormatHashV2(string password)
    {
        const int saltSize = 16;
        const int subkeySize = 32;
        const int iterations = 1000;

        byte[] salt = RandomNumberGenerator.GetBytes(saltSize);
        byte[] subkey = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.Unicode.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA1,
            subkeySize);

        byte[] outputBytes = new byte[1 + saltSize + subkeySize];
        outputBytes[0] = 0x00;
        Buffer.BlockCopy(salt, 0, outputBytes, 1, saltSize);
        Buffer.BlockCopy(subkey, 0, outputBytes, 1 + saltSize, subkeySize);
        return Convert.ToBase64String(outputBytes);
    }

    private static async Task AssertAuthenticationRequiredAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal("authentication_required", document.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
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

    private async Task ExecuteNonQueryAsync(string commandText)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        await command.ExecuteNonQueryAsync();
    }
}
