using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Api.OperatorCreate;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Auditing;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.IntegrationTests.PostgreSql;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.OperatorCreate;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class OperatorCreateTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset FrozenUtcNow =
        new(2031, 8, 9, 10, 11, 12, TimeSpan.Zero);

    private const string AdministratorPassword = "opr-create-admin-seed-password-not-for-production";

    [Theory]
    [InlineData("administrator")]
    [InlineData("teller")]
    [InlineData("viewer")]
    public async Task AdministratorCreateReturns201LocationAndExactProjection(string role)
    {
        await MigrateAsync();
        Operator administrator = await SeedAdministratorAsync($"opr.create.success.{role}.admin");
        string loginIdentifier = $"opr.create.success.{role}.target";
        const string correlationIdPrefix = "opr-create-success";
        string correlationId = $"{correlationIdPrefix}-{role}";

        await using OperatorCreateApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorCreateTestAuthentication.SendCreateAsync(
            client,
            new
            {
                loginIdentifier,
                password = OperatorCreateDisclosureOracle.PasswordSentinel,
                role,
            },
            OperatorCreateTestAuthentication.CreateToken(administrator),
            correlationId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement projection = document.RootElement;
        Guid createdId = projection.GetProperty("operatorIdentifier").GetGuid();

        AssertExactProjection(projection, createdId, "active", role);
        AssertLocation(response, createdId);

        PersistedOperator persisted = await ReadOperatorAsync(createdId);
        Assert.Equal(loginIdentifier, persisted.UserName);
        Assert.Equal(loginIdentifier.ToUpperInvariant(), persisted.NormalizedUserName);
        Assert.Equal("active", persisted.State);
        Assert.Equal(role, persisted.Role);
        Assert.NotEqual(OperatorCreateDisclosureOracle.PasswordSentinel, persisted.PasswordHash);
        Assert.DoesNotContain(
            OperatorCreateDisclosureOracle.PasswordSentinel,
            persisted.PasswordHash,
            StringComparison.Ordinal);
        Assert.Equal(
            PasswordVerificationResult.Success,
            IdentityPassword.Verify(
                await LoadOperatorAsync(createdId),
                OperatorCreateDisclosureOracle.PasswordSentinel));

        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(administrator.Id, audit.ActorIdentifier);
        Assert.Equal(OperatorCreateAudit.OperationIdentifier, audit.OperationIdentifier);
        Assert.Equal(createdId.ToString("D"), audit.TargetIdentifier);
        Assert.NotEqual(loginIdentifier, audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.SuccessResultToken, audit.Result);
        Assert.Null(audit.FailureBusinessErrorCode);
    }

    [Fact]
    public async Task CreatedOperatorCanAuthenticateWithTheSubmittedPassword()
    {
        await MigrateAsync();
        Operator administrator = await SeedAdministratorAsync("opr.create.login.admin");
        const string loginIdentifier = "opr.create.login.target";
        const string password = "opr-create-follow-on-login-password";

        await using OperatorCreateApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage created = await OperatorCreateTestAuthentication.SendCreateAsync(
            client,
            new { loginIdentifier, password, role = "viewer" },
            OperatorCreateTestAuthentication.CreateToken(administrator),
            "opr-create-follow-on-login");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using HttpResponseMessage login = await client.PostAsJsonAsync(
            "/auth/login",
            new { userName = loginIdentifier, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("ADMINISTRATOR")]
    [InlineData("admin")]
    [InlineData("teller ")]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData(" ")]
    public async Task InvalidRoleReturns400AndExactlyOneHandlerRejectionAudit(string role)
    {
        await MigrateAsync();
        Operator administrator = await SeedAdministratorAsync("opr.create.invalid-role.admin");
        OperatorCreateExecutionSignals signals = new();
        const string correlationId = "opr-create-invalid-role";
        const string loginIdentifier = "opr.create.invalid-role.target";

        await using OperatorCreateApiFactory factory = CreateFactory(services =>
            OperatorCreateTestAuthentication.AddExecutionSignal(services, signals));
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorCreateTestAuthentication.SendCreateAsync(
            client,
            new
            {
                loginIdentifier,
                password = "valid-password",
                role,
            },
            OperatorCreateTestAuthentication.CreateToken(administrator),
            correlationId);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(1, signals.ActionReachedCount);
        Assert.Equal(0L, await CountOperatorsByLoginAsync(loginIdentifier));

        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(administrator.Id, audit.ActorIdentifier);
        Assert.Equal(OperatorCreateAudit.OperationIdentifier, audit.OperationIdentifier);
        Assert.Equal(OperatorCreateAudit.CollectionTargetIdentifier, audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.FailureResultToken, audit.Result);
        Assert.Equal("validation_failed", audit.FailureBusinessErrorCode);
        Assert.NotEqual(loginIdentifier, audit.TargetIdentifier);
    }

    [Theory]
    [InlineData("missing-login", null, "valid-password", "teller")]
    [InlineData("missing-password", "opr.create.invalid-cred.target", null, "teller")]
    [InlineData("empty-login", "", "valid-password", "teller")]
    [InlineData("empty-password", "opr.create.invalid-cred.target", "", "teller")]
    [InlineData("whitespace-login", "   ", "valid-password", "teller")]
    [InlineData("whitespace-password", "opr.create.invalid-cred.target", " \t ", "teller")]
    public async Task InvalidCredentialReturns400AndExactlyOneHandlerRejectionAudit(
        string scenario,
        string? loginIdentifier,
        string? password,
        string role)
    {
        await MigrateAsync();
        Operator administrator = await SeedAdministratorAsync($"opr.create.invalid-cred.{scenario}.admin");
        OperatorCreateExecutionSignals signals = new();
        string correlationId = $"opr-create-invalid-cred-{scenario}";

        Dictionary<string, string?> payload = new()
        {
            ["role"] = role,
        };
        if (scenario is not "missing-login")
        {
            payload["loginIdentifier"] = loginIdentifier;
        }

        if (scenario is not "missing-password")
        {
            payload["password"] = password;
        }

        await using OperatorCreateApiFactory factory = CreateFactory(services =>
            OperatorCreateTestAuthentication.AddExecutionSignal(services, signals));
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorCreateTestAuthentication.SendCreateAsync(
            client,
            payload,
            OperatorCreateTestAuthentication.CreateToken(administrator),
            correlationId);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(1, signals.ActionReachedCount);
        Assert.Equal(0L, await CountOperatorsByLoginAsync(loginIdentifier ?? "missing"));

        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(OperatorCreateAudit.OperationIdentifier, audit.OperationIdentifier);
        Assert.Equal(OperatorCreateAudit.CollectionTargetIdentifier, audit.TargetIdentifier);
        Assert.Equal("validation_failed", audit.FailureBusinessErrorCode);
        Assert.False(
            OperatorCreateDisclosureOracle.Detects(
                JsonSerializer.Serialize(audit),
                loginIdentifier ?? string.Empty,
                password ?? string.Empty));
    }

    [Fact]
    public async Task DuplicateNormalizedLoginIdentifierReturns409WithoutPartialRow()
    {
        await MigrateAsync();
        Operator administrator = await SeedAdministratorAsync("opr.create.duplicate.admin");
        const string originalLogin = "Opr.Create.Duplicate.Target";
        await SeedOperatorAsync(originalLogin, OperatorRole.Viewer, "already-registered-password");
        OperatorCreateExecutionSignals signals = new();
        const string correlationId = "opr-create-duplicate";

        await using OperatorCreateApiFactory factory = CreateFactory(services =>
            OperatorCreateTestAuthentication.AddExecutionSignal(services, signals));
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorCreateTestAuthentication.SendCreateAsync(
            client,
            new
            {
                loginIdentifier = "opr.create.duplicate.target",
                password = "another-password",
                role = "teller",
            },
            OperatorCreateTestAuthentication.CreateToken(administrator),
            correlationId);

        await AssertErrorAsync(
            response,
            HttpStatusCode.Conflict,
            "operator_login_identifier_already_registered");
        Assert.Equal(1, signals.ActionReachedCount);
        Assert.Equal(1L, await CountOperatorsByLoginAsync(originalLogin));

        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(OperatorCreateAudit.OperationIdentifier, audit.OperationIdentifier);
        Assert.Equal(OperatorCreateAudit.CollectionTargetIdentifier, audit.TargetIdentifier);
        Assert.Equal("operator_login_identifier_already_registered", audit.FailureBusinessErrorCode);
        Assert.NotEqual("opr.create.duplicate.target", audit.TargetIdentifier);
    }

    [Fact]
    public async Task UnauthenticatedRequestReturns401WithoutReachingHandlerOrWritingProductAudit()
    {
        await MigrateAsync();
        _ = await SeedAdministratorAsync("opr.create.unauthenticated.admin");
        OperatorCreateExecutionSignals signals = new();

        await using OperatorCreateApiFactory factory = CreateFactory(services =>
            OperatorCreateTestAuthentication.AddExecutionSignal(services, signals));
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorCreateTestAuthentication.SendCreateAsync(
            client,
            new
            {
                loginIdentifier = "opr.create.unauthenticated.target",
                password = "ignored-password",
                role = "viewer",
            },
            token: null,
            correlationId: "opr-create-unauthenticated");

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "authentication_required");
        Assert.Equal(0, signals.ActionReachedCount);
        Assert.Equal(0L, await CountAuditsAsync());
        Assert.Equal(0L, await CountOperatorsByLoginAsync("opr.create.unauthenticated.target"));
    }

    [Theory]
    [InlineData(OperatorRole.Teller)]
    [InlineData(OperatorRole.Viewer)]
    public async Task NonAdministratorReturns403WithExactlyOneAuthzAuditAndNoFeatureHandler(OperatorRole role)
    {
        await MigrateAsync();
        Operator actor = await SeedOperatorAsync($"opr.create.forbidden.{role}", role, AdministratorPassword);
        OperatorCreateExecutionSignals signals = new();
        string correlationId = $"opr-create-forbidden-{role}";

        await using OperatorCreateApiFactory factory = CreateFactory(services =>
            OperatorCreateTestAuthentication.AddExecutionSignal(services, signals));
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorCreateTestAuthentication.SendCreateAsync(
            client,
            new
            {
                loginIdentifier = $"opr.create.forbidden.{role}.target",
                password = "ignored-password",
                role = "viewer",
            },
            OperatorCreateTestAuthentication.CreateToken(actor),
            correlationId);

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "operation_not_permitted");
        Assert.Equal(0, signals.ActionReachedCount);
        Assert.Equal(0L, await CountOperatorsByLoginAsync($"opr.create.forbidden.{role}.target"));

        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(actor.Id, audit.ActorIdentifier);
        Assert.Equal(OperatorCreateAudit.OperationIdentifier, audit.OperationIdentifier);
        Assert.Equal(OperatorCreateAudit.CollectionTargetIdentifier, audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.FailureResultToken, audit.Result);
        Assert.Equal("operation_not_permitted", audit.FailureBusinessErrorCode);
    }

    [Fact]
    public async Task SuccessAuditFailureRollsBackOperatorAndDoesNotReturn201()
    {
        await MigrateAsync();
        Operator administrator = await SeedAdministratorAsync("opr.create.audit-fail.admin");
        OperatorCreateAuditFailureProbe probe = new();
        const string loginIdentifier = "opr.create.audit-fail.target";
        const string correlationId = "opr-create-audit-fail";

        await using OperatorCreateApiFactory factory = CreateFactory(services =>
        {
            services.AddSingleton(probe);
            OperatorCreateTestAuthentication.ReplaceAuditWriter<FailingOperatorCreateAuditWriter>(services);
        });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorCreateTestAuthentication.SendCreateAsync(
            client,
            new
            {
                loginIdentifier,
                password = "will-not-persist",
                role = "teller",
            },
            OperatorCreateTestAuthentication.CreateToken(administrator),
            correlationId);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("internal_error", body, StringComparison.Ordinal);
        Assert.DoesNotContain("operatorIdentifier", body, StringComparison.Ordinal);
        Assert.Equal(1, probe.InvocationCount);
        Assert.Equal(0L, await CountOperatorsByLoginAsync(loginIdentifier));
        Assert.Empty(await ReadAuditsAsync(correlationId));
    }

    [Fact]
    public async Task ProductionAuditWriterFailureLeavesNoOperatorRow()
    {
        await MigrateAsync();
        Operator administrator = await SeedAdministratorAsync("opr.create.audit-interceptor.admin");
        const string loginIdentifier = "opr.create.audit-interceptor.target";
        const string correlationId = "opr-create-audit-interceptor";

        await using OperatorCreateApiFactory factory = CreateFactory(services =>
            OperatorCreateTestAuthentication.AddSaveChangesInterceptor(
                services,
                Database.ConnectionString,
                new ThrowOnAuditSaveChangesInterceptor()));
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorCreateTestAuthentication.SendCreateAsync(
            client,
            new
            {
                loginIdentifier,
                password = "will-not-persist",
                role = "viewer",
            },
            OperatorCreateTestAuthentication.CreateToken(administrator),
            correlationId);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(0L, await CountOperatorsByLoginAsync(loginIdentifier));
        Assert.Empty(await ReadAuditsAsync(correlationId));
    }

    [Fact]
    public async Task OperatorPersistenceFailureDoesNotLeaveSuccessAudit()
    {
        await MigrateAsync();
        Operator administrator = await SeedAdministratorAsync("opr.create.operator-fail.admin");
        const string loginIdentifier = "opr.create.operator-fail.target";
        const string correlationId = "opr-create-operator-fail";

        await using OperatorCreateApiFactory factory = CreateFactory(services =>
            OperatorCreateTestAuthentication.AddSaveChangesInterceptor(
                services,
                Database.ConnectionString,
                new ThrowOnOperatorSaveChangesInterceptor()));
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorCreateTestAuthentication.SendCreateAsync(
            client,
            new
            {
                loginIdentifier,
                password = "will-not-persist",
                role = "viewer",
            },
            OperatorCreateTestAuthentication.CreateToken(administrator),
            correlationId);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("operatorIdentifier", body, StringComparison.Ordinal);
        Assert.Equal(0L, await CountOperatorsByLoginAsync(loginIdentifier));
        Assert.Empty(await ReadAuditsAsync(correlationId));
    }

    [Fact]
    public async Task HandlerRejectionAuditFailureFailsClosedBeforeErrorResponse()
    {
        await MigrateAsync();
        Operator administrator = await SeedAdministratorAsync("opr.create.reject-audit-fail.admin");
        OperatorCreateAuditFailureProbe probe = new();
        const string loginIdentifier = "opr.create.reject-audit-fail.target";
        const string correlationId = "opr-create-reject-audit-fail";

        await using OperatorCreateApiFactory factory = CreateFactory(services =>
        {
            services.AddSingleton(probe);
            OperatorCreateTestAuthentication.ReplaceAuditWriter<FailingOperatorCreateAuditWriter>(services);
        });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorCreateTestAuthentication.SendCreateAsync(
            client,
            new
            {
                loginIdentifier,
                password = "valid-password",
                role = "Administrator",
            },
            OperatorCreateTestAuthentication.CreateToken(administrator),
            correlationId);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("validation_failed", body, StringComparison.Ordinal);
        Assert.Equal(1, probe.InvocationCount);
        Assert.Equal(0L, await CountOperatorsByLoginAsync(loginIdentifier));
        Assert.Empty(await ReadAuditsAsync(correlationId));
    }

    [Fact]
    public async Task ResponseAuditAndLogsOmitCredentialMaterialWithPositiveControl()
    {
        await MigrateAsync();
        Operator administrator = await SeedAdministratorAsync("opr.create.disclosure.admin");
        OperatorCreateLogCapture capture = new();
        capture.EmitPositiveControl(OperatorCreateDisclosureOracle.PasswordSentinel);
        Assert.True(
            OperatorCreateDisclosureOracle.Detects(
                string.Join('\n', capture.Messages),
                OperatorCreateDisclosureOracle.PasswordSentinel),
            "The log oracle must detect an intentionally emitted credential sentinel.");
        capture.Clear();

        string leaking = OperatorCreateDisclosureOracle.LeakingProjection(
            OperatorCreateDisclosureOracle.PasswordSentinel,
            OperatorCreateDisclosureOracle.LoginSentinel,
            OperatorCreateDisclosureOracle.HashSentinel);
        Assert.True(
            OperatorCreateDisclosureOracle.Detects(
                leaking,
                OperatorCreateDisclosureOracle.PasswordSentinel,
                OperatorCreateDisclosureOracle.LoginSentinel,
                OperatorCreateDisclosureOracle.HashSentinel));

        await using OperatorCreateApiFactory factory = CreateFactory(logCapture: capture);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorCreateTestAuthentication.SendCreateAsync(
            client,
            new
            {
                loginIdentifier = OperatorCreateDisclosureOracle.LoginSentinel,
                password = OperatorCreateDisclosureOracle.PasswordSentinel,
                role = "teller",
            },
            OperatorCreateTestAuthentication.CreateToken(administrator),
            "opr-create-disclosure");
        string body = await response.Content.ReadAsStringAsync();
        string logs = string.Join('\n', factory.LogCapture.Messages);
        Guid createdId = JsonDocument.Parse(body).RootElement.GetProperty("operatorIdentifier").GetGuid();
        PersistedOperator persisted = await ReadOperatorAsync(createdId);
        string auditJson = JsonSerializer.Serialize(await ReadAuditsAsync("opr-create-disclosure"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AssertExactProjection(JsonDocument.Parse(body).RootElement, createdId, "active", "teller");
        Assert.False(
            OperatorCreateDisclosureOracle.Detects(
                body,
                OperatorCreateDisclosureOracle.PasswordSentinel,
                OperatorCreateDisclosureOracle.HashSentinel,
                persisted.PasswordHash,
                persisted.SecurityStamp,
                "passwordHash",
                "securityStamp",
                "authorizationStateVersion"));
        Assert.False(
            OperatorCreateDisclosureOracle.Detects(
                auditJson,
                OperatorCreateDisclosureOracle.PasswordSentinel,
                OperatorCreateDisclosureOracle.LoginSentinel,
                persisted.PasswordHash));
        Assert.False(
            OperatorCreateDisclosureOracle.Detects(
                logs,
                OperatorCreateDisclosureOracle.PasswordSentinel,
                OperatorCreateDisclosureOracle.LoginSentinel,
                persisted.PasswordHash));
    }

    private OperatorCreateApiFactory CreateFactory(
        Action<IServiceCollection>? configureServices = null,
        OperatorCreateLogCapture? logCapture = null) =>
        new(Database.ConnectionString, configureServices, logCapture);

    private async Task MigrateAsync()
    {
        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, TimeSpan.FromSeconds(120));
        Assert.True(run.ExitCode == MigratorExitCode.Success, $"Migration failed. Output:{Environment.NewLine}{run.Output}");
    }

    private Task<Operator> SeedAdministratorAsync(string userName) =>
        SeedOperatorAsync(userName, OperatorRole.Administrator, AdministratorPassword);

    private async Task<Operator> SeedOperatorAsync(string userName, OperatorRole role, string password)
    {
        Operator created = OperatorFactory.Create(
            userName,
            password,
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

    private async Task<Operator> LoadOperatorAsync(Guid operatorId)
    {
        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        await using BankDbContext context = new(options.Options);
        return await context.Operators.AsNoTracking().SingleAsync(operatorEntity => operatorEntity.Id == operatorId);
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

    private async Task<long> CountOperatorsByLoginAsync(string loginIdentifier)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"""
             SELECT count(*) FROM {OperatorPersistence.TableName}
             WHERE {OperatorPersistence.NormalizedUserNameColumn} = @normalized;
             """,
            connection);
        command.Parameters.AddWithValue("normalized", loginIdentifier.Trim().ToUpperInvariant());
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private async Task<PersistedOperator> ReadOperatorAsync(Guid operatorId)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"""
             SELECT
                 {OperatorPersistence.IdColumn},
                 {OperatorPersistence.UserNameColumn},
                 {OperatorPersistence.NormalizedUserNameColumn},
                 {OperatorPersistence.PasswordHashColumn},
                 {OperatorPersistence.SecurityStampColumn},
                 {OperatorPersistence.StateColumn},
                 {OperatorPersistence.FixedRoleColumn}
             FROM {OperatorPersistence.TableName}
             WHERE {OperatorPersistence.IdColumn} = @id;
             """,
            connection);
        command.Parameters.AddWithValue("id", operatorId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new PersistedOperator(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6));
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

    private static void AssertExactProjection(
        JsonElement projection,
        Guid expectedIdentifier,
        string expectedState,
        string expectedRole)
    {
        Assert.Equal(expectedIdentifier, projection.GetProperty("operatorIdentifier").GetGuid());
        Assert.Equal(expectedState, projection.GetProperty("state").GetString());
        Assert.Equal(expectedRole, projection.GetProperty("role").GetString());

        string[] approvedFields = ["operatorIdentifier", "state", "role"];
        Assert.Equal(approvedFields.Length, projection.EnumerateObject().Count());
        foreach (JsonProperty property in projection.EnumerateObject())
        {
            Assert.Contains(property.Name, approvedFields);
        }
    }

    private static void AssertLocation(HttpResponseMessage response, Guid createdId)
    {
        Assert.NotNull(response.Headers.Location);
        string path = response.Headers.Location.IsAbsoluteUri
            ? response.Headers.Location.AbsolutePath
            : response.Headers.Location.OriginalString;
        Assert.Equal($"/operators/{createdId:D}", path);
    }

    private sealed record PersistedAudit(
        Guid ActorIdentifier,
        string ActorRole,
        string OperationIdentifier,
        string TargetIdentifier,
        string Result,
        string? FailureBusinessErrorCode,
        string CorrelationId);

    private sealed record PersistedOperator(
        Guid Id,
        string UserName,
        string NormalizedUserName,
        string PasswordHash,
        string SecurityStamp,
        string State,
        string Role);
}
