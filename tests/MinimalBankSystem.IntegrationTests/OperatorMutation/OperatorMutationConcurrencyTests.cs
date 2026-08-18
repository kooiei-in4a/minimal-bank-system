extern alias api;

using System.Data;
using System.Net;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using MinimalBankSystem.IntegrationTests.PostgreSql;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.OperatorMutation;

[Collection(TestExecutionCollections.ConsoleSensitive)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class OperatorMutationConcurrencyTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task ConcurrentDisableDisablePreservesAtLeastOneActiveAdministrator()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator first = await SeedAsync("opr.mut.conc.disable.a", OperatorRole.Administrator);
        Operator second = await SeedAsync("opr.mut.conc.disable.b", OperatorRole.Administrator);

        ConcurrentMutationResult result = await RunConcurrentAsync(
            first,
            second,
            (client, actor, target, correlationId) =>
                OperatorMutationTestSupport.SendDisableAsync(
                    client,
                    OperatorMutationTestSupport.CreateToken(actor),
                    target.Id,
                    correlationId),
            "opr-mut-conc-disable-disable");

        await AssertLastAdminInvariantHeldAsync(result);
    }

    [Fact]
    public async Task ConcurrentDemotionDemotionPreservesAtLeastOneActiveAdministrator()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator first = await SeedAsync("opr.mut.conc.demote.a", OperatorRole.Administrator);
        Operator second = await SeedAsync("opr.mut.conc.demote.b", OperatorRole.Administrator);

        ConcurrentMutationResult result = await RunConcurrentAsync(
            first,
            second,
            (client, actor, target, correlationId) =>
                OperatorMutationTestSupport.SendRoleChangeAsync(
                    client,
                    OperatorMutationTestSupport.CreateToken(actor),
                    target.Id,
                    correlationId,
                    "teller"),
            "opr-mut-conc-demote-demote");

        await AssertLastAdminInvariantHeldAsync(result);
    }

    [Fact]
    public async Task ConcurrentDisableAndDemotionPreservesAtLeastOneActiveAdministrator()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator first = await SeedAsync("opr.mut.conc.mix.a", OperatorRole.Administrator);
        Operator second = await SeedAsync("opr.mut.conc.mix.b", OperatorRole.Administrator);

        await using OperatorMutationApiFactory factory = new(Database.ConnectionString);
        using HttpClient firstClient = factory.CreateClient();
        using HttpClient secondClient = factory.CreateClient();

        Task<HttpResponseMessage> disableTask = OperatorMutationTestSupport.SendDisableAsync(
            firstClient,
            OperatorMutationTestSupport.CreateToken(first),
            second.Id,
            "opr-mut-conc-mix-disable");
        Task<HttpResponseMessage> demoteTask = OperatorMutationTestSupport.SendRoleChangeAsync(
            secondClient,
            OperatorMutationTestSupport.CreateToken(second),
            first.Id,
            "opr-mut-conc-mix-demote",
            "viewer");

        HttpResponseMessage[] responses = await Task.WhenAll(disableTask, demoteTask);
        using HttpResponseMessage disableResponse = responses[0];
        using HttpResponseMessage demoteResponse = responses[1];

        await AssertLastAdminInvariantHeldAsync(
            new ConcurrentMutationResult(disableResponse.StatusCode, demoteResponse.StatusCode));
    }

    [Fact]
    public async Task RealPostgreSqlLockTimeoutMapsToConcurrentOperationConflict()
    {
        await OperatorMutationTestSupport.MigrateAsync(Database.ConnectionString);
        Operator administrator = await SeedAsync("opr.mut.locktimeout.admin", OperatorRole.Administrator);
        Operator spareAdmin = await SeedAsync("opr.mut.locktimeout.spare", OperatorRole.Administrator);
        Operator target = await SeedAsync("opr.mut.locktimeout.target", OperatorRole.Teller);

        await using NpgsqlConnection holder = new(Database.ConnectionString);
        await holder.OpenAsync();
        await using NpgsqlTransaction heldTransaction = await holder.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await using (NpgsqlCommand timeout = new("SET LOCAL lock_timeout = '10s'", holder, heldTransaction))
        {
            await timeout.ExecuteNonQueryAsync();
        }

        await using (NpgsqlCommand lockAdmins = new(
                           OperatorMutationLocking.LockActiveAdministratorsSql,
                           holder,
                           heldTransaction))
        {
            await lockAdmins.ExecuteNonQueryAsync();
        }

        try
        {
            await using OperatorMutationApiFactory factory = new(Database.ConnectionString);
            using HttpClient client = factory.CreateClient();
            using HttpResponseMessage response = await OperatorMutationTestSupport.SendDisableAsync(
                client,
                OperatorMutationTestSupport.CreateToken(administrator),
                target.Id,
                "opr-mut-real-lock-timeout");

            await OperatorMutationTestSupport.AssertErrorAsync(
                response,
                HttpStatusCode.Conflict,
                "concurrent_operation_conflict");

            PersistedOperatorMutationAudit audit = Assert.Single(
                await OperatorMutationTestSupport.ReadAuditsAsync(
                    Database.ConnectionString,
                    "opr-mut-real-lock-timeout"));
            Assert.Equal("operator.command.disable", audit.OperationIdentifier);
            Assert.Equal(target.Id.ToString("D"), audit.TargetIdentifier);
            Assert.Equal("concurrent_operation_conflict", audit.FailureBusinessErrorCode);

            Operator persisted = await OperatorMutationTestSupport.ReadOperatorAsync(
                Database.ConnectionString,
                target.Id);
            Assert.Equal(OperatorState.Active, persisted.State);
            Assert.Equal(target.AuthorizationStateVersion, persisted.AuthorizationStateVersion);
            Assert.Equal(target.SecurityStamp, persisted.SecurityStamp);
            Assert.Equal(
                2L,
                await OperatorMutationTestSupport.CountActiveAdministratorsAsync(Database.ConnectionString));
            Assert.Equal(OperatorState.Active, spareAdmin.State);
        }
        finally
        {
            await heldTransaction.RollbackAsync();
        }
    }

    private Task<Operator> SeedAsync(string userName, OperatorRole role) =>
        OperatorMutationTestSupport.SeedOperatorAsync(Database.ConnectionString, userName, role);

    private async Task<ConcurrentMutationResult> RunConcurrentAsync(
        Operator first,
        Operator second,
        Func<HttpClient, Operator, Operator, string, Task<HttpResponseMessage>> send,
        string correlationPrefix)
    {
        await using OperatorMutationApiFactory factory = new(Database.ConnectionString);
        using HttpClient firstClient = factory.CreateClient();
        using HttpClient secondClient = factory.CreateClient();

        Task<HttpResponseMessage> firstTask = send(
            firstClient,
            first,
            second,
            correlationPrefix + "-first");
        Task<HttpResponseMessage> secondTask = send(
            secondClient,
            second,
            first,
            correlationPrefix + "-second");

        HttpResponseMessage[] responses = await Task.WhenAll(firstTask, secondTask);
        using HttpResponseMessage firstResponse = responses[0];
        using HttpResponseMessage secondResponse = responses[1];
        return new ConcurrentMutationResult(firstResponse.StatusCode, secondResponse.StatusCode);
    }

    private async Task AssertLastAdminInvariantHeldAsync(ConcurrentMutationResult result)
    {
        long activeAdministrators = await OperatorMutationTestSupport.CountActiveAdministratorsAsync(
            Database.ConnectionString);
        Assert.True(
            activeAdministrators >= 1,
            $"Concurrent Operator mutations left {activeAdministrators} active administrators.");
        Assert.True(
            result.SuccessCount <= 1,
            $"Concurrent Operator mutations produced {result.SuccessCount} successes, which would allow the last-admin invariant to be lost.");
        Assert.True(
            result.FirstStatus is HttpStatusCode.OK or HttpStatusCode.Conflict,
            $"Unexpected first status {result.FirstStatus}.");
        Assert.True(
            result.SecondStatus is HttpStatusCode.OK or HttpStatusCode.Conflict,
            $"Unexpected second status {result.SecondStatus}.");
    }

    private sealed record ConcurrentMutationResult(HttpStatusCode FirstStatus, HttpStatusCode SecondStatus)
    {
        public int SuccessCount =>
            (FirstStatus == HttpStatusCode.OK ? 1 : 0) + (SecondStatus == HttpStatusCode.OK ? 1 : 0);
    }
}
