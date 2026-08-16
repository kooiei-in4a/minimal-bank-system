extern alias api;

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Auditing;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.IntegrationTests.PostgreSql;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.OperatorQuery;

/// <summary>
/// Issue #169 (WP2-OPR-QRY-01) verification requirements 1-10 through the real ASP.NET Core
/// authentication, authorization, persistence, and Product Audit pipeline against the real
/// production <c>/operators</c> and <c>/operators/{id}</c> endpoints.
/// </summary>
[Trait("Category", "PostgreSqlIntegration")]
public sealed class OperatorQueryTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string PlaintextPassword = "oprqry01-integration-seed-password-not-for-production";
    private const string ListOperation = "operator.query.list";
    private const string DetailOperation = "operator.query.detail";
    private const string ListTarget = "operators";

    private static readonly DateTimeOffset FrozenUtc = new(2034, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [Fact]
    public async Task AdminListSucceedsWithApprovedProjectionAndAuditsExactlyOnce()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprqry01.list.admin", OperatorRole.Administrator);
        Operator viewer = await SeedOperatorAsync("oprqry01.list.viewer", OperatorRole.Viewer);
        const string correlationId = "oprqry01-list-success";

        await using OperatorQueryApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await SendWithTokenAsync(
            client,
            HttpMethod.Get,
            "/operators",
            CreateToken(administrator),
            correlationId);

        string body = await response.Content.ReadAsStringAsync();
        AssertNotInternalError(response, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement.ArrayEnumerator elements = document.RootElement.EnumerateArray();
        Dictionary<Guid, JsonElement> byId = [];
        foreach (JsonElement element in elements)
        {
            byId[Guid.Parse(element.GetProperty("id").GetString()!)] = element;
        }

        Assert.True(byId.ContainsKey(administrator.Id));
        Assert.True(byId.ContainsKey(viewer.Id));

        JsonElement viewerElement = byId[viewer.Id];
        Assert.Equal("Active", viewerElement.GetProperty("state").GetString());
        Assert.Equal("Viewer", viewerElement.GetProperty("role").GetString());
        AssertOnlyApprovedFields(viewerElement);
        AssertNonDisclosurePositiveControl(body, viewer);

        List<PersistedAudit> audits = await ReadAuditsAsync(correlationId);
        PersistedAudit audit = Assert.Single(audits);
        Assert.Equal(administrator.Id, audit.ActorIdentifier);
        Assert.Equal(AuditPersistence.AdministratorRoleToken, audit.ActorRole);
        Assert.Equal(ListOperation, audit.OperationIdentifier);
        Assert.Equal(ListTarget, audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.SuccessResultToken, audit.Result);
        Assert.Null(audit.FailureBusinessErrorCode);
    }

    [Fact]
    public async Task AdminDetailSucceedsWithApprovedProjectionAndAuditsExactlyOnce()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprqry01.detail.admin", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("oprqry01.detail.target", OperatorRole.Teller);
        const string correlationId = "oprqry01-detail-success";

        await using OperatorQueryApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await SendWithTokenAsync(
            client,
            HttpMethod.Get,
            $"/operators/{target.Id:D}",
            CreateToken(administrator),
            correlationId);

        string body = await response.Content.ReadAsStringAsync();
        AssertNotInternalError(response, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        Assert.Equal(target.Id, Guid.Parse(root.GetProperty("id").GetString()!));
        Assert.Equal("Active", root.GetProperty("state").GetString());
        Assert.Equal("Teller", root.GetProperty("role").GetString());
        AssertOnlyApprovedFields(root);
        AssertNonDisclosurePositiveControl(body, target);

        List<PersistedAudit> audits = await ReadAuditsAsync(correlationId);
        PersistedAudit audit = Assert.Single(audits);
        Assert.Equal(administrator.Id, audit.ActorIdentifier);
        Assert.Equal(DetailOperation, audit.OperationIdentifier);
        Assert.Equal(target.Id.ToString("D"), audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.SuccessResultToken, audit.Result);
        Assert.Null(audit.FailureBusinessErrorCode);
    }

    [Fact]
    public async Task RequiredListSuccessAuditFailurePreventsSuccessDataAndFailsClosed()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprqry01.list.audit-failure.admin", OperatorRole.Administrator);
        AuditFailureProbe failureProbe = new();

        await using OperatorQueryApiFactory factory = new(
            Database.ConnectionString,
            services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton(failureProbe);
                services.AddScoped<IAuditWriter, FailingAuditWriter>();
            });
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await SendWithTokenAsync(
            client,
            HttpMethod.Get,
            "/operators",
            CreateToken(administrator),
            "oprqry01-list-audit-failure");

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("oprqry01.list.audit-failure.admin", body, StringComparison.Ordinal);
        Assert.Equal(1, failureProbe.InvocationCount);
        Assert.Equal(0L, await CountAuditsAsync());
    }

    [Fact]
    public async Task RequiredDetailSuccessAuditFailurePreventsSuccessDataAndFailsClosed()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprqry01.detail.audit-failure.admin", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("oprqry01.detail.audit-failure.target", OperatorRole.Viewer);
        AuditFailureProbe failureProbe = new();

        await using OperatorQueryApiFactory factory = new(
            Database.ConnectionString,
            services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton(failureProbe);
                services.AddScoped<IAuditWriter, FailingAuditWriter>();
            });
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await SendWithTokenAsync(
            client,
            HttpMethod.Get,
            $"/operators/{target.Id:D}",
            CreateToken(administrator),
            "oprqry01-detail-audit-failure");

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("oprqry01.detail.audit-failure.target", body, StringComparison.Ordinal);
        Assert.Equal(1, failureProbe.InvocationCount);
        Assert.Equal(0L, await CountAuditsAsync());
    }

    [Fact]
    public async Task UnauthenticatedRequestsReturn401WithoutHandlerReachOrProductAudit()
    {
        await MigrateAsync();
        await using OperatorQueryApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage listResponse = await client.GetAsync("/operators");
        await AssertAuthenticationRequiredAsync(listResponse);

        using HttpResponseMessage detailResponse = await client.GetAsync($"/operators/{Guid.NewGuid():D}");
        await AssertAuthenticationRequiredAsync(detailResponse);

        Assert.Equal(0L, await CountAuditsAsync());
    }

    [Fact]
    public async Task AuthenticatedNonAdminReturns403WithHandlerNotReachedAndExactlyOneAuthzAudit()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("oprqry01.forbidden.viewer", OperatorRole.Viewer);
        Operator otherTarget = await SeedOperatorAsync("oprqry01.forbidden.target", OperatorRole.Teller);
        const string listCorrelation = "oprqry01-forbidden-list";
        const string detailCorrelation = "oprqry01-forbidden-detail";

        await using OperatorQueryApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage listResponse = await SendWithTokenAsync(
            client,
            HttpMethod.Get,
            "/operators",
            CreateToken(viewer),
            listCorrelation);
        await AssertErrorAsync(listResponse, HttpStatusCode.Forbidden, "operation_not_permitted");

        List<PersistedAudit> listAudits = await ReadAuditsAsync(listCorrelation);
        PersistedAudit listAudit = Assert.Single(listAudits);
        Assert.Equal(ListOperation, listAudit.OperationIdentifier);
        Assert.Equal(ListTarget, listAudit.TargetIdentifier);
        Assert.Equal(AuditPersistence.FailureResultToken, listAudit.Result);
        Assert.Equal("operation_not_permitted", listAudit.FailureBusinessErrorCode);

        using HttpResponseMessage detailResponse = await SendWithTokenAsync(
            client,
            HttpMethod.Get,
            $"/operators/{otherTarget.Id:D}",
            CreateToken(viewer),
            detailCorrelation);
        await AssertErrorAsync(detailResponse, HttpStatusCode.Forbidden, "operation_not_permitted");

        List<PersistedAudit> detailAudits = await ReadAuditsAsync(detailCorrelation);
        PersistedAudit detailAudit = Assert.Single(detailAudits);
        Assert.Equal(DetailOperation, detailAudit.OperationIdentifier);
        Assert.Equal(otherTarget.Id.ToString("D"), detailAudit.TargetIdentifier);
        Assert.Equal(AuditPersistence.FailureResultToken, detailAudit.Result);

        Assert.Equal(2L, await CountAuditsAsync());
    }

    [Fact]
    public async Task MissingDetailOperatorReturns404WithExactlyOneRejectionAudit()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprqry01.missing.admin", OperatorRole.Administrator);
        Guid missingId = Guid.NewGuid();
        const string correlationId = "oprqry01-missing-detail";

        await using OperatorQueryApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await SendWithTokenAsync(
            client,
            HttpMethod.Get,
            $"/operators/{missingId:D}",
            CreateToken(administrator),
            correlationId);

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "operator_not_found");

        List<PersistedAudit> audits = await ReadAuditsAsync(correlationId);
        PersistedAudit audit = Assert.Single(audits);
        Assert.Equal(administrator.Id, audit.ActorIdentifier);
        Assert.Equal(DetailOperation, audit.OperationIdentifier);
        Assert.Equal(missingId.ToString("D"), audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.FailureResultToken, audit.Result);
        Assert.Equal("operator_not_found", audit.FailureBusinessErrorCode);
    }

    [Fact]
    public async Task NoPolicyOrHandlerDoubleAuditOccursForAnySingleRequest()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprqry01.no-double.admin", OperatorRole.Administrator);
        Operator viewer = await SeedOperatorAsync("oprqry01.no-double.viewer", OperatorRole.Viewer);
        const string adminCorrelation = "oprqry01-no-double-admin";
        const string viewerCorrelation = "oprqry01-no-double-viewer";

        await using OperatorQueryApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage adminList = await SendWithTokenAsync(
            client, HttpMethod.Get, "/operators", CreateToken(administrator), adminCorrelation);
        Assert.Equal(HttpStatusCode.OK, adminList.StatusCode);
        Assert.Single(await ReadAuditsAsync(adminCorrelation));

        using HttpResponseMessage viewerList = await SendWithTokenAsync(
            client, HttpMethod.Get, "/operators", CreateToken(viewer), viewerCorrelation);
        Assert.Equal(HttpStatusCode.Forbidden, viewerList.StatusCode);
        Assert.Single(await ReadAuditsAsync(viewerCorrelation));

        Assert.Equal(2L, await CountAuditsAsync());
    }

    private static void AssertOnlyApprovedFields(JsonElement element)
    {
        HashSet<string> approved = new(StringComparer.Ordinal)
        {
            "id", "state", "role", "userName", "createdAt", "updatedAt",
        };

        foreach (JsonProperty property in element.EnumerateObject())
        {
            Assert.Contains(property.Name, approved);
        }

        Assert.True(element.TryGetProperty("id", out _));
        Assert.True(element.TryGetProperty("state", out _));
        Assert.True(element.TryGetProperty("role", out _));
    }

    /// <summary>
    /// Positive control: seeded sentinel values for password hash / security stamp / normalized
    /// user name / authorization-state version are proven present in the database row so their
    /// absence from the response body is a meaningful assertion rather than a vacuous one.
    /// </summary>
    private static void AssertNonDisclosurePositiveControl(string responseBody, Operator seeded)
    {
        Assert.DoesNotContain(seeded.PasswordHash, responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain(seeded.SecurityStamp, responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorizationStateVersion", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("jwt", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("signingKey", responseBody, StringComparison.OrdinalIgnoreCase);

        Assert.False(string.IsNullOrWhiteSpace(seeded.PasswordHash));
        Assert.False(string.IsNullOrWhiteSpace(seeded.SecurityStamp));
    }

    private async Task MigrateAsync()
    {
        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, TimeSpan.FromSeconds(120));
        Assert.True(
            run.ExitCode == MigratorExitCode.Success,
            $"Expected OPR-QRY-01 test migration success. Output:{Environment.NewLine}{run.Output}");
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

    private async Task<long> CountAuditsAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"SELECT count(*) FROM {AuditPersistence.TableName};",
            connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private async Task<List<PersistedAudit>> ReadAuditsAsync(string correlationId)
    {
        List<PersistedAudit> records = [];
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
                 {AuditPersistence.FailureBusinessErrorCodeColumn}
             FROM {AuditPersistence.TableName}
             WHERE {AuditPersistence.CorrelationIdColumn} = @correlation_id;
             """,
            connection);
        command.Parameters.AddWithValue("correlation_id", correlationId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            records.Add(new PersistedAudit(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return records;
    }

    private static string CreateToken(Operator operatorEntity)
    {
        DateTime now = DateTime.UtcNow;
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, operatorEntity.Id.ToString("D")),
            new(
                AuthnClaimTypes.AuthorizationStateVersion,
                operatorEntity.AuthorizationStateVersion.ToString(CultureInfo.InvariantCulture)),
        ];

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
        HttpMethod method,
        string path,
        string token,
        string? correlationId = null)
    {
        using HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (correlationId is not null)
        {
            request.Headers.Add("X-Correlation-ID", correlationId);
        }

        return await client.SendAsync(request);
    }

    private static async Task AssertAuthenticationRequiredAsync(HttpResponseMessage response) =>
        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "authentication_required");

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        string body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != expectedStatus)
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected {(int)expectedStatus} '{expectedCode}', got {(int)response.StatusCode}. Body: {body}");
        }

        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    private static void AssertNotInternalError(HttpResponseMessage response, string body)
    {
        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            throw new Xunit.Sdk.XunitException(
                $"OPR-QRY-01 endpoint returned HTTP 500 unexpectedly. Body: {body}");
        }
    }

    private sealed record PersistedAudit(
        Guid ActorIdentifier,
        string ActorRole,
        string OperationIdentifier,
        string TargetIdentifier,
        string Result,
        string? FailureBusinessErrorCode);
}

internal sealed class AuditFailureProbe
{
    private int invocationCount;

    public int InvocationCount => Volatile.Read(ref invocationCount);

    public void RecordInvocation() => Interlocked.Increment(ref invocationCount);
}

internal sealed class FailingAuditWriter(AuditFailureProbe failureProbe) : IAuditWriter
{
    public Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        throw new NotSupportedException("OPR-QRY-01 tests only exercise the separate transaction primitive.");
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
        throw new OperatorQueryAuditFailureInjectionException();
    }
}

internal sealed class OperatorQueryAuditFailureInjectionException()
    : InvalidOperationException("Deterministic test-only OPR-QRY-01 Product Audit failure.");

internal sealed class OperatorQueryApiFactory(
    string connectionString,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<api::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(JwtAuthnOptions.SigningKeyConfigurationKey, TestJwtConfiguration.SigningKey);
        builder.UseSetting(
            $"ConnectionStrings:{BankPersistence.ConnectionStringName}",
            connectionString);

        builder.ConfigureServices(services => configureServices?.Invoke(services));
    }
}
