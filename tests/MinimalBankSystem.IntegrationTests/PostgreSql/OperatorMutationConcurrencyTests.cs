extern alias api;

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using MinimalBankSystem.IntegrationTests.Authentication;
using MinimalBankSystem.IntegrationTests.OperatorMutation;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection(TestExecutionCollections.ConsoleSensitive)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class OperatorMutationConcurrencyTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string SeedPlaintextPassword = "operator-mutation-concurrency-seed-not-for-production";

    // The three cases below are the primary real-PostgreSQL concurrency proof for the
    // active-administrator invariant. Each seeds *exactly two* active administrators (A/B) and
    // has them mutate *each other* concurrently (cross-target), not a shared third target. A
    // common-target race only contends on one row's lock and does not exercise the ordered
    // FOR-UPDATE lock over the whole active-administrator set the way two administrators removing
    // each other does, so cross-target is required as the primary proof rather than a
    // same-target race.

    [Fact]
    public async Task ConcurrentCrossTargetDisableAndDisablePreservesActiveAdministratorInvariant()
    {
        await MigrateAsync();
        Operator adminA = await SeedOperatorAsync("opr-mut-concurrent.dd.admin-a", OperatorRole.Administrator);
        Operator adminB = await SeedOperatorAsync("opr-mut-concurrent.dd.admin-b", OperatorRole.Administrator);

        HttpResponseMessage[] responses = await RunConcurrentAsync(
            (client, correlationId) => SendMutationAsync(
                client,
                adminB.Id,
                "/disable",
                CreateToken(adminA),
                correlationId),
            (client, correlationId) => SendMutationAsync(
                client,
                adminA.Id,
                "/disable",
                CreateToken(adminB),
                correlationId));
        try
        {
            Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.True(
                await CountActiveAdministratorsAsync() >= 1,
                "Cross-target disable+disable must leave at least one active administrator.");
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
    public async Task ConcurrentCrossTargetDemotionAndDemotionPreservesActiveAdministratorInvariant()
    {
        await MigrateAsync();
        Operator adminA = await SeedOperatorAsync("opr-mut-concurrent.dm.admin-a", OperatorRole.Administrator);
        Operator adminB = await SeedOperatorAsync("opr-mut-concurrent.dm.admin-b", OperatorRole.Administrator);

        HttpResponseMessage[] responses = await RunConcurrentAsync(
            (client, correlationId) => SendRoleAsync(
                client,
                adminB.Id,
                CreateToken(adminA),
                correlationId),
            (client, correlationId) => SendRoleAsync(
                client,
                adminA.Id,
                CreateToken(adminB),
                correlationId));
        try
        {
            Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.True(
                await CountActiveAdministratorsAsync() >= 1,
                "Cross-target demotion+demotion must leave at least one active administrator.");
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
    public async Task ConcurrentCrossTargetDisableAndDemotionPreservesInvariantAndConvergesSafely()
    {
        await MigrateAsync();
        Operator adminA = await SeedOperatorAsync("opr-mut-concurrent.dmd.admin-a", OperatorRole.Administrator);
        Operator adminB = await SeedOperatorAsync("opr-mut-concurrent.dmd.admin-b", OperatorRole.Administrator);

        HttpResponseMessage[] responses = await RunConcurrentAsync(
            (client, correlationId) => SendMutationAsync(
                client,
                adminB.Id,
                "/disable",
                CreateToken(adminA),
                correlationId),
            (client, correlationId) => SendRoleAsync(
                client,
                adminA.Id,
                CreateToken(adminB),
                correlationId));
        try
        {
            Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.True(
                await CountActiveAdministratorsAsync() >= 1,
                "Cross-target disable+demotion must leave at least one active administrator.");
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
    public async Task LockTimeoutMapsToConflictWithoutRetryOrMutation()
    {
        await MigrateAsync();
        Operator actor = await SeedOperatorAsync("opr-mut-concurrent.timeout.actor", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("opr-mut-concurrent.timeout.target", OperatorRole.Administrator);
        string correlationId = "opr-mut-concurrent-lock-timeout";

        await using NpgsqlConnection holder = new(Database.ConnectionString);
        await holder.OpenAsync();
        await using NpgsqlTransaction heldTransaction = await holder.BeginTransactionAsync();
        await using (NpgsqlCommand lockCommand = new(
                         $"SELECT {OperatorPersistence.IdColumn} FROM {OperatorPersistence.TableName} WHERE {OperatorPersistence.IdColumn} = @operator_id FOR UPDATE;",
                         holder,
                         heldTransaction))
        {
            lockCommand.Parameters.AddWithValue("operator_id", target.Id);
            Assert.Equal(target.Id, await lockCommand.ExecuteScalarAsync());

            await using OperatorMutationApiFactory factory = new(Database.ConnectionString);
            using HttpClient client = factory.CreateClient();
            using HttpResponseMessage response = await SendMutationAsync(
                client,
                target.Id,
                "/disable",
                CreateToken(actor),
                correlationId);

            string body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains("concurrent_operation_conflict", body, StringComparison.Ordinal);
        }

        await heldTransaction.RollbackAsync();
        Assert.Equal(OperatorState.Active, (await ReadOperatorAsync(target.Id)).State);
        Assert.Equal(1, await CountAuditsAsync(correlationId));
    }

    private async Task<HttpResponseMessage[]> RunConcurrentAsync(
        Func<HttpClient, string, Task<HttpResponseMessage>> first,
        Func<HttpClient, string, Task<HttpResponseMessage>> second)
    {
        await using OperatorMutationApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        Task<HttpResponseMessage> firstRequest = first(client, "opr-mut-concurrent-first");
        Task<HttpResponseMessage> secondRequest = second(client, "opr-mut-concurrent-second");
        return await Task.WhenAll(firstRequest, secondRequest);
    }

    private async Task MigrateAsync()
    {
        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, TimeSpan.FromSeconds(120));
        Assert.Equal(MigratorExitCode.Success, run.ExitCode);
    }

    private async Task<Operator> SeedOperatorAsync(string userName, OperatorRole role)
    {
        Operator created = OperatorFactory.Create(
            userName,
            SeedPlaintextPassword,
            role,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        await using BankDbContext context = CreateContext();
        context.Operators.Add(created);
        await context.SaveChangesAsync();
        return created;
    }

    private async Task<Operator> ReadOperatorAsync(Guid identifier)
    {
        await using BankDbContext context = CreateContext();
        return await context.Operators.SingleAsync(candidate => candidate.Id == identifier);
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

    private async Task<long> CountAuditsAsync(string correlationId)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT count(*) FROM audit_records WHERE correlation_id = @correlation_id;",
            connection);
        command.Parameters.AddWithValue("correlation_id", correlationId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task<HttpResponseMessage> SendMutationAsync(
        HttpClient client,
        Guid targetIdentifier,
        string suffix,
        string token,
        string correlationId)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Post,
            $"/operators/{targetIdentifier:D}{suffix}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendRoleAsync(
        HttpClient client,
        Guid targetIdentifier,
        string token,
        string correlationId)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Post,
            $"/operators/{targetIdentifier:D}/role");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        request.Content = JsonContent.Create(new { role = "teller" });
        return await client.SendAsync(request);
    }

    private static string CreateToken(Operator operatorEntity)
    {
        DateTime now = DateTime.UtcNow;
        JwtSecurityToken token = new(
            "minimal-bank-system",
            "minimal-bank-system-api",
            [
                new Claim(JwtRegisteredClaimNames.Sub, operatorEntity.Id.ToString("D")),
                new Claim(
                    AuthnClaimTypes.AuthorizationStateVersion,
                    operatorEntity.AuthorizationStateVersion.ToString(CultureInfo.InvariantCulture)),
            ],
            now.AddMinutes(-1),
            now.AddMinutes(5),
            new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(TestJwtConfiguration.SigningKey)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
