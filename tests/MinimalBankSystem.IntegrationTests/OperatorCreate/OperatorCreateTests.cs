extern alias api;

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.IntegrationTests.PostgreSql;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.OperatorCreate;

[Trait("Category", "PostgreSqlIntegration")]
[Collection(TestExecutionCollections.ConsoleSensitive)]
public sealed class OperatorCreateTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string AdministratorPassword = "OPR-CREATE-admin-password";
    private const string CreatedPassword = "OPR-CREATE-password-sentinel";

    private static readonly DateTimeOffset FrozenUtcNow =
        new(2033, 6, 7, 8, 9, 10, TimeSpan.Zero);

    [Fact]
    public async Task AdministratorCreateReturns201LocationAndExactProjectionWithHashedPassword()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr.create.admin", OperatorRole.Administrator);
        const string correlationId = "opr-create-success";

        await using OperatorCreateApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendCreateAsync(
            client,
            new
            {
                loginIdentifier = "  Opr.Create.Created  ",
                password = CreatedPassword,
                role = "viewer",
            },
            CreateToken(administrator),
            correlationId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement projection = document.RootElement;
        Assert.Equal(
            ["operatorIdentifier", "role", "state"],
            projection.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
        Guid createdIdentifier = projection.GetProperty("operatorIdentifier").GetGuid();
        Assert.Equal("active", projection.GetProperty("state").GetString());
        Assert.Equal("viewer", projection.GetProperty("role").GetString());

        Assert.Equal(
            $"/operators/{createdIdentifier:D}",
            response.Headers.Location!.OriginalString);

        Operator created = await ReadOperatorAsync(createdIdentifier);
        Assert.Equal("Opr.Create.Created", created.UserName);
        Assert.Equal("OPR.CREATE.CREATED", created.NormalizedUserName);
        Assert.Equal(OperatorRole.Viewer, created.Role);
        Assert.Equal(OperatorState.Active, created.State);
        Assert.NotEqual(CreatedPassword, created.PasswordHash);
        Assert.DoesNotContain(CreatedPassword, created.PasswordHash, StringComparison.Ordinal);
        Assert.Equal(
            Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success,
            IdentityPassword.Verify(created, CreatedPassword));

        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(administrator.Id, audit.ActorIdentifier);
        Assert.Equal("administrator", audit.ActorRole);
        Assert.Equal("operator.command.create", audit.OperationIdentifier);
        Assert.Equal(createdIdentifier.ToString("D"), audit.TargetIdentifier);
        Assert.Equal("success", audit.Result);
        Assert.Null(audit.FailureBusinessErrorCode);
    }

    [Theory]
    [InlineData("missing-login", null, "valid-password", "viewer")]
    [InlineData("empty-login", "", "valid-password", "viewer")]
    [InlineData("blank-login", "   ", "valid-password", "viewer")]
    [InlineData("missing-password", "valid-login", null, "viewer")]
    [InlineData("empty-password", "valid-login", "", "viewer")]
    [InlineData("blank-password", "valid-login", "   ", "viewer")]
    public async Task InvalidCredentialReturns400AndExactlyOneHandlerAudit(
        string caseName,
        string? loginIdentifier,
        string? password,
        string role)
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync($"opr.create.invalid.{caseName}", OperatorRole.Administrator);
        string correlationId = $"opr-create-invalid-{caseName}";

        await using OperatorCreateApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendCreateAsync(
            client,
            new { loginIdentifier, password, role },
            CreateToken(administrator),
            correlationId);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(1L, await CountOperatorsAsync());
        AssertCreateRejectionAudit(
            Assert.Single(await ReadAuditsAsync(correlationId)),
            administrator,
            "validation_failed");
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("TELLER")]
    [InlineData(" viewer")]
    [InlineData("viewer ")]
    [InlineData("supervisor")]
    public async Task InvalidRoleReturns400AndExactlyOneHandlerAudit(string invalidRole)
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync(
            $"opr.create.invalid-role.{Guid.NewGuid():N}",
            OperatorRole.Administrator);
        string correlationId = $"opr-create-role-{Guid.NewGuid():N}"[..32];

        await using OperatorCreateApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendCreateAsync(
            client,
            new
            {
                loginIdentifier = "opr.create.invalid.role.target",
                password = CreatedPassword,
                role = invalidRole,
            },
            CreateToken(administrator),
            correlationId);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(1L, await CountOperatorsAsync());
        AssertCreateRejectionAudit(
            Assert.Single(await ReadAuditsAsync(correlationId)),
            administrator,
            "validation_failed");
    }

    [Fact]
    public async Task NormalizedDuplicateReturns409WithoutPartialRowAndExactlyOneHandlerAudit()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr.create.duplicate.admin", OperatorRole.Administrator);
        Operator existing = await SeedOperatorAsync("Existing.Login", OperatorRole.Viewer);
        const string correlationId = "opr-create-duplicate";

        await using OperatorCreateApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendCreateAsync(
            client,
            new
            {
                loginIdentifier = " existing.login ",
                password = CreatedPassword,
                role = "administrator",
            },
            CreateToken(administrator),
            correlationId);

        await AssertErrorAsync(
            response,
            HttpStatusCode.Conflict,
            "operator_login_identifier_already_registered");
        Assert.Single(await ReadOperatorsAsync(existing.NormalizedUserName));
        AssertCreateRejectionAudit(
            Assert.Single(await ReadAuditsAsync(correlationId)),
            administrator,
            "operator_login_identifier_already_registered");
    }

    [Fact]
    public async Task UnauthenticatedCreateReturns401WithoutHandlerReachOrProductAudit()
    {
        await MigrateAsync();
        CreateExecutionSignals signals = new();
        const string correlationId = "opr-create-unauthenticated";

        await using OperatorCreateApiFactory factory = CreateFactory(services => AddExecutionSignal(services, signals));
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendCreateAsync(
            client,
            new { loginIdentifier = "opr.create.unauthenticated", password = CreatedPassword, role = "viewer" },
            token: null,
            correlationId);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "authentication_required");
        Assert.Equal(0, signals.ActionReachedCount);
        Assert.Empty(await ReadAuditsAsync(correlationId));
    }

    [Fact]
    public async Task NonAdministratorCreateReturns403WithoutHandlerReachAndOnlyAuthzAudit()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("opr.create.viewer", OperatorRole.Viewer);
        CreateExecutionSignals signals = new();
        const string correlationId = "opr-create-forbidden";

        await using OperatorCreateApiFactory factory = CreateFactory(services => AddExecutionSignal(services, signals));
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendCreateAsync(
            client,
            new { loginIdentifier = "opr.create.forbidden.target", password = CreatedPassword, role = "viewer" },
            CreateToken(viewer),
            correlationId);

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "operation_not_permitted");
        Assert.Equal(0, signals.ActionReachedCount);

        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(viewer.Id, audit.ActorIdentifier);
        Assert.Equal("operator.command.create", audit.OperationIdentifier);
        Assert.Equal("operators", audit.TargetIdentifier);
        Assert.Equal("failure", audit.Result);
        Assert.Equal("operation_not_permitted", audit.FailureBusinessErrorCode);
        Assert.Empty(await ReadOperatorsAsync("OPR.CREATE.FORBIDDEN.TARGET"));
    }

    [Fact]
    public async Task InjectedAuditFailureRollsBackOperatorAndDoesNotExpose201()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr.create.audit-failure.admin", OperatorRole.Administrator);
        AuditFailureProbe probe = new();
        const string loginIdentifier = "opr.create.audit.failure.target";
        const string correlationId = "opr-create-audit-failure";

        await using OperatorCreateApiFactory factory = CreateFactory(services =>
        {
            services.RemoveAll<IAuditWriter>();
            services.AddSingleton(probe);
            services.AddScoped<IAuditWriter, FailingOperatorCreateAuditWriter>();
        });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendCreateAsync(
            client,
            new { loginIdentifier, password = CreatedPassword, role = "viewer" },
            CreateToken(administrator),
            correlationId);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("internal_error", body, StringComparison.Ordinal);
        Assert.DoesNotContain(loginIdentifier, body, StringComparison.Ordinal);
        Assert.Equal(1, probe.CurrentTransactionInvocationCount);
        Assert.Empty(await ReadOperatorsAsync("OPR.CREATE.AUDIT.FAILURE.TARGET"));
        Assert.Empty(await ReadAuditsAsync(correlationId));
    }

    [Fact]
    public async Task InjectedRejectionAuditFailureFailsClosedBefore400()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr.create.rejection-audit-failure.admin", OperatorRole.Administrator);
        AuditFailureProbe probe = new();
        const string correlationId = "opr-create-rejection-audit-failure";

        await using OperatorCreateApiFactory factory = CreateFactory(services =>
        {
            services.RemoveAll<IAuditWriter>();
            services.AddSingleton(probe);
            services.AddScoped<IAuditWriter, FailingOperatorCreateAuditWriter>();
        });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendCreateAsync(
            client,
            new
            {
                loginIdentifier = "opr.create.rejection.audit.failure.target",
                password = CreatedPassword,
                role = "VIEWER",
            },
            CreateToken(administrator),
            correlationId);

        await AssertErrorAsync(response, HttpStatusCode.InternalServerError, "internal_error");
        Assert.Equal(1, probe.SeparateTransactionInvocationCount);
        Assert.Equal(1L, await CountOperatorsAsync());
        Assert.Empty(await ReadAuditsAsync(correlationId));
    }

    [Fact]
    public async Task InjectedOperatorPersistenceFailureLeavesNoMisleadingSuccessAudit()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr.create.persistence-failure.admin", OperatorRole.Administrator);
        const string loginIdentifier = "opr.create.persistence.failure.target";
        const string correlationId = "opr-create-persistence-failure";

        await InstallOperatorInsertFailureTriggerAsync();
        await using OperatorCreateApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendCreateAsync(
            client,
            new { loginIdentifier, password = CreatedPassword, role = "viewer" },
            CreateToken(administrator),
            correlationId);

        await AssertErrorAsync(response, HttpStatusCode.InternalServerError, "internal_error");
        Assert.Empty(await ReadOperatorsAsync("OPR.CREATE.PERSISTENCE.FAILURE.TARGET"));
        Assert.Empty(await ReadAuditsAsync(correlationId));
    }

    [Fact]
    public async Task CredentialNonDisclosureOracleCoversResponseLogsAuditAndPersistence()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr.create.disclosure.admin", OperatorRole.Administrator);
        const string sentinel = "OPR_CREATE_CREDENTIAL_NON_DISCLOSURE_SENTINEL";
        const string correlationId = "opr-create-disclosure";

        using ConsoleCapture capture = new();
        await using OperatorCreateApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendCreateAsync(
            client,
            new
            {
                loginIdentifier = "opr.create.disclosure.target",
                password = sentinel,
                role = "viewer",
            },
            CreateToken(administrator),
            correlationId);
        string responseBody = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(responseBody);
        Guid createdIdentifier = document.RootElement.GetProperty("operatorIdentifier").GetGuid();
        Operator created = await ReadOperatorAsync(createdIdentifier);
        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));

        AssertCredentialNotDisclosed(
            sentinel,
            responseBody,
            capture.Content,
            audit.OperationIdentifier,
            audit.TargetIdentifier,
            audit.Result,
            created.PasswordHash,
            created.UserName,
            created.NormalizedUserName);

        // Positive control: the same oracle fails if any inspected surface contains the sentinel.
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
            AssertCredentialNotDisclosed(sentinel, $"intentional leak: {sentinel}"));
    }

    private OperatorCreateApiFactory CreateFactory(Action<IServiceCollection>? configureServices = null) =>
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
            AdministratorPassword,
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

    private async Task<Operator> ReadOperatorAsync(Guid identifier)
    {
        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        await using BankDbContext context = new(options.Options);
        Operator? loaded = await context.Operators.SingleOrDefaultAsync(candidate => candidate.Id == identifier);
        Assert.NotNull(loaded);
        return loaded!;
    }

    private async Task<IReadOnlyList<Operator>> ReadOperatorsAsync(string normalizedUserName)
    {
        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        await using BankDbContext context = new(options.Options);
        return await context.Operators
            .Where(candidate => candidate.NormalizedUserName == normalizedUserName)
            .ToListAsync();
    }

    private async Task<long> CountOperatorsAsync()
    {
        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        await using BankDbContext context = new(options.Options);
        return await context.Operators.LongCountAsync();
    }

    private async Task InstallOperatorInsertFailureTriggerAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            """
            CREATE OR REPLACE FUNCTION opr_create_test_operator_insert_failure()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                RAISE EXCEPTION 'deterministic test-only Operator persistence failure';
            END;
            $$;
            DROP TRIGGER IF EXISTS opr_create_test_operator_insert_failure ON operators;
            CREATE TRIGGER opr_create_test_operator_insert_failure
            BEFORE INSERT ON operators
            FOR EACH ROW
            EXECUTE FUNCTION opr_create_test_operator_insert_failure();
            """,
            connection);
        await command.ExecuteNonQueryAsync();
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

    private static async Task<HttpResponseMessage> SendCreateAsync(
        HttpClient client,
        object payload,
        string? token,
        string correlationId)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/operators")
        {
            Content = JsonContent.Create(payload),
        };

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

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

    private static void AssertCreateRejectionAudit(
        PersistedAudit audit,
        Operator actor,
        string expectedCode)
    {
        Assert.Equal(actor.Id, audit.ActorIdentifier);
        Assert.Equal("operator.command.create", audit.OperationIdentifier);
        Assert.Equal("operators", audit.TargetIdentifier);
        Assert.Equal("failure", audit.Result);
        Assert.Equal(expectedCode, audit.FailureBusinessErrorCode);
    }

    private static void AssertCredentialNotDisclosed(string credential, params string[] surfaces)
    {
        foreach (string surface in surfaces)
        {
            Assert.DoesNotContain(credential, surface, StringComparison.Ordinal);
        }
    }

    private static void AddExecutionSignal(IServiceCollection services, CreateExecutionSignals signals)
    {
        services.AddSingleton(signals);
        services.AddSingleton<CreateExecutionFilter>();
        services.Configure<MvcOptions>(options => options.Filters.AddService<CreateExecutionFilter>());
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

    private sealed record PersistedAudit(
        Guid ActorIdentifier,
        string ActorRole,
        string OperationIdentifier,
        string TargetIdentifier,
        string Result,
        string? FailureBusinessErrorCode,
        string CorrelationId);
}

internal sealed class OperatorCreateApiFactory(
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
        builder.ConfigureServices(services =>
        {
            configureServices?.Invoke(services);
        });
    }
}

internal sealed class CreateExecutionSignals
{
    private int actionReachedCount;

    public int ActionReachedCount => Volatile.Read(ref actionReachedCount);

    public void RecordActionReached() => Interlocked.Increment(ref actionReachedCount);
}

internal sealed class CreateExecutionFilter(CreateExecutionSignals signals) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (context.HttpContext.Request.Method == HttpMethods.Post &&
            context.HttpContext.Request.Path == "/operators")
        {
            signals.RecordActionReached();
        }

        await next().ConfigureAwait(false);
    }
}

internal sealed class AuditFailureProbe
{
    private int currentTransactionInvocationCount;
    private int separateTransactionInvocationCount;

    public int CurrentTransactionInvocationCount => Volatile.Read(ref currentTransactionInvocationCount);

    public int SeparateTransactionInvocationCount => Volatile.Read(ref separateTransactionInvocationCount);

    public void RecordCurrentTransactionInvocation() => Interlocked.Increment(ref currentTransactionInvocationCount);

    public void RecordSeparateTransactionInvocation() => Interlocked.Increment(ref separateTransactionInvocationCount);
}

internal sealed class FailingOperatorCreateAuditWriter(AuditFailureProbe probe) : IAuditWriter
{
    public Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        probe.RecordCurrentTransactionInvocation();
        throw new OperatorCreateAuditFailureInjectionException();
    }

    public Task<TResult> AppendInSeparateTransactionBeforeResultAsync<TResult>(
        AuditWriteRequest request,
        Func<CancellationToken, Task<TResult>> successResultFactory,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = successResultFactory;
        _ = cancellationToken;
        probe.RecordSeparateTransactionInvocation();
        throw new OperatorCreateAuditFailureInjectionException();
    }
}

internal sealed class OperatorCreateAuditFailureInjectionException()
    : InvalidOperationException("Deterministic test-only OPR-CREATE Audit failure.");
