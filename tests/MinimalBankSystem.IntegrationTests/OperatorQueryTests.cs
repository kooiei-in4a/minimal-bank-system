extern alias api;

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Api.OperatorQuery;
using MinimalBankSystem.Api.Runtime;
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

namespace MinimalBankSystem.IntegrationTests;

[Collection(TestExecutionCollections.ConsoleSensitive)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class OperatorQueryTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string PlaintextPassword = "opr-qry-01-integration-password-not-for-production";
    private const string PositiveControlSecurityStamp = "opr-qry-01-security-stamp-positive-control";
    private static readonly DateTimeOffset FrozenUtc = new(2034, 7, 8, 9, 10, 11, TimeSpan.Zero);
    private static readonly string[] ApprovedProjectionFields =
    [
        "operatorIdentifier",
        "state",
        "role",
        "loginIdentifier",
        "createdAt",
        "updatedAt",
    ];
    private static readonly string[] ProhibitedProjectionFields =
    [
        "password",
        "passwordHash",
        "securityStamp",
        "authorizationStateVersion",
        "jwt",
        "signingKey",
        "credential",
    ];

    [Fact]
    public async Task AdministratorListReturnsApprovedProjection()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr-qry.list.admin", OperatorRole.Administrator);
        Operator viewer = await SeedOperatorAsync("opr-qry.list.viewer", OperatorRole.Viewer);

        await using OperatorQueryApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendWithTokenAsync(client, "/operators", CreateToken(administrator));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement[] operators = document.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, operators.Length);
        AssertProjection(Assert.Single(operators, item =>
            item.GetProperty("operatorIdentifier").GetGuid() == administrator.Id), administrator);
        AssertProjection(Assert.Single(operators, item =>
            item.GetProperty("operatorIdentifier").GetGuid() == viewer.Id), viewer);
    }

    [Fact]
    public async Task AdministratorDetailReturnsApprovedProjection()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr-qry.detail.admin", OperatorRole.Administrator);
        Operator viewer = await SeedOperatorAsync("opr-qry.detail.viewer", OperatorRole.Viewer);

        await using OperatorQueryApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendWithTokenAsync(
            client,
            $"/operators/{viewer.Id:D}",
            CreateToken(administrator));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        AssertProjection(document.RootElement, viewer);
    }

    [Fact]
    public async Task ListSuccessWritesExactlyOneProductAudit()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr-qry.audit-list.admin", OperatorRole.Administrator);
        const string correlationId = "opr-qry-list-success-audit";

        await using OperatorQueryApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendWithTokenAsync(
            client,
            "/operators",
            CreateToken(administrator),
            correlationId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(administrator.Id, audit.ActorIdentifier);
        Assert.Equal("administrator", audit.ActorRole);
        Assert.Equal(OperatorQueryOperations.List, audit.OperationIdentifier);
        Assert.Equal(OperatorQueryOperations.CollectionTarget, audit.TargetIdentifier);
        Assert.Equal("success", audit.Result);
        Assert.Null(audit.FailureBusinessErrorCode);
    }

    [Fact]
    public async Task DetailSuccessWritesExactlyOneProductAudit()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr-qry.audit-detail.admin", OperatorRole.Administrator);
        Operator viewer = await SeedOperatorAsync("opr-qry.audit-detail.viewer", OperatorRole.Viewer);
        const string correlationId = "opr-qry-detail-success-audit";

        await using OperatorQueryApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendWithTokenAsync(
            client,
            $"/operators/{viewer.Id:D}",
            CreateToken(administrator),
            correlationId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(administrator.Id, audit.ActorIdentifier);
        Assert.Equal(OperatorQueryOperations.Detail, audit.OperationIdentifier);
        Assert.Equal(viewer.Id.ToString("D"), audit.TargetIdentifier);
        Assert.Equal("success", audit.Result);
        Assert.Null(audit.FailureBusinessErrorCode);
    }

    [Fact]
    public async Task RequiredAuditFailureBlocksListSuccessData()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr-qry.audit-failure.admin", OperatorRole.Administrator);
        OperatorQueryAuditInvocationProbe probe = new();

        await using OperatorQueryApiFactory factory = new(
            Database.ConnectionString,
            services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton(probe);
                services.AddScoped<IAuditWriter, FailingOperatorQueryAuditWriter>();
            });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendWithTokenAsync(
            client,
            "/operators",
            CreateToken(administrator));

        await AssertErrorAsync(response, HttpStatusCode.InternalServerError, "internal_error");
        Assert.Equal(1, probe.InvocationCount);
        Assert.Equal(0L, await CountAuditsAsync());
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("operatorIdentifier", body, StringComparison.Ordinal);
        Assert.DoesNotContain("loginIdentifier", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnauthenticatedListReturns401WithoutHandlerAudit()
    {
        await MigrateAsync();

        await using OperatorQueryApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/operators");

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "authentication_required");
        Assert.Equal(0L, await CountAuditsAsync());
    }

    [Fact]
    public async Task NonAdministratorListReturns403AndOnlyAuthzWritesOneAudit()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("opr-qry.policy.viewer", OperatorRole.Viewer);
        OperatorQueryAuditInvocationProbe probe = new();
        const string correlationId = "opr-qry-policy-rejection";

        await using OperatorQueryApiFactory factory = new(
            Database.ConnectionString,
            services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddScoped<PostgreSqlAuditWriter>();
                services.AddSingleton(probe);
                services.AddScoped<IAuditWriter>(serviceProvider =>
                    new CountingOperatorQueryAuditWriter(
                        serviceProvider.GetRequiredService<PostgreSqlAuditWriter>(),
                        serviceProvider.GetRequiredService<OperatorQueryAuditInvocationProbe>()));
            });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendWithTokenAsync(
            client,
            "/operators",
            CreateToken(viewer),
            correlationId);

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "operation_not_permitted");
        Assert.Equal(1, probe.InvocationCount);
        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(viewer.Id, audit.ActorIdentifier);
        Assert.Equal(OperatorQueryOperations.List, audit.OperationIdentifier);
        Assert.Equal(OperatorQueryOperations.CollectionTarget, audit.TargetIdentifier);
        Assert.Equal("failure", audit.Result);
        Assert.Equal("operation_not_permitted", audit.FailureBusinessErrorCode);
    }

    [Fact]
    public async Task MissingDetailReturnsOperatorNotFoundAndWritesOneHandlerAudit()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr-qry.missing.admin", OperatorRole.Administrator);
        Guid missingIdentifier = Guid.CreateVersion7(FrozenUtc.AddMinutes(1));
        const string correlationId = "opr-qry-missing-detail";

        await using OperatorQueryApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendWithTokenAsync(
            client,
            $"/operators/{missingIdentifier:D}",
            CreateToken(administrator),
            correlationId);

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "operator_not_found");
        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(administrator.Id, audit.ActorIdentifier);
        Assert.Equal(OperatorQueryOperations.Detail, audit.OperationIdentifier);
        Assert.Equal(missingIdentifier.ToString("D"), audit.TargetIdentifier);
        Assert.Equal("failure", audit.Result);
        Assert.Equal("operator_not_found", audit.FailureBusinessErrorCode);
    }

    [Fact]
    public async Task ProjectionOmitsProhibitedSecurityMaterialWithPositiveControls()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync(
            "opr-qry.projection.admin",
            OperatorRole.Administrator,
            PositiveControlSecurityStamp);

        string storedSecurityStamp = Assert.IsType<string>(
            await ExecuteScalarAsync(
                $"SELECT {OperatorPersistence.SecurityStampColumn} FROM {OperatorPersistence.TableName};"));
        Assert.Equal(PositiveControlSecurityStamp, storedSecurityStamp);

        string storedPasswordHash = Assert.IsType<string>(
            await ExecuteScalarAsync(
                $"SELECT {OperatorPersistence.PasswordHashColumn} FROM {OperatorPersistence.TableName};"));
        Assert.NotEmpty(storedPasswordHash);

        await using OperatorQueryApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendWithTokenAsync(
            client,
            $"/operators/{administrator.Id:D}",
            CreateToken(administrator));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(body);
        AssertProjection(document.RootElement, administrator);

        foreach (string prohibitedField in ProhibitedProjectionFields)
        {
            Assert.DoesNotContain(prohibitedField, body, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(PositiveControlSecurityStamp, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuccessfulListIsNotDoubleAudited()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr-qry.no-double.admin", OperatorRole.Administrator);
        OperatorQueryAuditInvocationProbe probe = new();
        const string correlationId = "opr-qry-no-double-success";

        await using OperatorQueryApiFactory factory = new(
            Database.ConnectionString,
            services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddScoped<PostgreSqlAuditWriter>();
                services.AddSingleton(probe);
                services.AddScoped<IAuditWriter>(serviceProvider =>
                    new CountingOperatorQueryAuditWriter(
                        serviceProvider.GetRequiredService<PostgreSqlAuditWriter>(),
                        serviceProvider.GetRequiredService<OperatorQueryAuditInvocationProbe>()));
            });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendWithTokenAsync(
            client,
            "/operators",
            CreateToken(administrator),
            correlationId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, probe.InvocationCount);
        Assert.Single(await ReadAuditsAsync(correlationId));
    }

    [Fact]
    public void EachOperatorQueryEndpointHasExactlyOneFeatureAuditContext()
    {
        using OperatorQueryApiFactory factory = new(HealthConnectionStrings.Unreachable);
        EndpointDataSource endpointDataSource = factory.Services.GetRequiredService<EndpointDataSource>();
        RouteEndpoint[] endpoints = endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/operators", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Equal(2, endpoints.Length);
        Assert.All(
            endpoints,
            endpoint => Assert.Single(endpoint.Metadata.GetOrderedMetadata<IAuthorizationAuditContext>()));
    }

    private async Task MigrateAsync()
    {
        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, TimeSpan.FromSeconds(120));
        Assert.Equal(MigratorExitCode.Success, run.ExitCode);
    }

    private async Task<Operator> SeedOperatorAsync(
        string userName,
        OperatorRole role,
        string? securityStamp = null)
    {
        Operator created = OperatorFactory.Create(
            userName,
            PlaintextPassword,
            role,
            FrozenUtc,
            securityStamp ?? Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        await using BankDbContext context = CreateContext();
        context.Operators.Add(created);
        await context.SaveChangesAsync();
        return created;
    }

    private BankDbContext CreateContext()
    {
        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        return new BankDbContext(options.Options);
    }

    private async Task<long> CountAuditsAsync()
    {
        return Convert.ToInt64(
            await ExecuteScalarAsync($"SELECT count(*) FROM {AuditPersistence.TableName};"),
            CultureInfo.InvariantCulture);
    }

    private async Task<IReadOnlyList<PersistedAudit>> ReadAuditsAsync(string correlationId)
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

    private async Task<object?> ExecuteScalarAsync(string commandText)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        return await command.ExecuteScalarAsync();
    }

    private static void AssertProjection(JsonElement element, Operator expected)
    {
        string[] fields = element.EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(6, fields.Length);
        Assert.All(ApprovedProjectionFields, field => Assert.Contains(field, fields));

        Assert.Equal(expected.Id, element.GetProperty("operatorIdentifier").GetGuid());
        Assert.Equal("active", element.GetProperty("state").GetString());
        Assert.Equal(RoleToken(expected.Role), element.GetProperty("role").GetString());
        Assert.Equal(expected.UserName, element.GetProperty("loginIdentifier").GetString());
        Assert.Equal(expected.CreatedAt, element.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(expected.UpdatedAt, element.GetProperty("updatedAt").GetDateTimeOffset());
    }

    private static string RoleToken(OperatorRole role) => role switch
    {
        OperatorRole.Administrator => "administrator",
        OperatorRole.Teller => "teller",
        OperatorRole.Viewer => "viewer",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown test role."),
    };

    private static string CreateToken(Operator operatorEntity) =>
        CreateToken(operatorEntity.Id, operatorEntity.AuthorizationStateVersion);

    private static string CreateToken(Guid operatorId, int authorizationStateVersion)
    {
        DateTime now = DateTime.UtcNow;
        JwtSecurityToken token = new(
            issuer: "minimal-bank-system",
            audience: "minimal-bank-system-api",
            claims:
            [
                new(JwtRegisteredClaimNames.Sub, operatorId.ToString("D")),
                new(
                    AuthnClaimTypes.AuthorizationStateVersion,
                    authorizationStateVersion.ToString(CultureInfo.InvariantCulture)),
            ],
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

    private sealed record PersistedAudit(
        Guid ActorIdentifier,
        string ActorRole,
        string OperationIdentifier,
        string TargetIdentifier,
        string Result,
        string? FailureBusinessErrorCode);
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
        if (configureServices is not null)
        {
            builder.ConfigureServices(configureServices);
        }
    }
}

internal sealed class OperatorQueryAuditInvocationProbe
{
    private int invocationCount;

    public int InvocationCount => Volatile.Read(ref invocationCount);

    public void RecordInvocation() => Interlocked.Increment(ref invocationCount);
}

internal sealed class CountingOperatorQueryAuditWriter(
    PostgreSqlAuditWriter inner,
    OperatorQueryAuditInvocationProbe probe) : IAuditWriter
{
    public async Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        probe.RecordInvocation();
        await inner.AppendToCurrentTransactionAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TResult> AppendInSeparateTransactionBeforeResultAsync<TResult>(
        AuditWriteRequest request,
        Func<CancellationToken, Task<TResult>> successResultFactory,
        CancellationToken cancellationToken = default)
    {
        probe.RecordInvocation();
        return await inner
            .AppendInSeparateTransactionBeforeResultAsync(request, successResultFactory, cancellationToken)
            .ConfigureAwait(false);
    }
}

internal sealed class FailingOperatorQueryAuditWriter(OperatorQueryAuditInvocationProbe probe) : IAuditWriter
{
    public Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        throw new NotSupportedException("Operator query tests use the separate Audit transaction primitive.");
    }

    public Task<TResult> AppendInSeparateTransactionBeforeResultAsync<TResult>(
        AuditWriteRequest request,
        Func<CancellationToken, Task<TResult>> successResultFactory,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = successResultFactory;
        _ = cancellationToken;
        probe.RecordInvocation();
        throw new OperatorQueryAuditFailureInjectionException();
    }
}

internal sealed class OperatorQueryAuditFailureInjectionException()
    : InvalidOperationException("Deterministic test-only Operator query Product Audit failure.");
