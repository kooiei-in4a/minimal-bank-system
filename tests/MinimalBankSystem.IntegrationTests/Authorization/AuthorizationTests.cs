using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Auditing;
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
/// Issue #168 verification requirements 1-11 through the real ASP.NET Core authentication and
/// authorization pipeline. The endpoints, Audit operation, and failure writer are test-only.
/// </summary>
[Collection(TestExecutionCollections.ConsoleSensitive)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class AuthorizationTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string PlaintextPassword = "authz01-integration-seed-password-not-for-production";
    private const string AdministratorJwtRole = "Administrator";
    private const string ViewerJwtRole = "Viewer";
    private static readonly DateTimeOffset FrozenUtc = new(2033, 6, 7, 8, 9, 10, TimeSpan.Zero);

    [Fact]
    public async Task ExplicitAnonymousEndpointsAndFramework404And405ContractsArePreserved()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("authz01.anonymous.viewer", OperatorRole.Viewer);

        await using AuthorizationProbeApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);

        using HttpResponseMessage ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        using HttpResponseMessage login = await client.PostAsJsonAsync(
            "/auth/login",
            new { userName = viewer.UserName, password = PlaintextPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using HttpResponseMessage missing = await client.GetAsync("/__authz-probe/not-a-route");
        await AssertErrorAsync(
            missing,
            HttpStatusCode.NotFound,
            "endpoint_not_found",
            "AUTHZ fallback replaced the framework 404 contract.");

        using HttpResponseMessage wrongMethod = await client.PostAsync(
            Route(AuthorizationProbeController.FallbackRoute, "method-target"),
            content: null);
        await AssertErrorAsync(
            wrongMethod,
            HttpStatusCode.MethodNotAllowed,
            "method_not_allowed",
            "AUTHZ fallback replaced the framework 405 contract.");
    }

    [Fact]
    public async Task NonRouteEndpointBypassHappensBeforeCurrentOperatorDbLookupAndDoesNotBypassRouteEndpoints()
    {
        await MigrateAsync();
        using ConsoleCapture capture = new();

        await using AuthorizationProbeApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        AuthorizationProbeSignals signals = factory.Services.GetRequiredService<AuthorizationProbeSignals>();
        string missingOperatorToken = CreateToken(Guid.NewGuid(), Operator.InitialAuthorizationStateVersion);

        using HttpResponseMessage missing = await SendWithTokenAsync(
            client,
            "/__authz-probe/not-a-route",
            missingOperatorToken);
        await AssertErrorAsync(
            missing,
            HttpStatusCode.NotFound,
            "endpoint_not_found",
            "Authenticated unmatched route must remain 404 without current-Operator resolution.");

        // POST against a GET-only action becomes a framework non-RouteEndpoint 405 endpoint.
        using HttpRequestMessage post = new(
            HttpMethod.Post,
            Route(AuthorizationProbeController.FallbackRoute, "routing-bypass"));
        post.Headers.Authorization = new AuthenticationHeaderValue("Bearer", missingOperatorToken);
        using HttpResponseMessage methodNotAllowed = await client.SendAsync(post);
        await AssertErrorAsync(
            methodNotAllowed,
            HttpStatusCode.MethodNotAllowed,
            "method_not_allowed",
            "Authenticated unsupported method must remain 405 without current-Operator resolution.");

        Assert.DoesNotContain("OperatorNotFound", capture.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("2001", capture.Content, StringComparison.Ordinal);

        Operator viewer = await SeedOperatorAsync("authz01.routing.viewer", OperatorRole.Viewer);
        using HttpResponseMessage allowed = await SendWithTokenAsync(
            client,
            Route(AuthorizationProbeController.FallbackRoute, "route-endpoint-not-bypassed"),
            CreateToken(viewer));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(1, signals.FallbackHandlerReachCount);
    }

    [Fact]
    public async Task Framework415BypassHappensBeforeCurrentOperatorDbLookupWithoutHandlerOrProductAudit()
    {
        // AUTHZ-H1-MAJ-01: FS-AUTHZ-03 requires explicit 415 proof through the AUTHZ-enabled path.
        await MigrateAsync();
        using ConsoleCapture capture = new();

        await using AuthorizationProbeApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        AuthorizationProbeSignals signals = factory.Services.GetRequiredService<AuthorizationProbeSignals>();
        string missingOperatorToken = CreateToken(Guid.NewGuid(), Operator.InitialAuthorizationStateVersion);

        using HttpRequestMessage unsupportedMediaType = new(
            HttpMethod.Post,
            Route(AuthorizationProbeController.MediaTypeRoute, "unsupported-media-type"));
        unsupportedMediaType.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", missingOperatorToken);
        unsupportedMediaType.Content = new StringContent("not-json", Encoding.UTF8, "text/plain");

        using HttpResponseMessage response = await client.SendAsync(unsupportedMediaType);
        await AssertErrorAsync(
            response,
            HttpStatusCode.UnsupportedMediaType,
            "unsupported_media_type",
            "Authenticated unsupported media type must remain 415 without current-Operator resolution.");

        Assert.Equal(0, signals.MediaTypeHandlerReachCount);
        Assert.DoesNotContain("OperatorNotFound", capture.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("2001", capture.Content, StringComparison.Ordinal);
        Assert.Equal(0L, await CountAuditsAsync());
    }

    [Fact]
    public async Task FallbackDenyRejectsMissingAndInvalidBearerBeforeHandlerWithoutProductAudit()
    {
        await MigrateAsync();
        await using AuthorizationProbeApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        AuthorizationProbeSignals signals = factory.Services.GetRequiredService<AuthorizationProbeSignals>();
        string path = Route(AuthorizationProbeController.FallbackRoute, "fallback-denied");

        using HttpResponseMessage missing = await client.GetAsync(path);
        await AssertAuthenticationRequiredAsync(missing, "Missing bearer was not denied by fallback authorization.");

        using HttpRequestMessage invalidRequest = new(HttpMethod.Get, path);
        invalidRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");
        using HttpResponseMessage invalid = await client.SendAsync(invalidRequest);
        await AssertAuthenticationRequiredAsync(invalid, "Invalid bearer was not denied by fallback authorization.");

        Assert.Equal(0, signals.FallbackHandlerReachCount);
        Assert.Equal(0L, await CountAuditsAsync());
    }

    [Fact]
    public async Task FallbackAllowsAnActiveCurrentOperatorWithMatchingAuthorizationState()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("authz01.fallback.viewer", OperatorRole.Viewer);
        await using AuthorizationProbeApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        AuthorizationProbeSignals signals = factory.Services.GetRequiredService<AuthorizationProbeSignals>();

        using HttpResponseMessage response = await SendWithTokenAsync(
            client,
            Route(AuthorizationProbeController.FallbackRoute, "fallback-allowed"),
            CreateToken(viewer));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, signals.FallbackHandlerReachCount);
        Assert.Equal(0L, await CountAuditsAsync());
    }

    [Fact]
    public async Task MissingOperatorReturns401WithoutProductAuditAndEmitsTechnicalSecurityLog()
    {
        await MigrateAsync();
        using ConsoleCapture capture = new();

        await using (AuthorizationProbeApiFactory factory = new(Database.ConnectionString))
        using (HttpClient client = factory.CreateClient())
        using (HttpResponseMessage response = await SendWithTokenAsync(
                   client,
                   Route(AuthorizationProbeController.FallbackRoute, "missing-operator"),
                   CreateToken(Guid.NewGuid(), Operator.InitialAuthorizationStateVersion)))
        {
            await AssertAuthenticationRequiredAsync(response, "A missing current Operator did not invalidate the bearer.");
            Assert.Equal(
                0,
                factory.Services.GetRequiredService<AuthorizationProbeSignals>().FallbackHandlerReachCount);
        }

        Assert.Equal(0L, await CountAuditsAsync());
        Assert.Contains("OperatorNotFound", capture.Content, StringComparison.Ordinal);
        Assert.Contains("2001", capture.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisabledOperatorReturns401WithoutReachingHandlerOrWritingProductAudit()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("authz01.disabled.viewer", OperatorRole.Viewer);
        await UpdateOperatorAsync(
            viewer.Id,
            $"{OperatorPersistence.StateColumn} = @value",
            OperatorPersistence.DisabledStateToken);

        await using AuthorizationProbeApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        AuthorizationProbeSignals signals = factory.Services.GetRequiredService<AuthorizationProbeSignals>();

        using HttpResponseMessage response = await SendWithTokenAsync(
            client,
            Route(AuthorizationProbeController.FallbackRoute, "disabled-operator"),
            CreateToken(viewer));

        if (response.StatusCode == HttpStatusCode.OK || signals.FallbackHandlerReachCount != 0)
        {
            throw new Xunit.Sdk.XunitException(
                "AUTHZ-STATE-01: disabled Operator bypass reached the feature handler.");
        }

        await AssertAuthenticationRequiredAsync(response, "Disabled Operator was not mapped to 401.");
        Assert.Equal(0L, await CountAuditsAsync());
    }

    [Fact]
    public async Task AuthorizationStateVersionMismatchReturns401WithoutHandlerOrProductAudit()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("authz01.version.viewer", OperatorRole.Viewer);

        await using AuthorizationProbeApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        AuthorizationProbeSignals signals = factory.Services.GetRequiredService<AuthorizationProbeSignals>();

        using HttpResponseMessage response = await SendWithTokenAsync(
            client,
            Route(AuthorizationProbeController.FallbackRoute, "version-mismatch"),
            CreateToken(viewer.Id, viewer.AuthorizationStateVersion + 1));

        if (response.StatusCode == HttpStatusCode.OK || signals.FallbackHandlerReachCount != 0)
        {
            throw new Xunit.Sdk.XunitException(
                "AUTHZ-STATE-02: authorization-state-version bypass reached the feature handler.");
        }

        await AssertAuthenticationRequiredAsync(response, "Stale authorization-state version was not mapped to 401.");
        Assert.Equal(0L, await CountAuditsAsync());
    }

    [Fact]
    public async Task CurrentDatabaseRoleOverridesJwtRoleClaimsInBothDirections()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("authz01.role.viewer", OperatorRole.Viewer);
        Operator administrator = await SeedOperatorAsync("authz01.role.admin", OperatorRole.Administrator);

        await using AuthorizationProbeApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        AuthorizationProbeSignals signals = factory.Services.GetRequiredService<AuthorizationProbeSignals>();

        using HttpResponseMessage staleElevatedJwt = await SendWithTokenAsync(
            client,
            Route(AuthorizationProbeController.AdministratorRoute, "jwt-admin-db-viewer"),
            CreateToken(viewer, AdministratorJwtRole),
            "authz-state-03-elevated");

        if (staleElevatedJwt.StatusCode == HttpStatusCode.OK || signals.AdministratorHandlerReachCount != 0)
        {
            throw new Xunit.Sdk.XunitException(
                "AUTHZ-STATE-03: JWT administrator role became authoritative over the current DB viewer role.");
        }

        await AssertErrorAsync(
            staleElevatedJwt,
            HttpStatusCode.Forbidden,
            "operation_not_permitted",
            "Current DB viewer role was not denied as an authenticated 403.");

        using HttpResponseMessage staleReducedJwt = await SendWithTokenAsync(
            client,
            Route(AuthorizationProbeController.AdministratorRoute, "jwt-viewer-db-admin"),
            CreateToken(administrator, ViewerJwtRole));

        Assert.Equal(HttpStatusCode.OK, staleReducedJwt.StatusCode);
        Assert.Equal(1, signals.AdministratorHandlerReachCount);
    }

    [Fact]
    public async Task AuthenticatedPolicyRejectionDoesNotReachHandlerAndAuditsExactlyOnceBefore403()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("authz01.audit.viewer", OperatorRole.Viewer);
        const string target = "audit-target";
        const string correlationId = "authz01-policy-rejection";

        await using AuthorizationProbeApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        AuthorizationProbeSignals signals = factory.Services.GetRequiredService<AuthorizationProbeSignals>();

        using HttpResponseMessage response = await SendWithTokenAsync(
            client,
            Route(AuthorizationProbeController.AdministratorRoute, target),
            CreateToken(viewer),
            correlationId);

        await AssertErrorAsync(
            response,
            HttpStatusCode.Forbidden,
            "operation_not_permitted",
            "Authenticated insufficient role did not produce the approved 403 contract.");
        Assert.Equal(0, signals.AdministratorHandlerReachCount);

        PersistedPolicyAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(viewer.Id, audit.ActorIdentifier);
        Assert.Equal(AuditPersistence.ViewerRoleToken, audit.ActorRole);
        Assert.Equal(TestAuthorizationAuditContextAttribute.Operation, audit.OperationIdentifier);
        Assert.Equal(target, audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.FailureResultToken, audit.Result);
        Assert.Equal("operation_not_permitted", audit.FailureBusinessErrorCode);
        Assert.Equal(correlationId, audit.CorrelationId);
    }

    [Fact]
    public async Task RequiredPolicyRejectionAuditFailureFailsClosedWithout403OrHandlerReach()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("authz01.audit-failure.viewer", OperatorRole.Viewer);
        AuthorizationAuditFailureProbe failureProbe = new();

        await using AuthorizationProbeApiFactory factory = new(
            Database.ConnectionString,
            services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton(failureProbe);
                services.AddScoped<IAuditWriter, FailingAuthorizationAuditWriter>();
            });
        using HttpClient client = factory.CreateClient();
        AuthorizationProbeSignals signals = factory.Services.GetRequiredService<AuthorizationProbeSignals>();

        using HttpResponseMessage response = await SendWithTokenAsync(
            client,
            Route(AuthorizationProbeController.AdministratorRoute, "audit-failure"),
            CreateToken(viewer),
            "authz01-audit-failure");

        await AssertErrorAsync(
            response,
            HttpStatusCode.InternalServerError,
            "internal_error",
            "Required 403 Product Audit failure returned an unaudited 403 instead of failing closed.");
        Assert.Equal(1, failureProbe.InvocationCount);
        Assert.Equal(0, signals.AdministratorHandlerReachCount);
        Assert.Equal(0L, await CountAuditsAsync());
    }

    [Fact]
    public async Task MissingFeatureAuditContextFailsClosedInsteadOfReturningUnaudited403()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("authz01.missing-context.viewer", OperatorRole.Viewer);

        await using AuthorizationProbeApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        AuthorizationProbeSignals signals = factory.Services.GetRequiredService<AuthorizationProbeSignals>();

        using HttpResponseMessage response = await SendWithTokenAsync(
            client,
            Route(AuthorizationProbeController.MissingAuditContextRoute, "missing-context"),
            CreateToken(viewer));

        await AssertErrorAsync(
            response,
            HttpStatusCode.InternalServerError,
            "internal_error",
            "Missing feature-owned Audit context returned an unaudited 403.");
        Assert.Equal(0, signals.MissingAuditContextHandlerReachCount);
        Assert.Equal(0L, await CountAuditsAsync());
    }

    [Fact]
    public async Task AuthnProbeRemainsAuthenticationOnlyAndAuthzSurfaceIsProductionUnreachable()
    {
        using AuthenticationProbeApiFactory authnFactory = new(TestJwtConfiguration.SigningKey);
        using HttpClient authnClient = authnFactory.CreateClient();
        using HttpResponseMessage authnResponse = await SendWithTokenAsync(
            authnClient,
            AuthenticationProbeController.ProbePath,
            CreateToken(Guid.NewGuid(), Operator.InitialAuthorizationStateVersion));

        Assert.Equal(HttpStatusCode.OK, authnResponse.StatusCode);

        string programSource = File.ReadAllText(Path.Combine(
            RepositoryLayout.RepositoryRoot.FullName,
            "src",
            "MinimalBankSystem.Api",
            "Program.cs"));

        Assert.DoesNotContain(nameof(AuthorizationProbeController), programSource, StringComparison.Ordinal);
        Assert.DoesNotContain("__authz-probe", programSource, StringComparison.Ordinal);
        Assert.DoesNotContain(TestAuthorizationAuditContextAttribute.Operation, programSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsEnvironment(\"Testing\")", programSource, StringComparison.Ordinal);
    }

    private async Task MigrateAsync()
    {
        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, TimeSpan.FromSeconds(120));
        Assert.True(
            run.ExitCode == MigratorExitCode.Success,
            $"Expected AUTHZ test migration success. Output:{Environment.NewLine}{run.Output}");
    }

    private async Task<Operator> SeedOperatorAsync(string userName, OperatorRole role)
    {
        Operator created = OperatorFactory.Create(
            userName,
            PlaintextPassword,
            role,
            FrozenUtc,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        await using BankDbContext context = new(options.Options);
        context.Operators.Add(created);
        await context.SaveChangesAsync();
        return created;
    }

    private async Task UpdateOperatorAsync(Guid operatorId, string assignment, object value)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"UPDATE {OperatorPersistence.TableName} SET {assignment} WHERE {OperatorPersistence.IdColumn} = @id;",
            connection);
        command.Parameters.AddWithValue("value", value);
        command.Parameters.AddWithValue("id", operatorId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private async Task<long> CountAuditsAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"SELECT count(*) FROM {AuditPersistence.TableName};",
            connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private async Task<IReadOnlyList<PersistedPolicyAudit>> ReadAuditsAsync(string correlationId)
    {
        List<PersistedPolicyAudit> records = [];
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"""
             SELECT
                 {AuditPersistence.ActorIdentifierColumn},
                 {AuditPersistence.ActorRoleColumn},
                 {AuditPersistence.OperationIdentifierColumn},
                 {AuditPersistence.TargetIdentifierColumn},
                 {AuditPersistence.ResultColumn},
                 {AuditPersistence.FailureBusinessErrorCodeColumn},
                 {AuditPersistence.CorrelationIdColumn}
             FROM {AuditPersistence.TableName}
             WHERE {AuditPersistence.CorrelationIdColumn} = @correlation_id;
             """,
            connection);
        command.Parameters.AddWithValue("correlation_id", correlationId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            records.Add(new PersistedPolicyAudit(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6)));
        }

        return records;
    }

    private static string CreateToken(Operator operatorEntity, string? jwtRole = null) =>
        CreateToken(operatorEntity.Id, operatorEntity.AuthorizationStateVersion, jwtRole);

    private static string CreateToken(Guid operatorId, int authorizationStateVersion, string? jwtRole = null)
    {
        DateTime now = DateTime.UtcNow;
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, operatorId.ToString("D")),
            new(
                AuthnClaimTypes.AuthorizationStateVersion,
                authorizationStateVersion.ToString(CultureInfo.InvariantCulture)),
        ];

        if (jwtRole is not null)
        {
            claims.Add(new Claim("role", jwtRole));
        }

        JwtSecurityToken token = new(
            issuer: "minimal-bank-system",
            audience: "minimal-bank-system-api",
            claims: claims,
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(TestJwtConfiguration.SigningKey)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<HttpResponseMessage> SendWithTokenAsync(
        HttpClient client,
        string path,
        string token,
        string? correlationId = null)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (correlationId is not null)
        {
            request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        }

        return await client.SendAsync(request);
    }

    private static async Task AssertAuthenticationRequiredAsync(
        HttpResponseMessage response,
        string semanticFailure)
    {
        await AssertErrorAsync(
            response,
            HttpStatusCode.Unauthorized,
            "authentication_required",
            semanticFailure);
    }

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode,
        string semanticFailure)
    {
        string body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != expectedStatus)
        {
            throw new Xunit.Sdk.XunitException(
                $"{semanticFailure} Expected {(int)expectedStatus}, got {(int)response.StatusCode}. Body: {body}");
        }

        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    private static string Route(string routeTemplate, string targetIdentifier) =>
        routeTemplate.Replace("{targetId}", targetIdentifier, StringComparison.Ordinal);

    private sealed record PersistedPolicyAudit(
        Guid ActorIdentifier,
        string ActorRole,
        string OperationIdentifier,
        string TargetIdentifier,
        string Result,
        string FailureBusinessErrorCode,
        string CorrelationId);
}

internal sealed class AuthorizationAuditFailureProbe
{
    private int invocationCount;

    public int InvocationCount => Volatile.Read(ref invocationCount);

    public void RecordInvocation() => Interlocked.Increment(ref invocationCount);
}

internal sealed class FailingAuthorizationAuditWriter(AuthorizationAuditFailureProbe failureProbe)
    : IAuditWriter
{
    public Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        throw new NotSupportedException("AUTHZ policy rejection uses the separate transaction primitive.");
    }

    public Task<TResult> AppendInSeparateTransactionBeforeResultAsync<TResult>(
        AuditWriteRequest request,
        Func<CancellationToken, Task<TResult>> successResultFactory,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = successResultFactory;
        _ = cancellationToken;
        failureProbe.RecordInvocation();
        throw new AuthorizationAuditFailureInjectionException();
    }
}

internal sealed class AuthorizationAuditFailureInjectionException()
    : InvalidOperationException("Deterministic test-only AUTHZ Product Audit failure.");
