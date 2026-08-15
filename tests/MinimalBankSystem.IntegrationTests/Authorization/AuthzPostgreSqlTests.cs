using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Auditing;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using MinimalBankSystem.IntegrationTests.Authentication;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.IntegrationTests.PostgreSql;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.Authorization;

/// <summary>
/// Verification items 1, 2, 4-8 and 11 against a real PostgreSQL runtime: current-Operator
/// resolution gates every fallback-protected request, stale authentication state is answered with
/// HTTP 401, current-role policy rejections write exactly one failure Audit record before HTTP
/// 403 / operation_not_permitted, the JWT role claim is never authoritative, and required Audit
/// persistence failures fail closed with HTTP 500.
/// </summary>
[Trait("Category", "PostgreSqlIntegration")]
[Collection(TestExecutionCollections.ConsoleSensitive)]
public sealed class AuthzPostgreSqlTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string SeedPlaintextPassword = "authz01-integration-seed-password-not-for-production";

    private static readonly DateTimeOffset FrozenUtc = new(2032, 6, 7, 8, 9, 10, TimeSpan.Zero);

    [Fact]
    public async Task ActiveAdministratorReachesTheAdministratorOnlyFeature()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("authz01.active.administrator", OperatorRole.Administrator);

        await using AuthorizationProbeApiFactory factory = new(
            TestJwtConfiguration.SigningKey,
            Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(administrator.Id, 1);

        using HttpResponseMessage response = await SendAsync(
            client,
            HttpMethod.Get,
            AuthzFeatureController.AdministratorOnlyPath,
            token);

        await AssertHandlerReachedAsync(response);
    }

    [Fact]
    public async Task ActiveTellerReachesTheTellerOrAdministratorFeature()
    {
        await MigrateAsync();
        Operator teller = await SeedOperatorAsync("authz01.active.teller", OperatorRole.Teller);

        await using AuthorizationProbeApiFactory factory = new(
            TestJwtConfiguration.SigningKey,
            Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(teller.Id, 1);

        using HttpResponseMessage response = await SendAsync(
            client,
            HttpMethod.Get,
            AuthzFeatureController.TellerOrAdministratorPath,
            token);

        await AssertHandlerReachedAsync(response);
    }

    [Fact]
    public async Task ViewerIsRejectedWith403OperationNotPermittedOnTellerOrAdministrator()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("authz01.viewer.rejected", OperatorRole.Viewer);

        await using AuthorizationProbeApiFactory factory = new(
            TestJwtConfiguration.SigningKey,
            Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(viewer.Id, 1);

        using HttpResponseMessage response = await SendAsync(
            client,
            HttpMethod.Get,
            AuthzFeatureController.TellerOrAdministratorPath,
            token);

        await AssertRejectedAsync(response, HttpStatusCode.Forbidden, "viewer-on-teller-or-administrator");
    }

    [Fact]
    public async Task ViewerIsRejectedWith403AndWritesExactlyOneFailureAuditRecord()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("authz01.viewer.audit", OperatorRole.Viewer);

        await using AuthorizationProbeApiFactory factory = new(
            TestJwtConfiguration.SigningKey,
            Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(viewer.Id, 1);
        const string correlationId = "authz-403-viewer-audit";

        using HttpRequestMessage request = new(HttpMethod.Get, AuthzFeatureController.AdministratorOnlyPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        using HttpResponseMessage response = await client.SendAsync(request);

        await AssertRejectedAsync(response, HttpStatusCode.Forbidden, "viewer-on-administrator");

        AuditRow row = await ReadSingleAuditRowAsync(correlationId);
        Assert.Equal(viewer.Id, row.ActorIdentifier);
        Assert.Equal(AuditPersistence.ViewerRoleToken, row.ActorRole);
        Assert.Equal(AuthzFeatureController.VerificationOperationIdentifier, row.OperationIdentifier);
        Assert.Equal(AuthzFeatureController.VerificationTargetIdentifier, row.TargetIdentifier);
        Assert.Equal(AuditPersistence.FailureResultToken, row.Result);
        Assert.Equal(ApiErrorEnvelope.OperationNotPermitted.Code, row.FailureBusinessErrorCode);
        Assert.Equal(correlationId, row.CorrelationId);
    }

    [Fact]
    public async Task UnknownOperatorIsRejectedWith401AndWritesNoAudit()
    {
        await MigrateAsync();

        await using AuthorizationProbeApiFactory factory = new(
            TestJwtConfiguration.SigningKey,
            Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(Guid.NewGuid(), 1);
        const string correlationId = "authz-401-unknown-operator";

        using HttpRequestMessage request = new(HttpMethod.Get, AuthzFeatureController.AdministratorOnlyPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        using HttpResponseMessage response = await client.SendAsync(request);

        await AssertRejectedAsync(response, HttpStatusCode.Unauthorized, "unknown-operator");
        Assert.Equal(0L, await CountAuditAsync(correlationId));
    }

    [Fact]
    public async Task DisabledOperatorIsRejectedWith401WithoutAudit()
    {
        await MigrateAsync();
        Operator teller = await SeedOperatorAsync("authz01.disabled.teller", OperatorRole.Teller);
        await ExecuteNonQueryAsync(
            $"""
             UPDATE {OperatorPersistence.TableName}
             SET {OperatorPersistence.StateColumn} = @state
             WHERE {OperatorPersistence.IdColumn} = @id;
             """,
            ("state", OperatorPersistence.DisabledStateToken),
            ("id", teller.Id));

        await using AuthorizationProbeApiFactory factory = new(
            TestJwtConfiguration.SigningKey,
            Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(teller.Id, 1);
        const string correlationId = "authz-401-disabled-operator";

        using HttpRequestMessage request = new(HttpMethod.Get, AuthzFeatureController.AdministratorOnlyPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        using HttpResponseMessage response = await client.SendAsync(request);

        await AssertRejectedWithMarkerAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHZ-STATE-01",
            "disabled operator must be rejected with 401");
        Assert.Equal(0L, await CountAuditAsync(correlationId));
    }

    [Fact]
    public async Task StaleAuthorizationStateVersionIsRejectedWith401()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("authz01.stale.administrator", OperatorRole.Administrator);
        await ExecuteNonQueryAsync(
            $"""
             UPDATE {OperatorPersistence.TableName}
             SET {OperatorPersistence.AuthorizationStateVersionColumn} = @version
             WHERE {OperatorPersistence.IdColumn} = @id;
             """,
            ("version", administrator.AuthorizationStateVersion + 1),
            ("id", administrator.Id));

        await using AuthorizationProbeApiFactory factory = new(
            TestJwtConfiguration.SigningKey,
            Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(administrator.Id, 1);
        const string correlationId = "authz-401-stale-version";

        using HttpRequestMessage request = new(HttpMethod.Get, AuthzFeatureController.AdministratorOnlyPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        using HttpResponseMessage response = await client.SendAsync(request);

        await AssertRejectedWithMarkerAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHZ-STATE-02",
            "stale authorization-state version must be rejected with 401");
        Assert.Equal(0L, await CountAuditAsync(correlationId));
    }

    [Fact]
    public async Task JwtRoleClaimIsNotAuthoritativeForPolicyAuthorization()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("authz01.forged.viewer", OperatorRole.Viewer);

        await using AuthorizationProbeApiFactory factory = new(
            TestJwtConfiguration.SigningKey,
            Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(viewer.Id, 1, forgedRoleClaim: OperatorRole.Administrator.ToString());
        const string correlationId = "authz-403-forged-role";

        using HttpRequestMessage request = new(HttpMethod.Get, AuthzFeatureController.AdministratorOnlyPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        using HttpResponseMessage response = await client.SendAsync(request);

        await AssertRejectedWithMarkerAsync(
            response,
            HttpStatusCode.Forbidden,
            "AUTHZ-STATE-03",
            "a JWT role claim must never authorize a policy");
        AuditRow row = await ReadSingleAuditRowAsync(correlationId);
        Assert.Equal(viewer.Id, row.ActorIdentifier);
        Assert.Equal(AuditPersistence.ViewerRoleToken, row.ActorRole);
    }

    [Fact]
    public async Task CurrentDbRoleIsTheAuthorityAcrossRequestsEvenWhenTheJwtWasIssuedEarlier()
    {
        await MigrateAsync();
        Operator teller = await SeedOperatorAsync("authz01.role.change", OperatorRole.Teller);

        await using AuthorizationProbeApiFactory factory = new(
            TestJwtConfiguration.SigningKey,
            Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(teller.Id, 1);

        using HttpResponseMessage before = await SendAsync(
            client,
            HttpMethod.Get,
            AuthzFeatureController.AdministratorOnlyPath,
            token);
        await AssertRejectedAsync(before, HttpStatusCode.Forbidden, "teller-before-role-change");

        await ExecuteNonQueryAsync(
            $"""
             UPDATE {OperatorPersistence.TableName}
             SET {OperatorPersistence.FixedRoleColumn} = @role
             WHERE {OperatorPersistence.IdColumn} = @id;
             """,
            ("role", OperatorPersistence.AdministratorRoleToken),
            ("id", teller.Id));

        using HttpResponseMessage after = await SendAsync(
            client,
            HttpMethod.Get,
            AuthzFeatureController.AdministratorOnlyPath,
            token);
        await AssertHandlerReachedAsync(after);
    }

    [Fact]
    public async Task RequiredAuditPersistenceFailureFailsClosedWith500AndNoEnvelopeLeak()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("authz01.audit.fail", OperatorRole.Viewer);

        await using AuthorizationProbeApiFactory factory = new(
            TestJwtConfiguration.SigningKey,
            Database.ConnectionString,
            services => services.AddScoped<IAuditWriter>(_ => new ThrowingAuditWriter()));
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(viewer.Id, 1);
        const string correlationId = "authz-500-audit-failure";

        using HttpRequestMessage request = new(HttpMethod.Get, AuthzFeatureController.AdministratorOnlyPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal("internal_error", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("An internal error occurred.", document.RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain("handlerReached", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("operation_not_permitted", body, StringComparison.Ordinal);
        Assert.Equal(0L, await CountAuditAsync(correlationId));
    }

    [Fact]
    public async Task LoginIssuedTokenPassesTheFallbackPolicyAndReachesTheProtectedFeature()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("authz01.login.viewer", OperatorRole.Viewer);

        await using AuthorizationProbeApiFactory factory = new(
            TestJwtConfiguration.SigningKey,
            Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage loginResponse = await client.PostAsJsonAsync(
            "/auth/login",
            new { userName = viewer.UserName, password = SeedPlaintextPassword });
        string loginBody = await loginResponse.Content.ReadAsStringAsync();
        AssertNotInternalError(loginResponse, loginBody);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        using JsonDocument loginDocument = JsonDocument.Parse(loginBody);
        string accessToken = loginDocument.RootElement.GetProperty("accessToken").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        using HttpResponseMessage featureResponse = await SendAsync(
            client,
            HttpMethod.Get,
            AuthzFeatureController.DefaultDenyPath,
            accessToken);

        await AssertHandlerReachedAsync(featureResponse);
    }

    [Fact]
    public async Task StaleAuthenticationStateRejectionIsRecordedInTechnicalSecurityLog()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("authz01.stale.log", OperatorRole.Administrator);
        await ExecuteNonQueryAsync(
            $"""
             UPDATE {OperatorPersistence.TableName}
             SET {OperatorPersistence.StateColumn} = @state
             WHERE {OperatorPersistence.IdColumn} = @id;
             """,
            ("state", OperatorPersistence.DisabledStateToken),
            ("id", administrator.Id));

        const string correlationId = "authz-stale-state-log-correlation";
        using ConsoleCapture capture = new();

        await using (AuthorizationProbeApiFactory factory = new(
            TestJwtConfiguration.SigningKey,
            Database.ConnectionString))
        {
            using HttpClient client = factory.CreateClient();
            string token = CreateToken(administrator.Id, 1);

            using HttpRequestMessage request = new(HttpMethod.Get, AuthzFeatureController.AdministratorOnlyPath);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
            using HttpResponseMessage response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        Assert.Contains(
            "presented authentication state is no longer valid",
            capture.Content,
            StringComparison.Ordinal);
        Assert.Contains(correlationId, capture.Content, StringComparison.Ordinal);
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

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string token)
    {
        using HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static async Task AssertHandlerReachedAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        AssertNotInternalError(response, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.GetProperty("handlerReached").GetBoolean());
    }

    private static async Task AssertRejectedAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatusCode,
        string scenario)
    {
        string body = await response.Content.ReadAsStringAsync();
        AssertNotInternalError(response, body);

        Assert.Equal(expectedStatusCode, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        string expectedCode = expectedStatusCode == HttpStatusCode.Unauthorized
            ? "authentication_required"
            : "operation_not_permitted";
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("handlerReached", body, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertRejectedWithMarkerAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatusCode,
        string marker,
        string semantic)
    {
        string body = await response.Content.ReadAsStringAsync();

        if (response.StatusCode != expectedStatusCode)
        {
            throw new Xunit.Sdk.XunitException(
                $"{marker}: expected HTTP {(int)expectedStatusCode}, observed HTTP {(int)response.StatusCode}. " +
                $"Semantic failure: {semantic}. Body: {body}");
        }

        using JsonDocument document = JsonDocument.Parse(body);
        string expectedCode = expectedStatusCode == HttpStatusCode.Unauthorized
            ? "authentication_required"
            : "operation_not_permitted";
        string observedCode = document.RootElement.GetProperty("code").GetString()!;

        if (observedCode != expectedCode)
        {
            throw new Xunit.Sdk.XunitException(
                $"{marker}: expected code '{expectedCode}', observed '{observedCode}'. " +
                $"Semantic failure: {semantic}. Body: {body}");
        }

        Assert.DoesNotContain("handlerReached", body, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertNotInternalError(HttpResponseMessage response, string body)
    {
        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            throw new Xunit.Sdk.XunitException(
                $"PostgreSQL AUTHZ returned HTTP 500; production error contract was not weakened. Body: {body}");
        }
    }

    private static string CreateToken(
        Guid subject,
        int authorizationStateVersion,
        string? forgedRoleClaim = null)
    {
        DateTime now = DateTime.UtcNow;
        List<Claim> claims =
        [
            new Claim(JwtRegisteredClaimNames.Sub, subject.ToString("D")),
            new Claim(
                AuthnClaimTypes.AuthorizationStateVersion,
                authorizationStateVersion.ToString(CultureInfo.InvariantCulture)),
        ];

        if (forgedRoleClaim is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, forgedRoleClaim));
        }

        JwtSecurityToken token = new(
            "minimal-bank-system",
            "minimal-bank-system-api",
            claims,
            now.AddMinutes(-1),
            now.AddMinutes(5),
            new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(TestJwtConfiguration.SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<long> CountAuditAsync(string correlationId)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"SELECT count(*) FROM {AuditPersistence.TableName} WHERE {AuditPersistence.CorrelationIdColumn} = @correlation;",
            connection);
        command.Parameters.AddWithValue("correlation", correlationId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private async Task<AuditRow> ReadSingleAuditRowAsync(string correlationId)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"SELECT {AuditPersistence.ActorIdentifierColumn}, {AuditPersistence.ActorRoleColumn}, " +
            $"{AuditPersistence.OperationIdentifierColumn}, {AuditPersistence.TargetIdentifierColumn}, " +
            $"{AuditPersistence.ResultColumn}, {AuditPersistence.FailureBusinessErrorCodeColumn}, " +
            $"{AuditPersistence.CorrelationIdColumn} " +
            $"FROM {AuditPersistence.TableName} WHERE {AuditPersistence.CorrelationIdColumn} = @correlation;",
            connection);
        command.Parameters.AddWithValue("correlation", correlationId);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected exactly one AUTHZ policy-rejection Audit record for correlation '{correlationId}', but none was written.");
        }

        AuditRow row = new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetString(6));

        if (await reader.ReadAsync())
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected exactly one AUTHZ policy-rejection Audit record for correlation '{correlationId}', but more than one was written.");
        }

        return row;
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

    private sealed record AuditRow(
        Guid ActorIdentifier,
        string ActorRole,
        string OperationIdentifier,
        string TargetIdentifier,
        string Result,
        string? FailureBusinessErrorCode,
        string CorrelationId);

    private sealed class ThrowingAuditWriter : IAuditWriter
    {
        public Task AppendToCurrentTransactionAsync(
            AuditWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            _ = request;
            _ = cancellationToken;
            throw new InvalidOperationException("Deterministic test-only Product Audit persistence failure.");
        }

        public Task<TResult> AppendInSeparateTransactionBeforeResultAsync<TResult>(
            AuditWriteRequest request,
            Func<CancellationToken, Task<TResult>> successResultFactory,
            CancellationToken cancellationToken = default)
        {
            _ = request;
            _ = successResultFactory;
            _ = cancellationToken;
            throw new InvalidOperationException("Deterministic test-only Product Audit persistence failure.");
        }
    }
}
