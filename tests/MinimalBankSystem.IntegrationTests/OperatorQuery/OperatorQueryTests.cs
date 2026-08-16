extern alias api;

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

namespace MinimalBankSystem.IntegrationTests.OperatorQuery;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class OperatorQueryTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset FrozenUtcNow =
        new(2030, 4, 5, 6, 7, 8, TimeSpan.Zero);

    private const string PlaintextPassword = "OperatorQuery-Only-Test-Password-123!";

    [Fact]
    public async Task AdministratorListReturnsOnlyApprovedProjection()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("operator.query.list.admin", OperatorRole.Administrator);
        Operator viewer = await SeedOperatorAsync("operator.query.list.viewer", OperatorRole.Viewer);

        await using OperatorQueryApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendGetAsync(
            client,
            "/operators",
            CreateToken(administrator),
            "opr-query-list-projection");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement[] entries = document.RootElement.EnumerateArray().ToArray();

        Assert.Equal(2, entries.Length);
        AssertProjection(
            entries.Single(entry => entry.GetProperty("operatorIdentifier").GetGuid() == administrator.Id),
            administrator.Id,
            expectedState: "active",
            expectedRole: "administrator");
        AssertProjection(
            entries.Single(entry => entry.GetProperty("operatorIdentifier").GetGuid() == viewer.Id),
            viewer.Id,
            expectedState: "active",
            expectedRole: "viewer");
    }

    [Fact]
    public async Task AdministratorDetailReturnsOnlyApprovedProjection()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("operator.query.detail.admin", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("operator.query.detail.target", OperatorRole.Viewer);

        await using OperatorQueryApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendGetAsync(
            client,
            $"/operators/{target.Id:D}",
            CreateToken(administrator),
            "opr-query-detail-projection");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        AssertProjection(
            document.RootElement,
            target.Id,
            expectedState: "active",
            expectedRole: "viewer");
    }

    [Fact]
    public async Task ListSuccessWritesExactlyOneProductAudit()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("operator.query.audit.list", OperatorRole.Administrator);
        const string correlationId = "opr-query-list-audit";

        await using OperatorQueryApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendGetAsync(
            client,
            "/operators",
            CreateToken(administrator),
            correlationId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(administrator.Id, audit.ActorIdentifier);
        Assert.Equal(OperatorPersistence.AdministratorRoleToken, audit.ActorRole);
        Assert.Equal("operator.query.list", audit.OperationIdentifier);
        Assert.Equal("operators", audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.SuccessResultToken, audit.Result);
        Assert.Null(audit.FailureBusinessErrorCode);
    }

    [Fact]
    public async Task DetailSuccessWritesExactlyOneProductAudit()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("operator.query.audit.detail", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("operator.query.audit.detail.target", OperatorRole.Viewer);
        const string correlationId = "opr-query-detail-audit";

        await using OperatorQueryApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendGetAsync(
            client,
            $"/operators/{target.Id:D}",
            CreateToken(administrator),
            correlationId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(administrator.Id, audit.ActorIdentifier);
        Assert.Equal("operator.query.detail", audit.OperationIdentifier);
        Assert.Equal(target.Id.ToString("D"), audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.SuccessResultToken, audit.Result);
        Assert.Null(audit.FailureBusinessErrorCode);
    }

    [Fact]
    public async Task AuditFailurePreventsSuccessDataFromBeingReturned()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("operator.query.audit.failure", OperatorRole.Administrator);
        AuditFailureProbe failureProbe = new();

        await using OperatorQueryApiFactory factory = CreateFactory(services =>
        {
            services.RemoveAll<IAuditWriter>();
            services.AddSingleton(failureProbe);
            services.AddScoped<IAuditWriter, FailingOperatorQueryAuditWriter>();
        });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendGetAsync(
            client,
            "/operators",
            CreateToken(administrator),
            "opr-query-audit-failure");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("internal_error", body, StringComparison.Ordinal);
        Assert.DoesNotContain(administrator.Id.ToString("D"), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("operatorIdentifier", body, StringComparison.Ordinal);
        Assert.Equal(1, failureProbe.InvocationCount);
        Assert.Empty(await ReadAuditsAsync("opr-query-audit-failure"));
    }

    [Fact]
    public async Task UnauthenticatedRequestReturns401WithoutReachingHandlerOrWritingProductAudit()
    {
        await MigrateAsync();
        _ = await SeedOperatorAsync("operator.query.unauthenticated", OperatorRole.Administrator);
        OperatorQueryExecutionSignals signals = new();

        await using OperatorQueryApiFactory factory = CreateFactory(services => AddExecutionSignal(services, signals));
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/operators");

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "authentication_required");
        Assert.Equal(0, signals.ActionReachedCount);
        Assert.Equal(0L, await CountAuditsAsync());
    }

    [Fact]
    public async Task NonAdministratorReturns403WithoutHandlerReachAndWritesOneAuthzAudit()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("operator.query.non-admin", OperatorRole.Viewer);
        OperatorQueryExecutionSignals signals = new();
        const string correlationId = "opr-query-non-admin";

        await using OperatorQueryApiFactory factory = CreateFactory(services => AddExecutionSignal(services, signals));
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendGetAsync(
            client,
            "/operators",
            CreateToken(viewer),
            correlationId);

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "operation_not_permitted");
        Assert.Equal(0, signals.ActionReachedCount);

        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(viewer.Id, audit.ActorIdentifier);
        Assert.Equal("operator.query.list", audit.OperationIdentifier);
        Assert.Equal("operators", audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.FailureResultToken, audit.Result);
        Assert.Equal("operation_not_permitted", audit.FailureBusinessErrorCode);
    }

    [Fact]
    public async Task MissingDetailReturnsOperatorNotFoundAndAuditsRequestedCanonicalIdentifierExactlyOnce()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("operator.query.missing", OperatorRole.Administrator);
        Guid requestedIdentifier = Guid.Parse("7f5f4d2a-82dd-4bd4-ae05-7f5fd08c3c5f");
        OperatorQueryExecutionSignals signals = new();
        const string correlationId = "opr-query-missing";

        await using OperatorQueryApiFactory factory = CreateFactory(services => AddExecutionSignal(services, signals));
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendGetAsync(
            client,
            $"/operators/{requestedIdentifier:D}",
            CreateToken(administrator),
            correlationId);

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "operator_not_found");
        Assert.Equal(1, signals.ActionReachedCount);

        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(administrator.Id, audit.ActorIdentifier);
        Assert.Equal("operator.query.detail", audit.OperationIdentifier);
        Assert.Equal(requestedIdentifier.ToString("D"), audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.FailureResultToken, audit.Result);
        Assert.Equal("operator_not_found", audit.FailureBusinessErrorCode);
    }

    [Fact]
    public async Task ProjectionHasPositiveControlFieldsAndOmitsProhibitedFields()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("operator.query.disclosure", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("operator.query.disclosure.target", OperatorRole.Viewer);

        await using OperatorQueryApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendGetAsync(
            client,
            $"/operators/{target.Id:D}",
            CreateToken(administrator),
            "opr-query-disclosure");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement projection = document.RootElement;

        AssertProjection(
            projection,
            target.Id,
            expectedState: "active",
            expectedRole: "viewer");
        foreach (string prohibitedField in new[]
                 {
                     "password",
                     "passwordHash",
                     "securityStamp",
                     "authorizationStateVersion",
                     "jwt",
                     "signingKey",
                     "credential",
                 })
        {
            Assert.False(
                projection.TryGetProperty(prohibitedField, out _),
                $"The prohibited field '{prohibitedField}' was disclosed.");
        }
    }

    [Fact]
    public async Task PolicyAndHandlerRejectionsDoNotDoubleAudit()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("operator.query.no-double.viewer", OperatorRole.Viewer);
        Operator administrator = await SeedOperatorAsync("operator.query.no-double.admin", OperatorRole.Administrator);
        Guid missingIdentifier = Guid.Parse("b5d2a1f6-2c3b-4d61-8ef5-079858fb7d33");

        await using OperatorQueryApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage forbidden = await SendGetAsync(
            client,
            "/operators",
            CreateToken(viewer),
            "opr-query-no-double-policy");
        using HttpResponseMessage missing = await SendGetAsync(
            client,
            $"/operators/{missingIdentifier:D}",
            CreateToken(administrator),
            "opr-query-no-double-handler");

        await AssertErrorAsync(forbidden, HttpStatusCode.Forbidden, "operation_not_permitted");
        await AssertErrorAsync(missing, HttpStatusCode.NotFound, "operator_not_found");
        Assert.Single(await ReadAuditsAsync("opr-query-no-double-policy"));
        Assert.Single(await ReadAuditsAsync("opr-query-no-double-handler"));
    }

    private OperatorQueryApiFactory CreateFactory(Action<IServiceCollection>? configureServices = null) =>
        new(Database.ConnectionString, configureServices);

    private async Task MigrateAsync()
    {
        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, TimeSpan.FromSeconds(120));
        Assert.True(run.ExitCode == MigratorExitCode.Success, $"Migration failed. Output:{Environment.NewLine}{run.Output}");
    }

    private async Task<Operator> SeedOperatorAsync(string userName, OperatorRole role)
    {
        Operator created = OperatorFactory.Create(
            userName,
            PlaintextPassword,
            role,
            FrozenUtcNow,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        await using BankDbContext context = new(options.Options);
        context.Operators.Add(created);
        await context.SaveChangesAsync();
        return created;
    }

    private async Task<IReadOnlyList<PersistedAudit>> ReadAuditsAsync(string correlationId)
    {
        List<PersistedAudit> audits = [];
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
            audits.Add(new PersistedAudit(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetString(6)));
        }

        return audits;
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

    private static async Task<HttpResponseMessage> SendGetAsync(
        HttpClient client,
        string path,
        string token,
        string correlationId)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        return await client.SendAsync(request);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(expectedStatus, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    private static void AssertProjection(
        JsonElement projection,
        Guid expectedIdentifier,
        string expectedState,
        string expectedRole)
    {
        Assert.Equal(expectedIdentifier, projection.GetProperty("operatorIdentifier").GetGuid());

        JsonElement state = projection.GetProperty("state");
        Assert.Equal(JsonValueKind.String, state.ValueKind);
        Assert.Equal(expectedState, state.GetString());

        JsonElement role = projection.GetProperty("role");
        Assert.Equal(JsonValueKind.String, role.ValueKind);
        Assert.Equal(expectedRole, role.GetString());

        string[] approvedFields = ["operatorIdentifier", "state", "role"];
        Assert.Equal(approvedFields.Length, projection.EnumerateObject().Count());
        foreach (JsonProperty property in projection.EnumerateObject())
        {
            Assert.Contains(property.Name, approvedFields);
        }
    }

    private static string CreateToken(Operator operatorEntity)
    {
        DateTime now = DateTime.UtcNow;
        JwtSecurityToken token = new(
            issuer: "minimal-bank-system",
            audience: "minimal-bank-system-api",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, operatorEntity.Id.ToString("D")),
                new Claim(
                    AuthnClaimTypes.AuthorizationStateVersion,
                    operatorEntity.AuthorizationStateVersion.ToString(CultureInfo.InvariantCulture)),
            ],
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(TestJwtConfiguration.SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static void AddExecutionSignal(IServiceCollection services, OperatorQueryExecutionSignals signals)
    {
        services.AddSingleton(signals);
        services.AddSingleton<OperatorQueryExecutionFilter>();
        services.Configure<MvcOptions>(options => options.Filters.AddService<OperatorQueryExecutionFilter>());
    }

    private sealed record PersistedAudit(
        Guid ActorIdentifier,
        string ActorRole,
        string OperationIdentifier,
        string TargetIdentifier,
        string Result,
        string? FailureBusinessErrorCode,
        string CorrelationId);
}

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

internal sealed class OperatorQueryExecutionSignals
{
    private int actionReachedCount;

    public int ActionReachedCount => Volatile.Read(ref actionReachedCount);

    public void RecordActionReached() => Interlocked.Increment(ref actionReachedCount);
}

internal sealed class OperatorQueryExecutionFilter(OperatorQueryExecutionSignals signals) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (context.HttpContext.Request.Path.StartsWithSegments("/operators"))
        {
            signals.RecordActionReached();
        }

        await next().ConfigureAwait(false);
    }
}

internal sealed class AuditFailureProbe
{
    private int invocationCount;

    public int InvocationCount => Volatile.Read(ref invocationCount);

    public void RecordInvocation() => Interlocked.Increment(ref invocationCount);
}

internal sealed class FailingOperatorQueryAuditWriter(AuditFailureProbe failureProbe) : IAuditWriter
{
    public Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        throw new NotSupportedException("Operator query tests use the separate Audit primitive.");
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
    : InvalidOperationException("Deterministic test-only Operator query Audit persistence failure.");
