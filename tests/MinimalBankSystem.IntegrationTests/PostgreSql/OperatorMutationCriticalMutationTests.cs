extern alias api;

using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MinimalBankSystem.Api.OperatorMutation;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.IntegrationTests.Authorization;
using MinimalBankSystem.IntegrationTests.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.OperatorMutation;

[Collection(TestExecutionCollections.ConsoleSensitive)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class OperatorMutationCriticalMutationTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task OprMutAdmin01ActiveAdministratorInvariantBypassIsKilled()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);

        await RunAdminPairAsync(
            "opr.mut.admin01.baseline",
            configureServices: null,
            expectedMinimumActiveAdministrators: 1,
            "OPR-MUT-ADMIN-01 BASELINE_GREEN");

        await OperatorMutationTestSupport.DeleteAllOperatorsAsync(Database.ConnectionString);
        await RunAdminPairAsync(
            "opr.mut.admin01.mutated",
            services =>
            {
                services.RemoveAll<ILastActiveAdministratorInvariant>();
                services.AddScoped<ILastActiveAdministratorInvariant, LastActiveAdministratorBypassInvariant>();
            },
            expectedMinimumActiveAdministrators: 0,
            "OPR-MUT-ADMIN-01 MUTATION_RED");

        long mutatedCount = await OperatorMutationTestSupport.CountActiveAdministratorsAsync(
            Database.ConnectionString);
        Assert.True(
            mutatedCount == 0,
            $"OPR-MUT-ADMIN-01 MUTATION_RED: expected committed active administrator count == 0 after last-admin bypass, but observed {mutatedCount}.");

        await OperatorMutationTestSupport.DeleteAllOperatorsAsync(Database.ConnectionString);
        await RunAdminPairAsync(
            "opr.mut.admin01.restored",
            configureServices: null,
            expectedMinimumActiveAdministrators: 1,
            "OPR-MUT-ADMIN-01 RESTORE_GREEN");
    }

    [Fact]
    public async Task OprMutAuth01AuthorizationStateInvalidationBypassIsKilled()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator administrator = await SeedAsync("opr.mut.auth01.admin", OperatorRole.Administrator);

        Operator baselineTarget = await SeedAsync("opr.mut.auth01.baseline", OperatorRole.Teller);
        string baselineToken = OperatorMutationTestSupport.CreateToken(baselineTarget);
        using (AuthorizationProbeApiFactory baseline = CreateProbeFactory())
        {
            using HttpClient client = baseline.CreateClient();
            using HttpResponseMessage mutation = await OperatorMutationTestSupport.SendRoleChangeAsync(
                client,
                OperatorMutationTestSupport.CreateToken(administrator),
                baselineTarget.Id,
                "opr-mut-auth01-baseline",
                "viewer");
            Assert.Equal(HttpStatusCode.OK, mutation.StatusCode);
            using HttpResponseMessage stale = await GetFallbackAsync(client, baselineToken, "auth01-baseline");
            Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
        }

        Operator mutatedTarget = await SeedAsync("opr.mut.auth01.mutated", OperatorRole.Teller);
        string mutatedToken = OperatorMutationTestSupport.CreateToken(mutatedTarget);
        int versionBefore = mutatedTarget.AuthorizationStateVersion;
        string stampBefore = mutatedTarget.SecurityStamp;
        using (AuthorizationProbeApiFactory mutated = CreateProbeFactory(services =>
               {
                   services.RemoveAll<IOperatorMutationEffect>();
                   services.AddScoped<IOperatorMutationEffect, AuthorizationStateInvalidationBypassEffect>();
               }))
        {
            using HttpClient client = mutated.CreateClient();
            using HttpResponseMessage mutation = await OperatorMutationTestSupport.SendRoleChangeAsync(
                client,
                OperatorMutationTestSupport.CreateToken(administrator),
                mutatedTarget.Id,
                "opr-mut-auth01-mutated",
                "viewer");
            Assert.Equal(HttpStatusCode.OK, mutation.StatusCode);

            Operator persisted = await OperatorMutationTestSupport.ReadOperatorAsync(
                Database.ConnectionString,
                mutatedTarget.Id);
            Assert.Equal(OperatorRole.Viewer, persisted.Role);
            Assert.Equal(versionBefore, persisted.AuthorizationStateVersion);
            Assert.Equal(stampBefore, persisted.SecurityStamp);

            using HttpResponseMessage stale = await GetFallbackAsync(client, mutatedToken, "auth01-mutated");
            Assert.True(
                stale.StatusCode == HttpStatusCode.OK,
                $"OPR-MUT-AUTH-01 MUTATION_RED: old authenticated state remained unauthorized. Status: {stale.StatusCode}.");
        }

        Operator restoredTarget = await SeedAsync("opr.mut.auth01.restored", OperatorRole.Teller);
        string restoredToken = OperatorMutationTestSupport.CreateToken(restoredTarget);
        using (AuthorizationProbeApiFactory restored = CreateProbeFactory())
        {
            using HttpClient client = restored.CreateClient();
            using HttpResponseMessage mutation = await OperatorMutationTestSupport.SendRoleChangeAsync(
                client,
                OperatorMutationTestSupport.CreateToken(administrator),
                restoredTarget.Id,
                "opr-mut-auth01-restored",
                "viewer");
            Assert.Equal(HttpStatusCode.OK, mutation.StatusCode);
            using HttpResponseMessage stale = await GetFallbackAsync(client, restoredToken, "auth01-restored");
            Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
        }
    }

    [Fact]
    public async Task OprMutAud01RequiredSuccessAuditAtomicityBypassIsKilled()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator administrator = await SeedAsync("opr.mut.aud01.admin", OperatorRole.Administrator);
        string token = OperatorMutationTestSupport.CreateToken(administrator);

        Operator baselineTarget = await SeedAsync("opr.mut.aud01.baseline", OperatorRole.Teller);
        using (OperatorMutationAuditFailureProbe baselineProbe = new())
        {
            await using OperatorMutationApiFactory baseline = CreateFactory(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton(baselineProbe);
                services.AddScoped<IAuditWriter, OperatorMutationFailingAuditWriter>();
            });
            using HttpClient client = baseline.CreateClient();
            using HttpResponseMessage response = await OperatorMutationTestSupport.SendDisableAsync(
                client,
                token,
                baselineTarget.Id,
                "opr-mut-aud01-baseline");
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(1, baselineProbe.CurrentTransactionFailures);
        }

        Operator baselinePersisted = await OperatorMutationTestSupport.ReadOperatorAsync(
            Database.ConnectionString,
            baselineTarget.Id);
        Assert.Equal(OperatorState.Active, baselinePersisted.State);
        Assert.Equal(0L, await OperatorMutationTestSupport.CountAuditsAsync(
            Database.ConnectionString,
            "opr-mut-aud01-baseline"));

        Operator mutatedTarget = await SeedAsync("opr.mut.aud01.mutated", OperatorRole.Teller);
        using (OperatorMutationAuditFailureProbe mutatedProbe = new())
        {
            await using OperatorMutationApiFactory mutated = CreateFactory(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton(mutatedProbe);
                services.AddScoped<IAuditWriter, OperatorMutationFailingAuditWriter>();
                services.RemoveAll<IOperatorMutationSuccessCommitter>();
                services.AddScoped<IOperatorMutationSuccessCommitter, NonAtomicOperatorMutationSuccessCommitter>();
            });
            using HttpClient client = mutated.CreateClient();
            using HttpResponseMessage response = await OperatorMutationTestSupport.SendDisableAsync(
                client,
                token,
                mutatedTarget.Id,
                "opr-mut-aud01-mutated");
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(1, mutatedProbe.CurrentTransactionFailures);
        }

        Operator mutatedPersisted = await OperatorMutationTestSupport.ReadOperatorAsync(
            Database.ConnectionString,
            mutatedTarget.Id);
        Assert.True(
            mutatedPersisted.State == OperatorState.Disabled,
            "OPR-MUT-AUD-01 MUTATION_RED: expected the non-atomic commit path to persist the state change without the required success Audit.");
        Assert.Equal(0L, await OperatorMutationTestSupport.CountAuditsAsync(
            Database.ConnectionString,
            "opr-mut-aud01-mutated"));

        Operator restoredTarget = await SeedAsync("opr.mut.aud01.restored", OperatorRole.Teller);
        using (OperatorMutationAuditFailureProbe restoredProbe = new())
        {
            await using OperatorMutationApiFactory restored = CreateFactory(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton(restoredProbe);
                services.AddScoped<IAuditWriter, OperatorMutationFailingAuditWriter>();
            });
            using HttpClient client = restored.CreateClient();
            using HttpResponseMessage response = await OperatorMutationTestSupport.SendDisableAsync(
                client,
                token,
                restoredTarget.Id,
                "opr-mut-aud01-restored");
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(1, restoredProbe.CurrentTransactionFailures);
        }

        Operator restoredPersisted = await OperatorMutationTestSupport.ReadOperatorAsync(
            Database.ConnectionString,
            restoredTarget.Id);
        Assert.Equal(OperatorState.Active, restoredPersisted.State);
        Assert.Equal(0L, await OperatorMutationTestSupport.CountAuditsAsync(
            Database.ConnectionString,
            "opr-mut-aud01-restored"));
    }

    private OperatorMutationApiFactory CreateFactory(Action<IServiceCollection> configureServices) =>
        new(Database.ConnectionString, configureServices);

    private AuthorizationProbeApiFactory CreateProbeFactory(Action<IServiceCollection>? configureServices = null) =>
        new(Database.ConnectionString, configureServices);

    private Task<Operator> SeedAsync(string userName, OperatorRole role) =>
        OperatorMutationTestSupport.SeedOperatorAsync(Database.ConnectionString, userName, role);

    private async Task RunAdminPairAsync(
        string namePrefix,
        Action<IServiceCollection>? configureServices,
        long expectedMinimumActiveAdministrators,
        string phase)
    {
        Operator first = await SeedAsync(namePrefix + ".a", OperatorRole.Administrator);
        Operator second = await SeedAsync(namePrefix + ".b", OperatorRole.Administrator);

        await using OperatorMutationApiFactory factory = new(Database.ConnectionString, configureServices);
        using HttpClient firstClient = factory.CreateClient();
        using HttpClient secondClient = factory.CreateClient();

        Task<HttpResponseMessage> firstTask = OperatorMutationTestSupport.SendDisableAsync(
            firstClient,
            OperatorMutationTestSupport.CreateToken(first),
            second.Id,
            namePrefix + "-first");
        Task<HttpResponseMessage> secondTask = OperatorMutationTestSupport.SendDisableAsync(
            secondClient,
            OperatorMutationTestSupport.CreateToken(second),
            first.Id,
            namePrefix + "-second");

        HttpResponseMessage[] responses = await Task.WhenAll(firstTask, secondTask);
        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }

        long activeAdministrators = await OperatorMutationTestSupport.CountActiveAdministratorsAsync(
            Database.ConnectionString);
        Assert.True(
            activeAdministrators >= expectedMinimumActiveAdministrators,
            $"{phase}: expected at least {expectedMinimumActiveAdministrators} active administrators, observed {activeAdministrators}.");
        if (expectedMinimumActiveAdministrators >= 1)
        {
            Assert.True(
                activeAdministrators >= 1,
                $"{phase}: production last-admin control unexpectedly allowed zero active administrators.");
        }
    }

    private static async Task<HttpResponseMessage> GetFallbackAsync(
        HttpClient client,
        string token,
        string targetId)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            AuthorizationProbeController.FallbackRoute.Replace("{targetId}", targetId, StringComparison.Ordinal));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}

internal sealed class LastActiveAdministratorBypassInvariant : ILastActiveAdministratorInvariant
{
    public bool WouldBeViolated(
        Operator target,
        OperatorMutationKind kind,
        OperatorRole? requestedRole,
        IReadOnlyList<Guid> lockedActiveAdministratorIdentifiers)
    {
        _ = target;
        _ = kind;
        _ = requestedRole;
        _ = lockedActiveAdministratorIdentifiers;
        return false;
    }
}

internal sealed class AuthorizationStateInvalidationBypassEffect : IOperatorMutationEffect
{
    public void Enable(Operator target, DateTimeOffset utcNow, string securityStamp) =>
        SetLifecycle(target, OperatorState.Active, target.Role, utcNow, securityStamp);

    public void Disable(Operator target, DateTimeOffset utcNow, string securityStamp) =>
        SetLifecycle(target, OperatorState.Disabled, target.Role, utcNow, securityStamp);

    public void ChangeRole(Operator target, OperatorRole role, DateTimeOffset utcNow, string securityStamp) =>
        SetLifecycle(target, target.State, role, utcNow, securityStamp);

    private static void SetLifecycle(
        Operator target,
        OperatorState state,
        OperatorRole role,
        DateTimeOffset utcNow,
        string securityStamp)
    {
        ArgumentNullException.ThrowIfNull(target);
        _ = securityStamp;
        typeof(Operator).GetProperty(nameof(Operator.State))!.SetValue(target, state);
        typeof(Operator).GetProperty(nameof(Operator.Role))!.SetValue(target, role);
        typeof(Operator).GetProperty(nameof(Operator.UpdatedAt))!.SetValue(target, utcNow.ToUniversalTime());
    }
}

internal sealed class NonAtomicOperatorMutationSuccessCommitter(
    BankDbContext persistence,
    IAuditWriter auditWriter) : IOperatorMutationSuccessCommitter
{
    public async Task CommitAsync(
        IDbContextTransaction transaction,
        AuditWriteRequest successAudit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(successAudit);

        await persistence.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        await auditWriter.AppendToCurrentTransactionAsync(successAudit, cancellationToken).ConfigureAwait(false);
    }
}
