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
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Auditing;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using MinimalBankSystem.IntegrationTests.Authorization;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// WP2-AUTHZ-01 (#168) non-vacuous verification, run through the real ASP.NET Core authorization
/// pipeline and a real persisted Operator/Audit trail: current-Operator resolution, active/disabled
/// state, authorization-state-version currency, current-DB-role policy authorization (never the
/// JWT role claim), the authenticated 403 policy-rejection Product Audit and its fail-closed
/// boundary.
/// </summary>
[Trait("Category", "PostgreSqlIntegration")]
public sealed class AuthorizationTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string SeedPlaintextPassword = "authz01-integration-seed-password-not-for-production";
    private const string Issuer = "minimal-bank-system";
    private const string Audience = "minimal-bank-system-api";

    private static readonly DateTimeOffset FrozenUtc = new(2033, 8, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AnyCurrentOperatorPolicySucceedsForAnyActiveRoleAndReachesTheHandler()
    {
        await using AuthorizationRuntimeBoundary boundary = await AuthorizationRuntimeBoundary.CreateAsync(this);
        Operator viewer = await boundary.SeedOperatorAsync("authz01.any.viewer", OperatorRole.Viewer);
        using AuthorizationProbeApiFactory factory = boundary.CreateFactory();
        using HttpClient client = factory.CreateClient();
        string token = await LoginAsync(client, viewer.UserName);

        using HttpResponseMessage response = await GetWithBearerAsync(
            client, AuthorizationProbeController.AnyCurrentOperatorPath, token);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.GetProperty("handlerReached").GetBoolean());
        Assert.Equal(viewer.Id.ToString("D"), document.RootElement.GetProperty("subject").GetString());
    }

    [Fact]
    public async Task DefaultDenyProtectsAnEndpointWithNoExplicitAuthorizationMetadata()
    {
        await using AuthorizationRuntimeBoundary boundary = await AuthorizationRuntimeBoundary.CreateAsync(this);
        Operator viewer = await boundary.SeedOperatorAsync("authz01.fallback.no-explicit-policy", OperatorRole.Viewer);
        using AuthorizationProbeApiFactory factory = boundary.CreateFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage anonymousResponse = await client.GetAsync(AuthorizationProbeController.NoExplicitPolicyPath);
        await AssertAuthenticationRequiredAsync(anonymousResponse);

        string token = await LoginAsync(client, viewer.UserName);
        using HttpResponseMessage authenticatedResponse = await GetWithBearerAsync(
            client, AuthorizationProbeController.NoExplicitPolicyPath, token);
        string body = await authenticatedResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, authenticatedResponse.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.GetProperty("handlerReached").GetBoolean());
    }

    [Fact]
    public async Task DisabledOperatorTokenIsRejectedAsUnauthenticatedAndProducesNoProductAudit()
    {
        await using AuthorizationRuntimeBoundary boundary = await AuthorizationRuntimeBoundary.CreateAsync(this);
        Operator operatorRow = await boundary.SeedOperatorAsync("authz01.state.disabled", OperatorRole.Administrator);
        using AuthorizationProbeApiFactory factory = boundary.CreateFactory();
        using HttpClient client = factory.CreateClient();
        string token = await LoginAsync(client, operatorRow.UserName);

        await boundary.SetOperatorStateAsync(operatorRow.Id, OperatorPersistence.DisabledStateToken);

        using HttpResponseMessage response = await GetWithBearerAsync(
            client, AuthorizationProbeController.AnyCurrentOperatorPath, token);

        await AssertAuthenticationRequiredAsync(response);
        Assert.Equal(0L, await boundary.CountAuditRecordsAsync());
    }

    [Fact]
    public async Task AuthorizationStateVersionMismatchIsRejectedAsUnauthenticatedAndProducesNoProductAudit()
    {
        await using AuthorizationRuntimeBoundary boundary = await AuthorizationRuntimeBoundary.CreateAsync(this);
        Operator operatorRow = await boundary.SeedOperatorAsync("authz01.state.stale-version", OperatorRole.Teller);
        using AuthorizationProbeApiFactory factory = boundary.CreateFactory();
        using HttpClient client = factory.CreateClient();
        string token = await LoginAsync(client, operatorRow.UserName);

        // Simulates the ADR-0007 immediate-invalidation contract: a role/state change bumps the
        // authorization-state version, which must invalidate the already-issued token.
        await boundary.BumpAuthorizationStateVersionAsync(operatorRow.Id);

        using HttpResponseMessage response = await GetWithBearerAsync(
            client, AuthorizationProbeController.AnyCurrentOperatorPath, token);

        await AssertAuthenticationRequiredAsync(response);
        Assert.Equal(0L, await boundary.CountAuditRecordsAsync());
    }

    [Fact]
    public async Task InsufficientRoleReturns403WithExactlyOnePolicyRejectionAuditAndHandlerNotReached()
    {
        await using AuthorizationRuntimeBoundary boundary = await AuthorizationRuntimeBoundary.CreateAsync(this);
        Operator viewer = await boundary.SeedOperatorAsync("authz01.role.viewer.insufficient", OperatorRole.Viewer);
        using AuthorizationProbeApiFactory factory = boundary.CreateFactory();
        using HttpClient client = factory.CreateClient();
        string token = await LoginAsync(client, viewer.UserName);

        using HttpResponseMessage response = await GetWithBearerAsync(
            client, AdministratorOnlyPath("authz01-target-insufficient"), token);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal("operation_not_permitted", document.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("handlerReached", body, StringComparison.Ordinal);

        AuditRecord record = await boundary.SingleAuditRecordAsync();
        Assert.Equal(viewer.Id, record.ActorIdentifier);
        Assert.Equal(OperatorRole.Viewer, record.ActorRole);
        Assert.Equal(AuthorizationProbeController.OperationIdentifier, record.OperationIdentifier);
        Assert.Equal("authz01-target-insufficient", record.TargetIdentifier);
        Assert.Equal(AuditResult.Failure, record.Result);
        Assert.Equal("operation_not_permitted", record.FailureBusinessErrorCode);
    }

    [Fact]
    public async Task SufficientRoleReturns200AndReachesTheHandler()
    {
        await using AuthorizationRuntimeBoundary boundary = await AuthorizationRuntimeBoundary.CreateAsync(this);
        Operator administrator = await boundary.SeedOperatorAsync("authz01.role.administrator.sufficient", OperatorRole.Administrator);
        using AuthorizationProbeApiFactory factory = boundary.CreateFactory();
        using HttpClient client = factory.CreateClient();
        string token = await LoginAsync(client, administrator.UserName);

        using HttpResponseMessage response = await GetWithBearerAsync(
            client, AdministratorOnlyPath("authz01-target-sufficient"), token);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.GetProperty("handlerReached").GetBoolean());
        Assert.Equal(0L, await boundary.CountAuditRecordsAsync());
    }

    [Fact]
    public async Task ForgedJwtRoleClaimDoesNotOverrideTheCurrentDatabaseRole()
    {
        await using AuthorizationRuntimeBoundary boundary = await AuthorizationRuntimeBoundary.CreateAsync(this);
        Operator viewer = await boundary.SeedOperatorAsync("authz01.role.viewer.forged-claim", OperatorRole.Viewer);
        using AuthorizationProbeApiFactory factory = boundary.CreateFactory();
        using HttpClient client = factory.CreateClient();

        // A validly signed token (the caller possesses the same shared test signing key) for the
        // real Viewer, carrying a forged "administrator" role claim never issued by AUTHN.
        string forgedToken = CreateToken(
            viewer.Id,
            Operator.InitialAuthorizationStateVersion,
            [new Claim(ClaimTypes.Role, "administrator"), new Claim("role", "administrator")]);

        using HttpResponseMessage response = await GetWithBearerAsync(
            client, AdministratorOnlyPath("authz01-target-forged-role"), forgedToken);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("handlerReached", body, StringComparison.Ordinal);
        AuditRecord record = await boundary.SingleAuditRecordAsync();
        Assert.Equal(OperatorRole.Viewer, record.ActorRole);
    }

    [Fact]
    public async Task RequiredPolicyRejectionAuditFailureFailsClosedWithoutAnUnauditedForbidden()
    {
        await using AuthorizationRuntimeBoundary boundary = await AuthorizationRuntimeBoundary.CreateAsync(this);
        Operator viewer = await boundary.SeedOperatorAsync("authz01.audit.fail-closed", OperatorRole.Viewer);
        using AuthorizationProbeApiFactory factory = boundary.CreateFactory(
            configureServices: AuthorizationProbeApiFactory.UseThrowingAuditWriter);
        using HttpClient client = factory.CreateClient();
        string token = await LoginAsync(client, viewer.UserName);

        using HttpResponseMessage response = await GetWithBearerAsync(
            client, AdministratorOnlyPath("authz01-target-audit-failure"), token);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal("internal_error", document.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("Deterministic test-only", body, StringComparison.Ordinal);
        Assert.Equal(0L, await boundary.CountAuditRecordsAsync());
    }

    [Fact]
    public async Task MissingAuditOperationContextFailsClosedInsteadOfAnUnauditedForbidden()
    {
        await using AuthorizationRuntimeBoundary boundary = await AuthorizationRuntimeBoundary.CreateAsync(this);
        Operator viewer = await boundary.SeedOperatorAsync("authz01.audit.missing-context", OperatorRole.Viewer);
        using AuthorizationProbeApiFactory factory = boundary.CreateFactory();
        using HttpClient client = factory.CreateClient();
        string token = await LoginAsync(client, viewer.UserName);

        using HttpResponseMessage response = await GetWithBearerAsync(
            client, AuthorizationProbeController.UnauditedRejectionPath, token);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal("internal_error", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(0L, await boundary.CountAuditRecordsAsync());
    }

    private static string AdministratorOnlyPath(string targetId) =>
        $"/__authz-probe/administrator-only/{targetId}";

    private static async Task<string> LoginAsync(HttpClient client, string userName)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/login",
            new { userName, password = SeedPlaintextPassword });
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected a successful AUTHN login for '{userName}'. Status: {response.StatusCode}. Body: {body}");

        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> GetWithBearerAsync(HttpClient client, string path, string token)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static async Task AssertAuthenticationRequiredAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal("authentication_required", document.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("handlerReached", body, StringComparison.Ordinal);
    }

    private static string CreateToken(Guid subject, int authorizationStateVersion, IEnumerable<Claim> extraClaims)
    {
        DateTime now = DateTime.UtcNow;
        List<Claim> claims =
        [
            new Claim(JwtRegisteredClaimNames.Sub, subject.ToString("D")),
            new Claim(
                AuthnClaimTypes.AuthorizationStateVersion,
                authorizationStateVersion.ToString(CultureInfo.InvariantCulture)),
            .. extraClaims,
        ];

        JwtSecurityToken token = new(
            Issuer,
            Audience,
            claims,
            now.AddMinutes(-1),
            now.AddMinutes(5),
            new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(TestJwtConfiguration.SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class AuthorizationRuntimeBoundary : IAsyncDisposable
    {
        private readonly AuthorizationTests owner;

        private AuthorizationRuntimeBoundary(AuthorizationTests owner)
        {
            this.owner = owner;
        }

        public static async Task<AuthorizationRuntimeBoundary> CreateAsync(AuthorizationTests owner)
        {
            AuthorizationRuntimeBoundary boundary = new(owner);
            MigratorRun run = await MigratorProcess.RunAsync(owner.Database.ConnectionString, TimeSpan.FromSeconds(120));
            Assert.True(
                run.ExitCode == MigratorExitCode.Success,
                $"Expected AUTHZ verification migration success. Output:\n{run.Output}");
            return boundary;
        }

        public AuthorizationProbeApiFactory CreateFactory(Action<IServiceCollection>? configureServices = null) =>
            new(TestJwtConfiguration.SigningKey, owner.Database.ConnectionString, configureServices);

        public async Task<Operator> SeedOperatorAsync(string userName, OperatorRole role)
        {
            Operator created = OperatorFactory.Create(
                userName,
                SeedPlaintextPassword,
                role,
                FrozenUtc,
                Guid.NewGuid().ToString());

            DbContextOptionsBuilder<BankDbContext> options = new();
            options.UseBankPostgreSql(owner.Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
            await using BankDbContext context = new(options.Options);
            context.Operators.Add(created);
            await context.SaveChangesAsync();

            return created;
        }

        public Task SetOperatorStateAsync(Guid operatorId, string stateToken) =>
            ExecuteNonQueryAsync(
                $"""
                 UPDATE {OperatorPersistence.TableName}
                 SET {OperatorPersistence.StateColumn} = @state
                 WHERE {OperatorPersistence.IdColumn} = @id;
                 """,
                ("state", stateToken),
                ("id", operatorId));

        public Task BumpAuthorizationStateVersionAsync(Guid operatorId) =>
            ExecuteNonQueryAsync(
                $"""
                 UPDATE {OperatorPersistence.TableName}
                 SET {OperatorPersistence.AuthorizationStateVersionColumn}
                     = {OperatorPersistence.AuthorizationStateVersionColumn} + 1
                 WHERE {OperatorPersistence.IdColumn} = @id;
                 """,
                ("id", operatorId));

        public async Task<long> CountAuditRecordsAsync()
        {
            await using NpgsqlConnection connection = new(owner.Database.ConnectionString);
            await connection.OpenAsync();
            await using NpgsqlCommand command = new($"SELECT count(*) FROM {AuditPersistence.TableName};", connection);
            return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        }

        public async Task<AuditRecord> SingleAuditRecordAsync()
        {
            DbContextOptionsBuilder<BankDbContext> options = new();
            options.UseBankPostgreSql(owner.Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
            await using BankDbContext context = new(options.Options);
            return await context.AuditRecords.AsNoTracking().SingleAsync();
        }

        private async Task ExecuteNonQueryAsync(string commandText, params (string Name, object Value)[] parameters)
        {
            await using NpgsqlConnection connection = new(owner.Database.ConnectionString);
            await connection.OpenAsync();
            await using NpgsqlCommand command = new(commandText, connection);
            foreach ((string name, object value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }

            await command.ExecuteNonQueryAsync();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
