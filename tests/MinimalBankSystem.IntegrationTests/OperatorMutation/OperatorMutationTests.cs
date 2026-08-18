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

namespace MinimalBankSystem.IntegrationTests.OperatorMutation;

[Collection(TestExecutionCollections.ConsoleSensitive)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class OperatorMutationTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset FrozenUtc =
        new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private const string SeedPlaintextPassword = "operator-mutation-seed-password-not-for-production";

    [Fact]
    public async Task ActualMutationsBumpVersionStampAndUpdatedAtAndReturnClosedProjection()
    {
        await MigrateAsync();
        Operator actor = await SeedOperatorAsync("opr-mut-success.actor", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("opr-mut-success.target", OperatorRole.Viewer, OperatorState.Disabled);

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        OperatorSnapshot beforeEnable = await ReadOperatorAsync(target.Id);
        using (HttpResponseMessage response = await SendMutationAsync(
                   client,
                   HttpMethod.Post,
                   $"/operators/{target.Id:D}/enable",
                   CreateToken(actor),
                   "opr-mut-success-enable"))
        {
            await AssertProjectionAsync(response, target.Id, "active", "viewer");
        }

        OperatorSnapshot afterEnable = await ReadOperatorAsync(target.Id);
        Assert.Equal(beforeEnable.AuthorizationStateVersion + 1, afterEnable.AuthorizationStateVersion);
        Assert.NotEqual(beforeEnable.SecurityStamp, afterEnable.SecurityStamp);
        Assert.True(afterEnable.UpdatedAt > beforeEnable.UpdatedAt);
        AssertAudit(
            Assert.Single(await ReadAuditsAsync("opr-mut-success-enable")),
            actor,
            "operator.command.enable",
            target.Id,
            AuditPersistence.SuccessResultToken,
            null);

        OperatorSnapshot beforeRole = afterEnable;
        using (HttpResponseMessage response = await SendMutationAsync(
                   client,
                   HttpMethod.Post,
                   $"/operators/{target.Id:D}/role",
                   CreateToken(actor),
                   "opr-mut-success-role",
                   new { role = "teller" }))
        {
            await AssertProjectionAsync(response, target.Id, "active", "teller");
        }

        OperatorSnapshot afterRole = await ReadOperatorAsync(target.Id);
        Assert.Equal(beforeRole.AuthorizationStateVersion + 1, afterRole.AuthorizationStateVersion);
        Assert.NotEqual(beforeRole.SecurityStamp, afterRole.SecurityStamp);
        Assert.True(afterRole.UpdatedAt > beforeRole.UpdatedAt);
        AssertAudit(
            Assert.Single(await ReadAuditsAsync("opr-mut-success-role")),
            actor,
            "operator.command.change-role",
            target.Id,
            AuditPersistence.SuccessResultToken,
            null);

        OperatorSnapshot beforeDisable = afterRole;
        using (HttpResponseMessage response = await SendMutationAsync(
                   client,
                   HttpMethod.Post,
                   $"/operators/{target.Id:D}/disable",
                   CreateToken(actor),
                   "opr-mut-success-disable"))
        {
            await AssertProjectionAsync(response, target.Id, "disabled", "teller");
        }

        OperatorSnapshot afterDisable = await ReadOperatorAsync(target.Id);
        Assert.Equal(beforeDisable.AuthorizationStateVersion + 1, afterDisable.AuthorizationStateVersion);
        Assert.NotEqual(beforeDisable.SecurityStamp, afterDisable.SecurityStamp);
        Assert.True(afterDisable.UpdatedAt > beforeDisable.UpdatedAt);
        AssertAudit(
            Assert.Single(await ReadAuditsAsync("opr-mut-success-disable")),
            actor,
            "operator.command.disable",
            target.Id,
            AuditPersistence.SuccessResultToken,
            null);
    }

    [Fact]
    public async Task NoOpsSelfDisableAndLastAdministratorRejectionsPreserveAuthorizationState()
    {
        await MigrateAsync();
        Operator actor = await SeedOperatorAsync("opr-mut-reject.actor", OperatorRole.Administrator);
        Operator disabled = await SeedOperatorAsync("opr-mut-reject.disabled", OperatorRole.Viewer, OperatorState.Disabled);

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        OperatorSnapshot actorBefore = await ReadOperatorAsync(actor.Id);
        OperatorSnapshot disabledBefore = await ReadOperatorAsync(disabled.Id);

        await AssertRejectedMutationAsync(
            client,
            $"/operators/{actor.Id:D}/enable",
            CreateToken(actor),
            "opr-mut-reject-enable-noop",
            expectedCode: "state_transition_not_allowed");
        await AssertRejectedMutationAsync(
            client,
            $"/operators/{disabled.Id:D}/disable",
            CreateToken(actor),
            "opr-mut-reject-disable-noop",
            expectedCode: "state_transition_not_allowed");
        await AssertRejectedMutationAsync(
            client,
            $"/operators/{actor.Id:D}/role",
            CreateToken(actor),
            "opr-mut-reject-role-noop",
            expectedCode: "state_transition_not_allowed",
            body: new { role = "administrator" });
        await AssertRejectedMutationAsync(
            client,
            $"/operators/{actor.Id:D}/disable",
            CreateToken(actor),
            "opr-mut-reject-self-disable",
            expectedCode: "state_transition_not_allowed");
        await AssertRejectedMutationAsync(
            client,
            $"/operators/{actor.Id:D}/role",
            CreateToken(actor),
            "opr-mut-reject-last-admin-demotion",
            expectedCode: "state_transition_not_allowed",
            body: new { role = "teller" });

        Assert.Equal(actorBefore, await ReadOperatorAsync(actor.Id));
        Assert.Equal(disabledBefore, await ReadOperatorAsync(disabled.Id));
        foreach (string correlationId in new[]
                 {
                     "opr-mut-reject-enable-noop",
                     "opr-mut-reject-disable-noop",
                     "opr-mut-reject-role-noop",
                     "opr-mut-reject-self-disable",
                     "opr-mut-reject-last-admin-demotion",
                 })
        {
            PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
            Assert.Equal(AuditPersistence.FailureResultToken, audit.Result);
            Assert.Equal("state_transition_not_allowed", audit.FailureBusinessErrorCode);
        }
    }

    [Fact]
    public async Task DisabledAdministratorRoleChangeAndSelfRoleChangeAreAllowedWhenInvariantRemainsSafe()
    {
        await MigrateAsync();
        Operator actor = await SeedOperatorAsync("opr-mut-role-boundary.actor", OperatorRole.Administrator);
        Operator secondAdministrator = await SeedOperatorAsync(
            "opr-mut-role-boundary.second-admin",
            OperatorRole.Administrator);
        Operator disabledAdministrator = await SeedOperatorAsync(
            "opr-mut-role-boundary.disabled-admin",
            OperatorRole.Administrator,
            OperatorState.Disabled);

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        using (HttpResponseMessage selfRole = await SendMutationAsync(
                   client,
                   HttpMethod.Post,
                   $"/operators/{actor.Id:D}/role",
                   CreateToken(actor),
                   "opr-mut-role-boundary-self",
                   new { role = "teller" }))
        {
            await AssertProjectionAsync(selfRole, actor.Id, "active", "teller");
        }

        using (HttpResponseMessage disabledRole = await SendMutationAsync(
                   client,
                   HttpMethod.Post,
                   $"/operators/{disabledAdministrator.Id:D}/role",
                   CreateToken(secondAdministrator),
                   "opr-mut-role-boundary-disabled",
                   new { role = "teller" }))
        {
            await AssertProjectionAsync(disabledRole, disabledAdministrator.Id, "disabled", "teller");
        }

        Assert.Equal(1L, await CountActiveAdministratorsAsync());
        Assert.Equal(OperatorRole.Teller, (await ReadOperatorAsync(actor.Id)).Role);
        Assert.Equal(OperatorRole.Teller, (await ReadOperatorAsync(disabledAdministrator.Id)).Role);
        Assert.Single(await ReadAuditsAsync("opr-mut-role-boundary-self"));
        Assert.Single(await ReadAuditsAsync("opr-mut-role-boundary-disabled"));
    }

    [Fact]
    public async Task ValidationMissingTargetAndAuthorizationOwnershipAreAuditedExactlyOnce()
    {
        await MigrateAsync();
        Operator actor = await SeedOperatorAsync("opr-mut-errors.actor", OperatorRole.Administrator);
        Operator viewer = await SeedOperatorAsync("opr-mut-errors.viewer", OperatorRole.Viewer);
        Guid missingIdentifier = Guid.Parse("7f5f4d2a-82dd-4bd4-ae05-7f5fd08c3c5f");
        MutationExecutionSignals signals = new();

        await using OperatorMutationApiFactory factory = CreateFactory(services =>
        {
            services.AddSingleton(signals);
            services.AddSingleton<MutationExecutionFilter>();
            services.Configure<MvcOptions>(options => options.Filters.AddService<MutationExecutionFilter>());
        });
        using HttpClient client = factory.CreateClient();

        await AssertRejectedMutationAsync(
            client,
            $"/operators/{actor.Id:D}/role",
            CreateToken(actor),
            "opr-mut-errors-invalid-role",
            expectedCode: "validation_failed",
            expectedStatus: HttpStatusCode.BadRequest,
            body: new { role = "owner" });
        Assert.Equal(1, signals.ActionReachedCount);

        await AssertRejectedMutationAsync(
            client,
            $"/operators/{missingIdentifier:D}/disable",
            CreateToken(actor),
            "opr-mut-errors-missing",
            expectedCode: "operator_not_found",
            expectedStatus: HttpStatusCode.NotFound);
        Assert.Equal(2, signals.ActionReachedCount);
        PersistedAudit missingAudit = Assert.Single(await ReadAuditsAsync("opr-mut-errors-missing"));
        Assert.Equal(missingIdentifier.ToString("D"), missingAudit.TargetIdentifier);
        Assert.Equal("operator.command.disable", missingAudit.OperationIdentifier);

        await AssertRejectedMutationAsync(
            client,
            $"/operators/{actor.Id:D}/disable",
            CreateToken(viewer),
            "opr-mut-errors-forbidden",
            expectedCode: "operation_not_permitted",
            expectedStatus: HttpStatusCode.Forbidden);
        Assert.Equal(2, signals.ActionReachedCount);
        PersistedAudit authorizationAudit = Assert.Single(await ReadAuditsAsync("opr-mut-errors-forbidden"));
        Assert.Equal(viewer.Id, authorizationAudit.ActorIdentifier);
        Assert.Equal("operator.command.disable", authorizationAudit.OperationIdentifier);
        Assert.Equal(actor.Id.ToString("D"), authorizationAudit.TargetIdentifier);

        using (HttpResponseMessage unauthenticated = await SendMutationAsync(
                   client,
                   HttpMethod.Post,
                   $"/operators/{actor.Id:D}/disable",
                   token: null,
                   "opr-mut-errors-unauthenticated"))
        {
            await AssertErrorAsync(unauthenticated, HttpStatusCode.Unauthorized, "authentication_required");
        }

        Assert.Equal(2, signals.ActionReachedCount);
        Assert.Empty(await ReadAuditsAsync("opr-mut-errors-unauthenticated"));
    }

    [Fact]
    public async Task MissingTargetUsesCanonicalGuidAndInvalidRoleUsesHandlerRejectionAudit()
    {
        await MigrateAsync();
        Operator actor = await SeedOperatorAsync("opr-mut-target.actor", OperatorRole.Administrator);
        Guid missingIdentifier = Guid.Parse("b5d2a1f6-2c3b-4d61-8ef5-079858fb7d33");

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        await AssertRejectedMutationAsync(
            client,
            $"/operators/{actor.Id:D}/role",
            CreateToken(actor),
            "opr-mut-target-invalid-role",
            expectedCode: "validation_failed",
            expectedStatus: HttpStatusCode.BadRequest,
            body: new { role = "Administrator" });
        PersistedAudit validationAudit = Assert.Single(await ReadAuditsAsync("opr-mut-target-invalid-role"));
        Assert.Equal(actor.Id.ToString("D"), validationAudit.TargetIdentifier);
        Assert.Equal("operator.command.change-role", validationAudit.OperationIdentifier);

        await AssertRejectedMutationAsync(
            client,
            $"/operators/{missingIdentifier:D}/enable",
            CreateToken(actor),
            "opr-mut-target-missing-enable",
            expectedCode: "operator_not_found",
            expectedStatus: HttpStatusCode.NotFound);
        PersistedAudit missingAudit = Assert.Single(await ReadAuditsAsync("opr-mut-target-missing-enable"));
        Assert.Equal(missingIdentifier.ToString("D"), missingAudit.TargetIdentifier);
        Assert.Equal("operator.command.enable", missingAudit.OperationIdentifier);
    }

    [Fact]
    public async Task SuccessfulMutationInvalidatesOldAuthenticatedStateOnTheNextRequest()
    {
        await MigrateAsync();
        Operator actor = await SeedOperatorAsync("opr-mut-stale.actor", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("opr-mut-stale.target", OperatorRole.Administrator);
        string staleToken = CreateToken(target);

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        using (HttpResponseMessage mutation = await SendMutationAsync(
                   client,
                   HttpMethod.Post,
                   $"/operators/{target.Id:D}/role",
                   CreateToken(actor),
                   "opr-mut-stale-change-role",
                   new { role = "teller" }))
        {
            await AssertProjectionAsync(mutation, target.Id, "active", "teller");
        }

        using HttpRequestMessage request = new(HttpMethod.Get, "/operators");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", staleToken);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "opr-mut-stale-next-request");
        using HttpResponseMessage staleResponse = await client.SendAsync(request);

        await AssertErrorAsync(staleResponse, HttpStatusCode.Unauthorized, "authentication_required");
        Assert.Empty(await ReadAuditsAsync("opr-mut-stale-next-request"));
    }

    [Fact]
    public async Task RequiredSuccessAuditFailureFailsClosedWithoutCommittingMutation()
    {
        await MigrateAsync();
        Operator actor = await SeedOperatorAsync("opr-mut-audit-failure.actor", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("opr-mut-audit-failure.target", OperatorRole.Viewer);
        OperatorSnapshot before = await ReadOperatorAsync(target.Id);
        AuditFailureProbe probe = new();

        await using OperatorMutationApiFactory factory = CreateFactory(services =>
        {
            services.RemoveAll<IAuditWriter>();
            services.AddSingleton(probe);
            services.AddScoped<IAuditWriter, FailingMutationAuditWriter>();
        });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendMutationAsync(
            client,
            HttpMethod.Post,
            $"/operators/{target.Id:D}/disable",
            CreateToken(actor),
            "opr-mut-audit-failure");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("internal_error", body, StringComparison.Ordinal);
        Assert.Equal(1, probe.CurrentTransactionInvocationCount);
        Assert.Equal(before, await ReadOperatorAsync(target.Id));
        Assert.Empty(await ReadAuditsAsync("opr-mut-audit-failure"));
    }

    [Fact]
    public async Task RejectionAuditFailureFailsClosedWithoutChangingOperator()
    {
        await MigrateAsync();
        Operator actor = await SeedOperatorAsync("opr-mut-rejection-audit-failure.actor", OperatorRole.Administrator);
        OperatorSnapshot before = await ReadOperatorAsync(actor.Id);
        AuditFailureProbe probe = new();

        await using OperatorMutationApiFactory factory = CreateFactory(services =>
        {
            services.RemoveAll<IAuditWriter>();
            services.AddSingleton(probe);
            services.AddScoped<IAuditWriter, FailingMutationAuditWriter>();
        });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendMutationAsync(
            client,
            HttpMethod.Post,
            $"/operators/{actor.Id:D}/role",
            CreateToken(actor),
            "opr-mut-rejection-audit-failure",
            new { role = "owner" });

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("internal_error", body, StringComparison.Ordinal);
        Assert.Equal(1, probe.SeparateTransactionInvocationCount);
        Assert.Equal(before, await ReadOperatorAsync(actor.Id));
        Assert.Empty(await ReadAuditsAsync("opr-mut-rejection-audit-failure"));
    }

    private OperatorMutationApiFactory CreateFactory(Action<IServiceCollection>? configureServices = null) =>
        new(Database.ConnectionString, configureServices);

    private async Task MigrateAsync()
    {
        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, TimeSpan.FromSeconds(120));
        Assert.True(run.ExitCode == MigratorExitCode.Success, $"Migration failed. Output:{Environment.NewLine}{run.Output}");
    }

    private async Task<Operator> SeedOperatorAsync(
        string userName,
        OperatorRole role,
        OperatorState state = OperatorState.Active)
    {
        Operator created = OperatorFactory.Create(
            userName,
            SeedPlaintextPassword,
            role,
            FrozenUtc,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        if (state != OperatorState.Active)
        {
            created.ApplyLifecycleMutation(
                state,
                role,
                FrozenUtc.AddSeconds(1),
                Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        }

        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        await using BankDbContext context = new(options.Options);
        context.Operators.Add(created);
        await context.SaveChangesAsync();
        return created;
    }

    private async Task<OperatorSnapshot> ReadOperatorAsync(Guid identifier)
    {
        await using BankDbContext context = CreateContext();
        Operator entity = await context.Operators.SingleAsync(candidate => candidate.Id == identifier);
        return new OperatorSnapshot(
            entity.State,
            entity.Role,
            entity.AuthorizationStateVersion,
            entity.SecurityStamp,
            entity.UpdatedAt);
    }

    private BankDbContext CreateContext()
    {
        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        return new BankDbContext(options.Options);
    }

    private async Task<long> CountActiveAdministratorsAsync()
    {
        await using BankDbContext context = CreateContext();
        return await context.Operators.LongCountAsync(candidate =>
            candidate.State == OperatorState.Active && candidate.Role == OperatorRole.Administrator);
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

    private static async Task<HttpResponseMessage> SendMutationAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string? token,
        string correlationId,
        object? body = null)
    {
        using HttpRequestMessage request = new(method, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    private async Task AssertRejectedMutationAsync(
        HttpClient client,
        string path,
        string token,
        string correlationId,
        string expectedCode,
        HttpStatusCode expectedStatus = HttpStatusCode.Conflict,
        object? body = null)
    {
        using HttpResponseMessage response = await SendMutationAsync(
            client,
            HttpMethod.Post,
            path,
            token,
            correlationId,
            body);
        await AssertErrorAsync(response, expectedStatus, expectedCode);
        _ = client;
        Assert.Single(await ReadAuditsAsync(correlationId));
    }

    private static async Task AssertProjectionAsync(
        HttpResponseMessage response,
        Guid identifier,
        string expectedState,
        string expectedRole)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement projection = document.RootElement;
        Assert.Equal(identifier, projection.GetProperty("operatorIdentifier").GetGuid());
        Assert.Equal(expectedState, projection.GetProperty("state").GetString());
        Assert.Equal(expectedRole, projection.GetProperty("role").GetString());
        Assert.Equal(3, projection.EnumerateObject().Count());
        Assert.DoesNotContain(projection.EnumerateObject(), property =>
            property.Name is "credential" or "securityStamp" or "authorizationStateVersion");
    }

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    private static void AssertAudit(
        PersistedAudit audit,
        Operator actor,
        string operationIdentifier,
        Guid targetIdentifier,
        string result,
        string? failureCode)
    {
        Assert.Equal(actor.Id, audit.ActorIdentifier);
        Assert.Equal(operationIdentifier, audit.OperationIdentifier);
        Assert.Equal(targetIdentifier.ToString("D"), audit.TargetIdentifier);
        Assert.Equal(result, audit.Result);
        Assert.Equal(failureCode, audit.FailureBusinessErrorCode);
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

    private sealed record OperatorSnapshot(
        OperatorState State,
        OperatorRole Role,
        int AuthorizationStateVersion,
        string SecurityStamp,
        DateTimeOffset UpdatedAt);

    private sealed record PersistedAudit(
        Guid ActorIdentifier,
        string ActorRole,
        string OperationIdentifier,
        string TargetIdentifier,
        string Result,
        string? FailureBusinessErrorCode,
        string CorrelationId);
}

internal sealed class OperatorMutationApiFactory(
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

internal sealed class MutationExecutionSignals
{
    private int actionReachedCount;

    public int ActionReachedCount => Volatile.Read(ref actionReachedCount);

    public void RecordActionReached() => Interlocked.Increment(ref actionReachedCount);
}

internal sealed class MutationExecutionFilter(MutationExecutionSignals signals) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.Request.Path.StartsWithSegments("/operators") &&
            context.HttpContext.Request.Method == HttpMethods.Post)
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

internal sealed class FailingMutationAuditWriter(AuditFailureProbe probe) : IAuditWriter
{
    public Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        probe.RecordCurrentTransactionInvocation();
        throw new OperatorMutationAuditFailureInjectionException();
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
        throw new OperatorMutationAuditFailureInjectionException();
    }
}

internal sealed class OperatorMutationAuditFailureInjectionException()
    : InvalidOperationException("Deterministic test-only Operator mutation Audit failure.");
