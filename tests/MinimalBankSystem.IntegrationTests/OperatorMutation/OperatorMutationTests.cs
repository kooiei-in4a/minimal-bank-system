extern alias api;

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Api.OperatorMutation;
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

/// <summary>
/// Issue #171 (WP2-OPR-MUT-01) verification: the approved enable/disable/role-change contract,
/// no-op/self-disable/last-administrator rejection, authorization-state invalidation, Audit
/// ownership/atomicity, and real PostgreSQL concurrency, all through the real ASP.NET Core
/// pipeline and a real PostgreSQL database.
/// </summary>
[Collection(TestExecutionCollections.ConsoleSensitive)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class OperatorMutationTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    // Deliberately in the past (not the far-future convention used by sibling suites) so that a
    // mutation's real-clock UpdatedAt is always observably later than a seeded row's UpdatedAt.
    private static readonly DateTimeOffset FrozenUtcNow = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private const string SeedPlaintextPassword = "OperatorMutation-Seed-Password-Only-123!";

    // ---- Success contract -------------------------------------------------------------------

    [Fact]
    public async Task EnableSucceedsAndReturnsExactProjectionAndBumpsAuthorizationState()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprmut.enable.admin", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("oprmut.enable.target", OperatorRole.Teller, OperatorState.Disabled);
        int versionBefore = target.AuthorizationStateVersion;
        string stampBefore = target.SecurityStamp;

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendMutationAsync(
            client, CreateToken(administrator), "oprmut-enable-success", target.Id, "enable");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement projection = document.RootElement;
        Assert.Equal(target.Id, projection.GetProperty("operatorIdentifier").GetGuid());
        Assert.Equal("active", projection.GetProperty("state").GetString());
        Assert.Equal("teller", projection.GetProperty("role").GetString());

        string[] approvedFields = ["operatorIdentifier", "state", "role"];
        Assert.Equal(approvedFields.Length, projection.EnumerateObject().Count());

        Operator persisted = await ReadOperatorAsync(target.Id);
        Assert.Equal(OperatorState.Active, persisted.State);
        Assert.Equal(versionBefore + 1, persisted.AuthorizationStateVersion);
        Assert.NotEqual(stampBefore, persisted.SecurityStamp);
        Assert.True(persisted.UpdatedAt > target.UpdatedAt);
    }

    [Fact]
    public async Task DisableSucceedsAndReturnsExactProjectionAndBumpsAuthorizationState()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprmut.disable.admin", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("oprmut.disable.target", OperatorRole.Viewer);
        int versionBefore = target.AuthorizationStateVersion;
        string stampBefore = target.SecurityStamp;

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendMutationAsync(
            client, CreateToken(administrator), "oprmut-disable-success", target.Id, "disable");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("disabled", document.RootElement.GetProperty("state").GetString());
        Assert.Equal("viewer", document.RootElement.GetProperty("role").GetString());

        Operator persisted = await ReadOperatorAsync(target.Id);
        Assert.Equal(OperatorState.Disabled, persisted.State);
        Assert.Equal(versionBefore + 1, persisted.AuthorizationStateVersion);
        Assert.NotEqual(stampBefore, persisted.SecurityStamp);
    }

    [Fact]
    public async Task RoleChangeSucceedsAndReturnsExactProjectionAndBumpsAuthorizationState()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprmut.role.admin", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("oprmut.role.target", OperatorRole.Viewer);
        int versionBefore = target.AuthorizationStateVersion;
        string stampBefore = target.SecurityStamp;

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendRoleChangeAsync(
            client, CreateToken(administrator), "oprmut-role-success", target.Id, "teller");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("teller", document.RootElement.GetProperty("role").GetString());
        Assert.Equal("active", document.RootElement.GetProperty("state").GetString());

        Operator persisted = await ReadOperatorAsync(target.Id);
        Assert.Equal(OperatorRole.Teller, persisted.Role);
        Assert.Equal(versionBefore + 1, persisted.AuthorizationStateVersion);
        Assert.NotEqual(stampBefore, persisted.SecurityStamp);
    }

    [Fact]
    public async Task SuccessWritesExactlyOneProductAuditInSameTransactionAsOperatorRow()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprmut.audit.admin", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("oprmut.audit.target", OperatorRole.Teller);
        const string correlationId = "oprmut-success-audit";

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendMutationAsync(
            client, CreateToken(administrator), correlationId, target.Id, "disable");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(administrator.Id, audit.ActorIdentifier);
        Assert.Equal(OperatorPersistence.AdministratorRoleToken, audit.ActorRole);
        Assert.Equal("operator.command.disable", audit.OperationIdentifier);
        Assert.Equal(target.Id.ToString("D"), audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.SuccessResultToken, audit.Result);
        Assert.Null(audit.FailureBusinessErrorCode);
    }

    // ---- Stale authenticated state -----------------------------------------------------------

    [Fact]
    public async Task StaleAuthenticatedTokenIsRejectedOnNextRequestAfterSuccessfulMutation()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprmut.stale.admin", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("oprmut.stale.target", OperatorRole.Teller);
        string staleTokenForTarget = CreateToken(target);

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        // The target's own token is minted at its current authorization-state version. Promote it
        // to administrator so its role claim would matter if the JWT role claim were authoritative
        // (it is not, per ADR-0007), then immediately re-use the pre-promotion token.
        using HttpResponseMessage mutationResponse = await SendRoleChangeAsync(
            client, CreateToken(administrator), "oprmut-stale-mutation", target.Id, "administrator");
        Assert.Equal(HttpStatusCode.OK, mutationResponse.StatusCode);

        using HttpRequestMessage staleRequest = new(HttpMethod.Post, $"/operators/{administrator.Id:D}/enable");
        staleRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", staleTokenForTarget);
        staleRequest.Headers.Add(CorrelationIdMiddleware.HeaderName, "oprmut-stale-next-request");
        using HttpResponseMessage staleResponse = await client.SendAsync(staleRequest);

        await AssertErrorAsync(staleResponse, HttpStatusCode.Unauthorized, "authentication_required");
        Assert.Empty(await ReadAuditsAsync("oprmut-stale-next-request"));
    }

    // ---- 401 / 403 --------------------------------------------------------------------------

    [Theory]
    [InlineData("enable")]
    [InlineData("disable")]
    public async Task UnauthenticatedRequestReturns401WithoutReachingHandlerOrWritingProductAudit(string action)
    {
        await MigrateAsync();
        Operator target = await SeedOperatorAsync($"oprmut.unauth.{action}", OperatorRole.Teller);

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, $"/operators/{target.Id:D}/{action}");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, $"oprmut-unauth-{action}");

        using HttpResponseMessage response = await client.SendAsync(request);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "authentication_required");
        Assert.Equal(0L, await CountAuditsAsync());
        Operator persisted = await ReadOperatorAsync(target.Id);
        Assert.Equal(target.State, persisted.State);
        Assert.Equal(target.AuthorizationStateVersion, persisted.AuthorizationStateVersion);
    }

    [Theory]
    [InlineData(OperatorRole.Viewer)]
    [InlineData(OperatorRole.Teller)]
    public async Task NonAdministratorReturns403WithoutHandlerReachAndWritesOneAuthzAudit(OperatorRole nonAdminRole)
    {
        await MigrateAsync();
        Operator nonAdmin = await SeedOperatorAsync(
            $"oprmut.non-admin.{nonAdminRole}".ToLowerInvariant(), nonAdminRole);
        Operator target = await SeedOperatorAsync(
            $"oprmut.non-admin.target.{nonAdminRole}".ToLowerInvariant(), OperatorRole.Teller);
        string correlationId = $"oprmut-non-admin-{nonAdminRole}".ToLowerInvariant();

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendMutationAsync(
            client, CreateToken(nonAdmin), correlationId, target.Id, "disable");

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "operation_not_permitted");
        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(nonAdmin.Id, audit.ActorIdentifier);
        Assert.Equal("operator.command.disable", audit.OperationIdentifier);
        Assert.Equal(target.Id.ToString("D"), audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.FailureResultToken, audit.Result);
        Assert.Equal("operation_not_permitted", audit.FailureBusinessErrorCode);

        Operator persisted = await ReadOperatorAsync(target.Id);
        Assert.Equal(target.State, persisted.State);
        Assert.Equal(target.AuthorizationStateVersion, persisted.AuthorizationStateVersion);
    }

    // ---- 404 ----------------------------------------------------------------------------------

    [Fact]
    public async Task MissingOperatorReturns404WithHandlerRejectionAuditExactlyOnceUsingRequestedGuid()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprmut.missing.admin", OperatorRole.Administrator);
        Guid missingIdentifier = Guid.NewGuid();
        const string correlationId = "oprmut-missing-operator";

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendMutationAsync(
            client, CreateToken(administrator), correlationId, missingIdentifier, "disable");

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "operator_not_found");
        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(missingIdentifier.ToString("D"), audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.FailureResultToken, audit.Result);
        Assert.Equal("operator_not_found", audit.FailureBusinessErrorCode);
    }

    // ---- 400 invalid role -----------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("owner")]
    [InlineData("Administrator")]
    [InlineData("ADMINISTRATOR")]
    public async Task InvalidRoleReturns400WithHandlerRejectionAuditExactlyOnceAndUnchangedState(string? role)
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync(
            $"oprmut.invalid.role.admin.{Guid.NewGuid():N}", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync(
            $"oprmut.invalid.role.target.{Guid.NewGuid():N}", OperatorRole.Viewer);
        string correlationId = $"oprmut-invalid-role-{Guid.NewGuid():N}";
        int versionBefore = target.AuthorizationStateVersion;
        string stampBefore = target.SecurityStamp;

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendRoleChangeAsync(
            client, CreateToken(administrator), correlationId, target.Id, role);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(target.Id.ToString("D"), audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.FailureResultToken, audit.Result);
        Assert.Equal("validation_failed", audit.FailureBusinessErrorCode);

        Operator persisted = await ReadOperatorAsync(target.Id);
        Assert.Equal(OperatorRole.Viewer, persisted.Role);
        Assert.Equal(versionBefore, persisted.AuthorizationStateVersion);
        Assert.Equal(stampBefore, persisted.SecurityStamp);
    }

    // ---- No-op 409 ------------------------------------------------------------------------

    [Fact]
    public async Task EnableAlreadyActiveReturns409WithoutChangeAndHandlerAuditExactlyOnce()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprmut.noop.enable.admin", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("oprmut.noop.enable.target", OperatorRole.Teller);
        const string correlationId = "oprmut-noop-enable";

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendMutationAsync(
            client, CreateToken(administrator), correlationId, target.Id, "enable");

        await AssertNoOpRejectionAsync(response, correlationId, target, "operator.command.enable");
    }

    [Fact]
    public async Task DisableAlreadyDisabledReturns409WithoutChangeAndHandlerAuditExactlyOnce()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprmut.noop.disable.admin", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync(
            "oprmut.noop.disable.target", OperatorRole.Teller, OperatorState.Disabled);
        const string correlationId = "oprmut-noop-disable";

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendMutationAsync(
            client, CreateToken(administrator), correlationId, target.Id, "disable");

        await AssertNoOpRejectionAsync(response, correlationId, target, "operator.command.disable");
    }

    [Fact]
    public async Task SameRoleChangeReturns409WithoutChangeAndHandlerAuditExactlyOnce()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprmut.noop.role.admin", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("oprmut.noop.role.target", OperatorRole.Teller);
        const string correlationId = "oprmut-noop-role";

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendRoleChangeAsync(
            client, CreateToken(administrator), correlationId, target.Id, "teller");

        await AssertNoOpRejectionAsync(response, correlationId, target, "operator.command.change-role");
    }

    // ---- Self-disable ------------------------------------------------------------------------

    [Fact]
    public async Task SelfDisableReturns409WithoutChangeAndHandlerAuditExactlyOnce()
    {
        await MigrateAsync();
        Operator administrator1 = await SeedOperatorAsync("oprmut.self-disable.admin1", OperatorRole.Administrator);
        await SeedOperatorAsync("oprmut.self-disable.admin2", OperatorRole.Administrator);
        const string correlationId = "oprmut-self-disable";

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendMutationAsync(
            client, CreateToken(administrator1), correlationId, administrator1.Id, "disable");

        await AssertNoOpRejectionAsync(
            response, correlationId, administrator1, "operator.command.disable");
    }

    // ---- Last active administrator ------------------------------------------------------------

    /// <summary>
    /// A different, still-active administrator disabling one of two active administrators always
    /// leaves at least one behind (the caller), so the last-active-administrator invariant for
    /// disable can only bind when the caller targets themselves while they are the sole active
    /// administrator (the AUTHZ policy that gates this endpoint already requires the caller to be
    /// a currently active administrator, so a *different*, non-self "last admin" target can never
    /// legitimately be the sole one while a distinct administrator authenticates against it under
    /// normal sequential execution). This test seeds exactly one active administrator system-wide
    /// and asserts the invariant, not merely the independent self-disable rule, is what a
    /// different actor would also be blocked by if they could reach this state.
    /// </summary>
    [Fact]
    public async Task LastActiveAdministratorDisableReturns409WithoutChangeAndHandlerAuditExactlyOnce()
    {
        await MigrateAsync();
        Operator sole = await SeedOperatorAsync("oprmut.last-admin.disable.sole", OperatorRole.Administrator);
        Assert.Equal(1L, await CountActiveAdministratorsAsync());

        const string correlationId = "oprmut-last-admin-disable";
        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        Operator soleBefore = await ReadOperatorAsync(sole.Id);
        using HttpResponseMessage response = await SendMutationAsync(
            client, CreateToken(sole), correlationId, sole.Id, "disable");

        await AssertNoOpRejectionAsync(response, correlationId, soleBefore, "operator.command.disable");
        Assert.Equal(1L, await CountActiveAdministratorsAsync());
    }

    /// <summary>
    /// Self role-change is otherwise allowed (there is no self-specific rule for role changes), so
    /// this isolates the last-administrator invariant itself: the sole active administrator
    /// demotes themselves and must be rejected purely by the invariant, not by any self-targeting
    /// rule.
    /// </summary>
    [Fact]
    public async Task LastActiveAdministratorDemotionReturns409WithoutChangeAndHandlerAuditExactlyOnce()
    {
        await MigrateAsync();
        Operator sole = await SeedOperatorAsync("oprmut.last-admin.demote.sole", OperatorRole.Administrator);
        Assert.Equal(1L, await CountActiveAdministratorsAsync());

        const string correlationId = "oprmut-last-admin-demote";
        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        Operator soleBefore = await ReadOperatorAsync(sole.Id);
        using HttpResponseMessage response = await SendRoleChangeAsync(
            client, CreateToken(sole), correlationId, sole.Id, "teller");

        await AssertNoOpRejectionAsync(response, correlationId, soleBefore, "operator.command.change-role");
        Assert.Equal(1L, await CountActiveAdministratorsAsync());
    }

    [Fact]
    public async Task DisabledAdministratorRoleChangeIsAllowed()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprmut.disabled-admin-role.caller", OperatorRole.Administrator);
        Operator disabledAdmin = await SeedOperatorAsync(
            "oprmut.disabled-admin-role.target", OperatorRole.Administrator, OperatorState.Disabled);

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendRoleChangeAsync(
            client, CreateToken(administrator), "oprmut-disabled-admin-role", disabledAdmin.Id, "teller");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Operator persisted = await ReadOperatorAsync(disabledAdmin.Id);
        Assert.Equal(OperatorRole.Teller, persisted.Role);
        Assert.Equal(OperatorState.Disabled, persisted.State);
    }

    [Fact]
    public async Task SelfRoleChangeIsAllowedWhenNotTheLastActiveAdministrator()
    {
        await MigrateAsync();
        Operator administrator1 = await SeedOperatorAsync("oprmut.self-role.admin1", OperatorRole.Administrator);
        await SeedOperatorAsync("oprmut.self-role.admin2", OperatorRole.Administrator);

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendRoleChangeAsync(
            client, CreateToken(administrator1), "oprmut-self-role", administrator1.Id, "teller");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Operator persisted = await ReadOperatorAsync(administrator1.Id);
        Assert.Equal(OperatorRole.Teller, persisted.Role);
    }

    // ---- Double-audit exclusion --------------------------------------------------------------

    [Fact]
    public async Task PolicyAndHandlerRejectionsDoNotDoubleAudit()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("oprmut.no-double.viewer", OperatorRole.Viewer);
        Operator administrator = await SeedOperatorAsync("oprmut.no-double.admin", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("oprmut.no-double.target", OperatorRole.Teller);

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage forbidden = await SendMutationAsync(
            client, CreateToken(viewer), "oprmut-no-double-policy", target.Id, "disable");
        using HttpResponseMessage invalidRole = await SendRoleChangeAsync(
            client, CreateToken(administrator), "oprmut-no-double-handler", target.Id, "owner");

        await AssertErrorAsync(forbidden, HttpStatusCode.Forbidden, "operation_not_permitted");
        await AssertErrorAsync(invalidRole, HttpStatusCode.BadRequest, "validation_failed");
        Assert.Single(await ReadAuditsAsync("oprmut-no-double-policy"));
        Assert.Single(await ReadAuditsAsync("oprmut-no-double-handler"));
    }

    // ---- Fail-closed atomicity ------------------------------------------------------------

    [Fact]
    public async Task RequiredSuccessAuditFailureLeavesOperatorUnchangedAndNoSuccessResponse()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprmut.audit-failure.admin", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("oprmut.audit-failure.target", OperatorRole.Teller);
        int versionBefore = target.AuthorizationStateVersion;
        AuditFailureProbe failureProbe = new();

        await using OperatorMutationApiFactory factory = CreateFactory(services =>
        {
            services.RemoveAll<IAuditWriter>();
            services.AddSingleton(failureProbe);
            services.AddScoped<IAuditWriter, FailingSuccessAuditWriter>();
        });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendMutationAsync(
            client, CreateToken(administrator), "oprmut-audit-failure", target.Id, "disable");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("internal_error", body, StringComparison.Ordinal);
        Assert.Equal(1, failureProbe.InvocationCount);

        Operator persisted = await ReadOperatorAsync(target.Id);
        Assert.Equal(OperatorState.Active, persisted.State);
        Assert.Equal(versionBefore, persisted.AuthorizationStateVersion);
        Assert.Equal(0L, await CountAuditsAsync());
    }

    [Fact]
    public async Task HandlerRejectionAuditFailureFailsClosedWithoutResponseOrAudit()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprmut.rejection-audit-failure.admin", OperatorRole.Administrator);
        Guid missingIdentifier = Guid.NewGuid();
        AuditFailureProbe failureProbe = new();

        await using OperatorMutationApiFactory factory = CreateFactory(services =>
        {
            services.RemoveAll<IAuditWriter>();
            services.AddSingleton(failureProbe);
            services.AddScoped<IAuditWriter, FailingSuccessAuditWriter>();
        });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendMutationAsync(
            client, CreateToken(administrator), "oprmut-rejection-audit-failure", missingIdentifier, "disable");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("internal_error", body, StringComparison.Ordinal);
        Assert.DoesNotContain("operator_not_found", body, StringComparison.Ordinal);
        Assert.Equal(1, failureProbe.InvocationCount);
        Assert.Equal(0L, await CountAuditsAsync());
    }

    // ---- PostgreSQL concurrency -------------------------------------------------------------

    /// <summary>
    /// Exactly two active administrators exist, each concurrently disabling the other. If both
    /// succeeded independently, the active-administrator count would reach zero, so exactly one
    /// must win and the other must be rejected.
    /// </summary>
    [Fact]
    public async Task ConcurrentDisableOfTwoActiveAdministratorsNeverZerosActiveAdministratorCount()
    {
        await MigrateAsync();
        Operator admin1 = await SeedOperatorAsync("oprmut.concurrent.disable.admin1", OperatorRole.Administrator);
        Operator admin2 = await SeedOperatorAsync("oprmut.concurrent.disable.admin2", OperatorRole.Administrator);
        Assert.Equal(2L, await CountActiveAdministratorsAsync());

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        Task<HttpResponseMessage> first = SendMutationAsync(
            client, CreateToken(admin1), "oprmut-concurrent-disable-a", admin2.Id, "disable");
        Task<HttpResponseMessage> second = SendMutationAsync(
            client, CreateToken(admin2), "oprmut-concurrent-disable-b", admin1.Id, "disable");
        HttpResponseMessage[] responses = await Task.WhenAll(first, second);

        try
        {
            AssertExactlyOneSuccessAndOneConflictOrOk(responses);
            Assert.True(await CountActiveAdministratorsAsync() >= 1);
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }
    }

    /// <summary>
    /// Exactly two active administrators exist, each concurrently demoting the other. If both
    /// succeeded independently, the active-administrator count would reach zero, so exactly one
    /// must win and the other must be rejected.
    /// </summary>
    [Fact]
    public async Task ConcurrentDemotionOfTwoActiveAdministratorsNeverZerosActiveAdministratorCount()
    {
        await MigrateAsync();
        Operator admin1 = await SeedOperatorAsync("oprmut.concurrent.demote.admin1", OperatorRole.Administrator);
        Operator admin2 = await SeedOperatorAsync("oprmut.concurrent.demote.admin2", OperatorRole.Administrator);
        Assert.Equal(2L, await CountActiveAdministratorsAsync());

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        Task<HttpResponseMessage> first = SendRoleChangeAsync(
            client, CreateToken(admin1), "oprmut-concurrent-demote-a", admin2.Id, "teller");
        Task<HttpResponseMessage> second = SendRoleChangeAsync(
            client, CreateToken(admin2), "oprmut-concurrent-demote-b", admin1.Id, "viewer");
        HttpResponseMessage[] responses = await Task.WhenAll(first, second);

        try
        {
            AssertExactlyOneSuccessAndOneConflictOrOk(responses);
            Assert.True(await CountActiveAdministratorsAsync() >= 1);
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }
    }

    /// <summary>
    /// Exactly two active administrators exist: one concurrently disables the other while the
    /// other concurrently demotes the first. If both succeeded independently, the
    /// active-administrator count would reach zero, so exactly one must win.
    /// </summary>
    [Fact]
    public async Task ConcurrentDisableAndDemotionNeverZerosActiveAdministratorCount()
    {
        await MigrateAsync();
        Operator admin1 = await SeedOperatorAsync("oprmut.concurrent.mixed.admin1", OperatorRole.Administrator);
        Operator admin2 = await SeedOperatorAsync("oprmut.concurrent.mixed.admin2", OperatorRole.Administrator);
        Assert.Equal(2L, await CountActiveAdministratorsAsync());

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        Task<HttpResponseMessage> first = SendMutationAsync(
            client, CreateToken(admin1), "oprmut-concurrent-mixed-a", admin2.Id, "disable");
        Task<HttpResponseMessage> second = SendRoleChangeAsync(
            client, CreateToken(admin2), "oprmut-concurrent-mixed-b", admin1.Id, "viewer");
        HttpResponseMessage[] responses = await Task.WhenAll(first, second);

        try
        {
            AssertExactlyOneSuccessAndOneConflictOrOk(responses);
            Assert.True(await CountActiveAdministratorsAsync() >= 1);
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task LockTimeoutReturnsConcurrentOperationConflictWithHandlerAuditExactlyOnce()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprmut.lock-timeout.admin", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("oprmut.lock-timeout.target", OperatorRole.Teller);
        Operator targetBefore = await ReadOperatorAsync(target.Id);

        await using NpgsqlConnection holder = new(Database.ConnectionString);
        await holder.OpenAsync();
        await using NpgsqlTransaction holderTransaction = await holder.BeginTransactionAsync();
        await using (NpgsqlCommand lockCommand = new(
            $"SELECT * FROM {OperatorPersistence.TableName} WHERE {OperatorPersistence.IdColumn} = @id FOR UPDATE",
            holder,
            holderTransaction))
        {
            lockCommand.Parameters.AddWithValue("id", target.Id);
            await using NpgsqlDataReader reader = await lockCommand.ExecuteReaderAsync();
            await reader.ReadAsync();
        }

        try
        {
            const string correlationId = "oprmut-lock-timeout";
            await using OperatorMutationApiFactory factory = CreateFactory();
            using HttpClient client = factory.CreateClient();
            using HttpResponseMessage response = await SendMutationAsync(
                client, CreateToken(administrator), correlationId, target.Id, "disable");

            await AssertErrorAsync(response, HttpStatusCode.Conflict, "concurrent_operation_conflict");
            PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
            Assert.Equal("concurrent_operation_conflict", audit.FailureBusinessErrorCode);
            Assert.Equal(target.Id.ToString("D"), audit.TargetIdentifier);
        }
        finally
        {
            await holderTransaction.RollbackAsync();
        }

        Operator persisted = await ReadOperatorAsync(target.Id);
        Assert.Equal(targetBefore.State, persisted.State);
        Assert.Equal(targetBefore.AuthorizationStateVersion, persisted.AuthorizationStateVersion);
    }

    // ---- Helpers ------------------------------------------------------------------------------

    private static void AssertExactlyOneSuccessAndOneConflictOrOk(HttpResponseMessage[] responses)
    {
        int successCount = responses.Count(response => response.StatusCode == HttpStatusCode.OK);
        int conflictCount = responses.Count(response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, successCount);
        Assert.Equal(1, conflictCount);
    }

    private async Task AssertNoOpRejectionAsync(
        HttpResponseMessage response,
        string correlationId,
        Operator targetBefore,
        string expectedOperationIdentifier)
    {
        await AssertErrorAsync(response, HttpStatusCode.Conflict, "state_transition_not_allowed");
        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(expectedOperationIdentifier, audit.OperationIdentifier);
        Assert.Equal(targetBefore.Id.ToString("D"), audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.FailureResultToken, audit.Result);
        Assert.Equal("state_transition_not_allowed", audit.FailureBusinessErrorCode);

        Operator persisted = await ReadOperatorAsync(targetBefore.Id);
        Assert.Equal(targetBefore.State, persisted.State);
        Assert.Equal(targetBefore.Role, persisted.Role);
        Assert.Equal(targetBefore.AuthorizationStateVersion, persisted.AuthorizationStateVersion);
        Assert.Equal(targetBefore.SecurityStamp, persisted.SecurityStamp);
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
            FrozenUtcNow,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        if (state == OperatorState.Disabled)
        {
            created.Disable(FrozenUtcNow, Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        }

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

    private async Task<Operator> ReadOperatorAsync(Guid identifier)
    {
        await using BankDbContext context = CreateContext();
        return await context.Operators.AsNoTracking().SingleAsync(candidate => candidate.Id == identifier);
    }

    private async Task<long> CountAuditsAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new($"SELECT count(*) FROM {AuditPersistence.TableName};", connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private async Task<long> CountActiveAdministratorsAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"""
             SELECT count(*) FROM {OperatorPersistence.TableName}
             WHERE {OperatorPersistence.FixedRoleColumn} = '{OperatorPersistence.AdministratorRoleToken}'
               AND {OperatorPersistence.StateColumn} = '{OperatorPersistence.ActiveStateToken}';
             """,
            connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
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
        string token,
        string correlationId,
        Guid operatorIdentifier,
        string action)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, $"/operators/{operatorIdentifier:D}/{action}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendRoleChangeAsync(
        HttpClient client,
        string token,
        string correlationId,
        Guid operatorIdentifier,
        string? role)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, $"/operators/{operatorIdentifier:D}/role");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        request.Content = JsonContent.Create(new { role });
        return await client.SendAsync(request);
    }

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == expectedStatus,
            $"Expected {(int)expectedStatus} but got {(int)response.StatusCode}. Body: {body}");
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
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

internal sealed class AuditFailureProbe
{
    private int invocationCount;

    public int InvocationCount => Volatile.Read(ref invocationCount);

    public void RecordInvocation() => Interlocked.Increment(ref invocationCount);
}

/// <summary>
/// Fails on both Audit transaction primitives so it models a required-Audit failure regardless of
/// which primitive the calling code path exercises.
/// </summary>
internal sealed class FailingSuccessAuditWriter(AuditFailureProbe failureProbe) : IAuditWriter
{
    public Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        failureProbe.RecordInvocation();
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
        failureProbe.RecordInvocation();
        throw new OperatorMutationAuditFailureInjectionException();
    }
}

internal sealed class OperatorMutationAuditFailureInjectionException()
    : InvalidOperationException("Deterministic test-only Operator mutation Audit persistence failure.");
