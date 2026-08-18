extern alias api;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MinimalBankSystem.Api.OperatorMutation;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Application.Runtime;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Auditing;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using MinimalBankSystem.IntegrationTests.PostgreSql;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.OperatorMutation;

[Collection(TestExecutionCollections.ConsoleSensitive)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class OperatorMutationTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task EnableSucceedsWithExactProjectionVersionStampAndUpdatedAt()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator administrator = await SeedAsync("opr.mut.enable.admin", OperatorRole.Administrator);
        Operator target = await OperatorMutationTestSupport.SeedDisabledOperatorAsync(
            Database.ConnectionString,
            "opr.mut.enable.target",
            OperatorRole.Teller);
        int versionBefore = target.AuthorizationStateVersion;
        string stampBefore = target.SecurityStamp;
        DateTimeOffset updatedAtBefore = target.UpdatedAt;
        DateTimeOffset frozenNow = OperatorMutationTestSupport.FrozenUtcNow.AddHours(2);

        await using OperatorMutationApiFactory factory = CreateFactory(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FrozenTimeProvider(frozenNow));
            services.RemoveAll<ApplicationTime>();
            services.AddSingleton<ApplicationTime>();
        });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorMutationTestSupport.SendEnableAsync(
            client,
            OperatorMutationTestSupport.CreateToken(administrator),
            target.Id,
            "opr-mut-enable-success");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        OperatorMutationTestSupport.AssertExactProjection(
            document.RootElement,
            target.Id,
            OperatorPersistence.ActiveStateToken,
            OperatorPersistence.TellerRoleToken);

        Operator persisted = await OperatorMutationTestSupport.ReadOperatorAsync(Database.ConnectionString, target.Id);
        Assert.Equal(OperatorState.Active, persisted.State);
        Assert.Equal(OperatorRole.Teller, persisted.Role);
        Assert.Equal(versionBefore + 1, persisted.AuthorizationStateVersion);
        Assert.NotEqual(stampBefore, persisted.SecurityStamp);
        Assert.Equal(frozenNow, persisted.UpdatedAt);
        Assert.True(persisted.UpdatedAt > updatedAtBefore);

        PersistedOperatorMutationAudit audit = Assert.Single(
            await OperatorMutationTestSupport.ReadAuditsAsync(Database.ConnectionString, "opr-mut-enable-success"));
        Assert.Equal(administrator.Id, audit.ActorIdentifier);
        Assert.Equal("operator.command.enable", audit.OperationIdentifier);
        Assert.Equal(target.Id.ToString("D"), audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.SuccessResultToken, audit.Result);
        Assert.Null(audit.FailureBusinessErrorCode);
    }

    [Fact]
    public async Task DisableSucceedsWithExactProjectionAndStaleAuthRejection()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator administrator = await SeedAsync("opr.mut.disable.admin", OperatorRole.Administrator);
        Operator target = await SeedAsync("opr.mut.disable.target", OperatorRole.Teller);
        string staleToken = OperatorMutationTestSupport.CreateToken(target);
        int versionBefore = target.AuthorizationStateVersion;
        string stampBefore = target.SecurityStamp;

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorMutationTestSupport.SendDisableAsync(
            client,
            OperatorMutationTestSupport.CreateToken(administrator),
            target.Id,
            "opr-mut-disable-success");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        OperatorMutationTestSupport.AssertExactProjection(
            document.RootElement,
            target.Id,
            OperatorPersistence.DisabledStateToken,
            OperatorPersistence.TellerRoleToken);

        Operator persisted = await OperatorMutationTestSupport.ReadOperatorAsync(Database.ConnectionString, target.Id);
        Assert.Equal(OperatorState.Disabled, persisted.State);
        Assert.Equal(versionBefore + 1, persisted.AuthorizationStateVersion);
        Assert.NotEqual(stampBefore, persisted.SecurityStamp);

        PersistedOperatorMutationAudit audit = Assert.Single(
            await OperatorMutationTestSupport.ReadAuditsAsync(Database.ConnectionString, "opr-mut-disable-success"));
        Assert.Equal("operator.command.disable", audit.OperationIdentifier);
        Assert.Equal(target.Id.ToString("D"), audit.TargetIdentifier);

        using HttpResponseMessage stale = await OperatorMutationTestSupport.SendEnableAsync(
            client,
            staleToken,
            administrator.Id,
            "opr-mut-disable-stale-auth");
        await OperatorMutationTestSupport.AssertErrorAsync(
            stale,
            HttpStatusCode.Unauthorized,
            "authentication_required");
        Assert.Equal(0L, await OperatorMutationTestSupport.CountAuditsAsync(
            Database.ConnectionString,
            "opr-mut-disable-stale-auth"));
        Operator actorAfterStale = await OperatorMutationTestSupport.ReadOperatorAsync(
            Database.ConnectionString,
            administrator.Id);
        Assert.Equal(OperatorState.Active, actorAfterStale.State);
        Assert.Equal(administrator.AuthorizationStateVersion, actorAfterStale.AuthorizationStateVersion);
    }

    [Fact]
    public async Task RoleChangeSucceedsWithExactProjectionAndStaleAuthRejection()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator administrator = await SeedAsync("opr.mut.role.admin", OperatorRole.Administrator);
        Operator target = await SeedAsync("opr.mut.role.target", OperatorRole.Teller);
        string staleToken = OperatorMutationTestSupport.CreateToken(target);
        int versionBefore = target.AuthorizationStateVersion;
        string stampBefore = target.SecurityStamp;

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorMutationTestSupport.SendRoleChangeAsync(
            client,
            OperatorMutationTestSupport.CreateToken(administrator),
            target.Id,
            "opr-mut-role-success",
            "viewer");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        OperatorMutationTestSupport.AssertExactProjection(
            document.RootElement,
            target.Id,
            OperatorPersistence.ActiveStateToken,
            OperatorPersistence.ViewerRoleToken);

        Operator persisted = await OperatorMutationTestSupport.ReadOperatorAsync(Database.ConnectionString, target.Id);
        Assert.Equal(OperatorRole.Viewer, persisted.Role);
        Assert.Equal(OperatorState.Active, persisted.State);
        Assert.Equal(versionBefore + 1, persisted.AuthorizationStateVersion);
        Assert.NotEqual(stampBefore, persisted.SecurityStamp);

        PersistedOperatorMutationAudit audit = Assert.Single(
            await OperatorMutationTestSupport.ReadAuditsAsync(Database.ConnectionString, "opr-mut-role-success"));
        Assert.Equal("operator.command.change-role", audit.OperationIdentifier);
        Assert.Equal(target.Id.ToString("D"), audit.TargetIdentifier);

        using HttpResponseMessage stale = await OperatorMutationTestSupport.SendDisableAsync(
            client,
            staleToken,
            administrator.Id,
            "opr-mut-role-stale-auth");
        await OperatorMutationTestSupport.AssertErrorAsync(
            stale,
            HttpStatusCode.Unauthorized,
            "authentication_required");
        Assert.Equal(0L, await OperatorMutationTestSupport.CountAuditsAsync(
            Database.ConnectionString,
            "opr-mut-role-stale-auth"));
    }

    [Fact]
    public async Task SuccessProjectionOmitsCredentialSecurityStampAndAuthorizationStateVersion()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator administrator = await SeedAsync("opr.mut.projection.admin", OperatorRole.Administrator);
        Operator target = await SeedAsync("opr.mut.projection.target", OperatorRole.Viewer);

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorMutationTestSupport.SendRoleChangeAsync(
            client,
            OperatorMutationTestSupport.CreateToken(administrator),
            target.Id,
            "opr-mut-projection",
            "teller");

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        foreach (string prohibited in new[]
                 {
                     "password",
                     "passwordHash",
                     "securityStamp",
                     "authorizationStateVersion",
                     "credential",
                 })
        {
            Assert.DoesNotContain(prohibited, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task UnauthenticatedRequestReturns401WithoutProductAudit()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator target = await SeedAsync("opr.mut.unauth.target", OperatorRole.Teller);

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorMutationTestSupport.SendMutationAsync(
            client,
            token: null,
            $"/operators/{target.Id:D}/disable",
            "opr-mut-unauth",
            content: null);

        await OperatorMutationTestSupport.AssertErrorAsync(
            response,
            HttpStatusCode.Unauthorized,
            "authentication_required");
        Assert.Equal(0L, await OperatorMutationTestSupport.CountAuditsAsync(
            Database.ConnectionString,
            "opr-mut-unauth"));
        Operator persisted = await OperatorMutationTestSupport.ReadOperatorAsync(Database.ConnectionString, target.Id);
        Assert.Equal(OperatorState.Active, persisted.State);
        Assert.Equal(target.AuthorizationStateVersion, persisted.AuthorizationStateVersion);
        Assert.Equal(target.SecurityStamp, persisted.SecurityStamp);
    }

    [Theory]
    [InlineData(OperatorRole.Viewer)]
    [InlineData(OperatorRole.Teller)]
    public async Task NonAdministratorReturns403WithAuthzAuditExactlyOnceWithoutHandlerReach(
        OperatorRole nonAdminRole)
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator nonAdmin = await SeedAsync($"opr.mut.forbidden.{nonAdminRole}", nonAdminRole);
        Operator target = await SeedAsync($"opr.mut.forbidden.target.{nonAdminRole}", OperatorRole.Viewer);
        OperatorMutationActionReachProbe reachProbe = new();
        string correlationId = $"opr-mut-forbidden-{nonAdminRole}".ToLowerInvariant();

        await using OperatorMutationApiFactory factory = CreateFactory(services =>
        {
            services.Configure<MvcOptions>(options =>
                options.Filters.Add(new OperatorMutationActionReachProbeFilter(reachProbe)));
        });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorMutationTestSupport.SendDisableAsync(
            client,
            OperatorMutationTestSupport.CreateToken(nonAdmin),
            target.Id,
            correlationId);

        await OperatorMutationTestSupport.AssertErrorAsync(
            response,
            HttpStatusCode.Forbidden,
            "operation_not_permitted");
        Assert.Equal(0, reachProbe.InvocationCount);

        PersistedOperatorMutationAudit audit = Assert.Single(
            await OperatorMutationTestSupport.ReadAuditsAsync(Database.ConnectionString, correlationId));
        Assert.Equal(nonAdmin.Id, audit.ActorIdentifier);
        Assert.Equal("operator.command.disable", audit.OperationIdentifier);
        Assert.Equal(target.Id.ToString("D"), audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.FailureResultToken, audit.Result);
        Assert.Equal("operation_not_permitted", audit.FailureBusinessErrorCode);

        Operator persisted = await OperatorMutationTestSupport.ReadOperatorAsync(Database.ConnectionString, target.Id);
        Assert.Equal(OperatorState.Active, persisted.State);
        Assert.Equal(target.AuthorizationStateVersion, persisted.AuthorizationStateVersion);
        Assert.Equal(target.SecurityStamp, persisted.SecurityStamp);
    }

    [Fact]
    public async Task MissingOperatorReturns404WithRequestedCanonicalGuidAuditExactlyOnce()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator administrator = await SeedAsync("opr.mut.missing.admin", OperatorRole.Administrator);
        Guid missing = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorMutationTestSupport.SendEnableAsync(
            client,
            OperatorMutationTestSupport.CreateToken(administrator),
            missing,
            "opr-mut-missing");

        await OperatorMutationTestSupport.AssertErrorAsync(
            response,
            HttpStatusCode.NotFound,
            "operator_not_found");

        PersistedOperatorMutationAudit audit = Assert.Single(
            await OperatorMutationTestSupport.ReadAuditsAsync(Database.ConnectionString, "opr-mut-missing"));
        Assert.Equal("operator.command.enable", audit.OperationIdentifier);
        Assert.Equal(missing.ToString("D"), audit.TargetIdentifier);
        Assert.DoesNotContain(administrator.UserName, audit.TargetIdentifier, StringComparison.Ordinal);
        Assert.Equal("operator_not_found", audit.FailureBusinessErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Administrator")]
    [InlineData("owner")]
    public async Task InvalidRoleReturns400WithHandlerRejectionAuditExactlyOnce(string? role)
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator administrator = await SeedAsync($"opr.mut.invalid-role.admin.{role ?? "null"}", OperatorRole.Administrator);
        Operator target = await SeedAsync($"opr.mut.invalid-role.target.{role ?? "null"}", OperatorRole.Teller);
        string correlationId = $"opr-mut-invalid-role-{role ?? "null"}";

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = role is null
            ? await OperatorMutationTestSupport.SendRawRoleChangeAsync(
                client,
                OperatorMutationTestSupport.CreateToken(administrator),
                target.Id,
                correlationId,
                """{"role":null}""")
            : await OperatorMutationTestSupport.SendRoleChangeAsync(
                client,
                OperatorMutationTestSupport.CreateToken(administrator),
                target.Id,
                correlationId,
                role);

        await OperatorMutationTestSupport.AssertErrorAsync(
            response,
            HttpStatusCode.BadRequest,
            "validation_failed");

        Operator persisted = await OperatorMutationTestSupport.ReadOperatorAsync(Database.ConnectionString, target.Id);
        Assert.Equal(OperatorRole.Teller, persisted.Role);
        Assert.Equal(target.AuthorizationStateVersion, persisted.AuthorizationStateVersion);
        Assert.Equal(target.SecurityStamp, persisted.SecurityStamp);

        PersistedOperatorMutationAudit audit = Assert.Single(
            await OperatorMutationTestSupport.ReadAuditsAsync(Database.ConnectionString, correlationId));
        Assert.Equal("operator.command.change-role", audit.OperationIdentifier);
        Assert.Equal(target.Id.ToString("D"), audit.TargetIdentifier);
        Assert.Equal("validation_failed", audit.FailureBusinessErrorCode);
    }

    [Fact]
    public async Task NoOpEnableDisableAndSameRoleReturn409WithoutChangingVersionOrStamp()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator administrator = await SeedAsync("opr.mut.noop.admin", OperatorRole.Administrator);
        Operator activeTeller = await SeedAsync("opr.mut.noop.active", OperatorRole.Teller);
        Operator disabledTeller = await OperatorMutationTestSupport.SeedDisabledOperatorAsync(
            Database.ConnectionString,
            "opr.mut.noop.disabled",
            OperatorRole.Teller);

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage enableNoOp = await OperatorMutationTestSupport.SendEnableAsync(
            client,
            OperatorMutationTestSupport.CreateToken(administrator),
            activeTeller.Id,
            "opr-mut-noop-enable");
        await AssertUnchangedRejectionAsync(
            enableNoOp,
            "opr-mut-noop-enable",
            "operator.command.enable",
            activeTeller);

        using HttpResponseMessage disableNoOp = await OperatorMutationTestSupport.SendDisableAsync(
            client,
            OperatorMutationTestSupport.CreateToken(administrator),
            disabledTeller.Id,
            "opr-mut-noop-disable");
        await AssertUnchangedRejectionAsync(
            disableNoOp,
            "opr-mut-noop-disable",
            "operator.command.disable",
            disabledTeller);

        using HttpResponseMessage sameRole = await OperatorMutationTestSupport.SendRoleChangeAsync(
            client,
            OperatorMutationTestSupport.CreateToken(administrator),
            activeTeller.Id,
            "opr-mut-noop-role",
            "teller");
        await AssertUnchangedRejectionAsync(
            sameRole,
            "opr-mut-noop-role",
            "operator.command.change-role",
            activeTeller);
    }

    [Fact]
    public async Task SelfDisableReturns409WithoutChangingState()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator administrator = await SeedAsync("opr.mut.self.admin", OperatorRole.Administrator);
        _ = await SeedAsync("opr.mut.self.other-admin", OperatorRole.Administrator);

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorMutationTestSupport.SendDisableAsync(
            client,
            OperatorMutationTestSupport.CreateToken(administrator),
            administrator.Id,
            "opr-mut-self-disable");

        await AssertUnchangedRejectionAsync(
            response,
            "opr-mut-self-disable",
            "operator.command.disable",
            administrator);
        Assert.Equal(
            2L,
            await OperatorMutationTestSupport.CountActiveAdministratorsAsync(Database.ConnectionString));
    }

    [Fact]
    public async Task LastActiveAdministratorDisableAndDemotionReturn409()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator lastAdmin = await SeedAsync("opr.mut.last.admin", OperatorRole.Administrator);
        Operator teller = await SeedAsync("opr.mut.last.teller", OperatorRole.Teller);

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage disable = await OperatorMutationTestSupport.SendDisableAsync(
            client,
            OperatorMutationTestSupport.CreateToken(lastAdmin),
            lastAdmin.Id,
            "opr-mut-last-disable");
        await AssertUnchangedRejectionAsync(
            disable,
            "opr-mut-last-disable",
            "operator.command.disable",
            lastAdmin);

        using HttpResponseMessage demote = await OperatorMutationTestSupport.SendRoleChangeAsync(
            client,
            OperatorMutationTestSupport.CreateToken(lastAdmin),
            lastAdmin.Id,
            "opr-mut-last-demote",
            "viewer");
        await AssertUnchangedRejectionAsync(
            demote,
            "opr-mut-last-demote",
            "operator.command.change-role",
            lastAdmin);

        Assert.Equal(
            1L,
            await OperatorMutationTestSupport.CountActiveAdministratorsAsync(Database.ConnectionString));
        Operator tellerAfter = await OperatorMutationTestSupport.ReadOperatorAsync(Database.ConnectionString, teller.Id);
        Assert.Equal(OperatorRole.Teller, tellerAfter.Role);
        Assert.Equal(teller.AuthorizationStateVersion, tellerAfter.AuthorizationStateVersion);
    }

    [Fact]
    public async Task DisabledAdministratorRoleChangeIsAllowed()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator administrator = await SeedAsync("opr.mut.disabled-admin.actor", OperatorRole.Administrator);
        Operator disabledAdmin = await OperatorMutationTestSupport.SeedDisabledOperatorAsync(
            Database.ConnectionString,
            "opr.mut.disabled-admin.target",
            OperatorRole.Administrator);

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorMutationTestSupport.SendRoleChangeAsync(
            client,
            OperatorMutationTestSupport.CreateToken(administrator),
            disabledAdmin.Id,
            "opr-mut-disabled-admin-role",
            "teller");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Operator persisted = await OperatorMutationTestSupport.ReadOperatorAsync(
            Database.ConnectionString,
            disabledAdmin.Id);
        Assert.Equal(OperatorState.Disabled, persisted.State);
        Assert.Equal(OperatorRole.Teller, persisted.Role);
        Assert.Equal(disabledAdmin.AuthorizationStateVersion + 1, persisted.AuthorizationStateVersion);
        Assert.Equal(
            1L,
            await OperatorMutationTestSupport.CountActiveAdministratorsAsync(Database.ConnectionString));
    }

    [Fact]
    public async Task PolicyAndHandlerRejectionsDoNotDoubleAudit()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator viewer = await SeedAsync("opr.mut.nodouble.viewer", OperatorRole.Viewer);
        Operator administrator = await SeedAsync("opr.mut.nodouble.admin", OperatorRole.Administrator);
        Operator target = await SeedAsync("opr.mut.nodouble.target", OperatorRole.Teller);

        await using OperatorMutationApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage forbidden = await OperatorMutationTestSupport.SendDisableAsync(
            client,
            OperatorMutationTestSupport.CreateToken(viewer),
            target.Id,
            "opr-mut-nodouble-policy");
        await OperatorMutationTestSupport.AssertErrorAsync(
            forbidden,
            HttpStatusCode.Forbidden,
            "operation_not_permitted");
        Assert.Single(await OperatorMutationTestSupport.ReadAuditsAsync(
            Database.ConnectionString,
            "opr-mut-nodouble-policy"));

        using HttpResponseMessage invalidRole = await OperatorMutationTestSupport.SendRoleChangeAsync(
            client,
            OperatorMutationTestSupport.CreateToken(administrator),
            target.Id,
            "opr-mut-nodouble-handler",
            "owner");
        await OperatorMutationTestSupport.AssertErrorAsync(
            invalidRole,
            HttpStatusCode.BadRequest,
            "validation_failed");
        Assert.Single(await OperatorMutationTestSupport.ReadAuditsAsync(
            Database.ConnectionString,
            "opr-mut-nodouble-handler"));
    }

    [Fact]
    public async Task RequiredSuccessAuditFailureRollsBackMutation()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator administrator = await SeedAsync("opr.mut.audfail.admin", OperatorRole.Administrator);
        Operator target = await SeedAsync("opr.mut.audfail.target", OperatorRole.Teller);
        OperatorMutationAuditFailureProbe probe = new();

        await using OperatorMutationApiFactory factory = CreateFactory(services =>
        {
            services.RemoveAll<IAuditWriter>();
            services.AddSingleton(probe);
            services.AddScoped<IAuditWriter, OperatorMutationFailingAuditWriter>();
        });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorMutationTestSupport.SendDisableAsync(
            client,
            OperatorMutationTestSupport.CreateToken(administrator),
            target.Id,
            "opr-mut-success-audit-fail");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(probe.CurrentTransactionFailures > 0);
        Operator persisted = await OperatorMutationTestSupport.ReadOperatorAsync(Database.ConnectionString, target.Id);
        Assert.Equal(OperatorState.Active, persisted.State);
        Assert.Equal(target.AuthorizationStateVersion, persisted.AuthorizationStateVersion);
        Assert.Equal(target.SecurityStamp, persisted.SecurityStamp);
        Assert.Equal(0L, await OperatorMutationTestSupport.CountAuditsAsync(
            Database.ConnectionString,
            "opr-mut-success-audit-fail"));
    }

    [Fact]
    public async Task RejectionAuditFailureIsFailClosedAndLeavesStateUnchanged()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator administrator = await SeedAsync("opr.mut.rejfail.admin", OperatorRole.Administrator);
        Operator target = await SeedAsync("opr.mut.rejfail.target", OperatorRole.Teller);
        OperatorMutationAuditFailureProbe probe = new();

        await using OperatorMutationApiFactory factory = CreateFactory(services =>
        {
            services.RemoveAll<IAuditWriter>();
            services.AddSingleton(probe);
            services.AddScoped<IAuditWriter, OperatorMutationFailingAuditWriter>();
        });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await OperatorMutationTestSupport.SendEnableAsync(
            client,
            OperatorMutationTestSupport.CreateToken(administrator),
            target.Id,
            "opr-mut-rejection-audit-fail");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(probe.SeparateTransactionFailures > 0);
        Operator persisted = await OperatorMutationTestSupport.ReadOperatorAsync(Database.ConnectionString, target.Id);
        Assert.Equal(OperatorState.Active, persisted.State);
        Assert.Equal(target.AuthorizationStateVersion, persisted.AuthorizationStateVersion);
        Assert.Equal(0L, await OperatorMutationTestSupport.CountAuditsAsync(
            Database.ConnectionString,
            "opr-mut-rejection-audit-fail"));
    }

    [Fact]
    public async Task LockTimeoutAndDeadlockMapToConcurrentOperationConflictWithRejectionAudit()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator administrator = await SeedAsync("opr.mut.conflict.admin", OperatorRole.Administrator);
        Operator target = await SeedAsync("opr.mut.conflict.target", OperatorRole.Teller);

        await using OperatorMutationApiFactory factory = CreateFactory(services =>
        {
            services.RemoveAll<IOperatorMutationLockSession>();
            services.AddScoped<IOperatorMutationLockSession>(_ =>
                new ThrowingOperatorMutationLockSession(PostgresErrorCodes.DeadlockDetected));
        });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage deadlock = await OperatorMutationTestSupport.SendDisableAsync(
            client,
            OperatorMutationTestSupport.CreateToken(administrator),
            target.Id,
            "opr-mut-deadlock");

        await OperatorMutationTestSupport.AssertErrorAsync(
            deadlock,
            HttpStatusCode.Conflict,
            "concurrent_operation_conflict");
        PersistedOperatorMutationAudit deadlockAudit = Assert.Single(
            await OperatorMutationTestSupport.ReadAuditsAsync(Database.ConnectionString, "opr-mut-deadlock"));
        Assert.Equal("operator.command.disable", deadlockAudit.OperationIdentifier);
        Assert.Equal(target.Id.ToString("D"), deadlockAudit.TargetIdentifier);
        Assert.Equal("concurrent_operation_conflict", deadlockAudit.FailureBusinessErrorCode);

        await using OperatorMutationApiFactory timeoutFactory = CreateFactory(services =>
        {
            services.RemoveAll<IOperatorMutationLockSession>();
            services.AddScoped<IOperatorMutationLockSession>(_ =>
                new ThrowingOperatorMutationLockSession(PostgresErrorCodes.LockNotAvailable));
        });
        using HttpClient timeoutClient = timeoutFactory.CreateClient();
        using HttpResponseMessage timeout = await OperatorMutationTestSupport.SendDisableAsync(
            timeoutClient,
            OperatorMutationTestSupport.CreateToken(administrator),
            target.Id,
            "opr-mut-lock-timeout");
        await OperatorMutationTestSupport.AssertErrorAsync(
            timeout,
            HttpStatusCode.Conflict,
            "concurrent_operation_conflict");

        Operator persisted = await OperatorMutationTestSupport.ReadOperatorAsync(Database.ConnectionString, target.Id);
        Assert.Equal(OperatorState.Active, persisted.State);
        Assert.Equal(target.AuthorizationStateVersion, persisted.AuthorizationStateVersion);
        Assert.Equal(target.SecurityStamp, persisted.SecurityStamp);
    }

    private OperatorMutationApiFactory CreateFactory(Action<IServiceCollection>? configureServices = null) =>
        new(Database.ConnectionString, configureServices);

    private Task<Operator> SeedAsync(string userName, OperatorRole role) =>
        OperatorMutationTestSupport.SeedOperatorAsync(Database.ConnectionString, userName, role);

    private async Task AssertUnchangedRejectionAsync(
        HttpResponseMessage response,
        string correlationId,
        string operationIdentifier,
        Operator original)
    {
        await OperatorMutationTestSupport.AssertErrorAsync(
            response,
            HttpStatusCode.Conflict,
            "state_transition_not_allowed");

        Operator persisted = await OperatorMutationTestSupport.ReadOperatorAsync(Database.ConnectionString, original.Id);
        Assert.Equal(original.State, persisted.State);
        Assert.Equal(original.Role, persisted.Role);
        Assert.Equal(original.AuthorizationStateVersion, persisted.AuthorizationStateVersion);
        Assert.Equal(original.SecurityStamp, persisted.SecurityStamp);
        Assert.Equal(original.UpdatedAt, persisted.UpdatedAt);

        PersistedOperatorMutationAudit audit = Assert.Single(
            await OperatorMutationTestSupport.ReadAuditsAsync(Database.ConnectionString, correlationId));
        Assert.Equal(operationIdentifier, audit.OperationIdentifier);
        Assert.Equal(original.Id.ToString("D"), audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.FailureResultToken, audit.Result);
        Assert.Equal("state_transition_not_allowed", audit.FailureBusinessErrorCode);
        Assert.DoesNotContain(original.UserName, audit.TargetIdentifier, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class OperatorMutationActionReachProbe
{
    private int invocationCount;

    public int InvocationCount => Volatile.Read(ref invocationCount);

    public void RecordInvocation() => Interlocked.Increment(ref invocationCount);
}

internal sealed class OperatorMutationActionReachProbeFilter(OperatorMutationActionReachProbe probe)
    : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.Request.Method == HttpMethods.Post &&
            context.HttpContext.Request.Path.StartsWithSegments("/operators"))
        {
            probe.RecordInvocation();
        }

        await next().ConfigureAwait(false);
    }
}

internal sealed class OperatorMutationAuditFailureProbe : IDisposable
{
    private int currentTransactionFailures;
    private int separateTransactionFailures;

    public int CurrentTransactionFailures => Volatile.Read(ref currentTransactionFailures);

    public int SeparateTransactionFailures => Volatile.Read(ref separateTransactionFailures);

    public void RecordCurrentTransactionFailure() => Interlocked.Increment(ref currentTransactionFailures);

    public void RecordSeparateTransactionFailure() => Interlocked.Increment(ref separateTransactionFailures);

    public void Dispose()
    {
    }
}

internal sealed class OperatorMutationFailingAuditWriter(OperatorMutationAuditFailureProbe probe) : IAuditWriter
{
    public Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        probe.RecordCurrentTransactionFailure();
        throw new InvalidOperationException("Deterministic test-only Operator mutation success Audit failure.");
    }

    public Task<TResult> AppendInSeparateTransactionBeforeResultAsync<TResult>(
        AuditWriteRequest request,
        Func<CancellationToken, Task<TResult>> successResultFactory,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = successResultFactory;
        _ = cancellationToken;
        probe.RecordSeparateTransactionFailure();
        throw new InvalidOperationException("Deterministic test-only Operator mutation rejection Audit failure.");
    }
}

internal sealed class ThrowingOperatorMutationLockSession(string sqlState) : IOperatorMutationLockSession
{
    public Task SetLockTimeoutAsync(BankDbContext persistence, CancellationToken cancellationToken)
    {
        _ = persistence;
        _ = cancellationToken;
        throw CreatePostgresException();
    }

    public Task<IReadOnlyList<Guid>> LockActiveAdministratorIdentifiersAsync(
        BankDbContext persistence,
        CancellationToken cancellationToken)
    {
        _ = persistence;
        _ = cancellationToken;
        throw CreatePostgresException();
    }

    public Task<bool> TryLockOperatorByIdAsync(
        BankDbContext persistence,
        Guid operatorId,
        CancellationToken cancellationToken)
    {
        _ = persistence;
        _ = operatorId;
        _ = cancellationToken;
        throw CreatePostgresException();
    }

    private PostgresException CreatePostgresException() =>
        new("injected operator-mutation concurrency abort", "ERROR", "ERROR", sqlState);
}
