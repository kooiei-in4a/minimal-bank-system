using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.IntegrationTests.PostgreSql;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.OperatorCreate;

/// <summary>
/// OPR-CREATE-AUD-01. The mutation commits Operator creation before the required success Audit,
/// proving the approved same-transaction atomicity control is actually load-bearing.
/// </summary>
[Trait("Category", "PostgreSqlIntegration")]
public sealed class OperatorCreateCriticalMutationTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset FrozenUtcNow =
        new(2031, 8, 9, 10, 11, 12, TimeSpan.Zero);

    private const string AdministratorPassword = "opr-create-aud-01-admin-password";
    private const string LoginIdentifier = "opr.create.aud01.target";
    private const string CorrelationId = "opr-create-aud-01";

    [Fact]
    public async Task OprCreateAud01SameTransactionBypassIsKilledAndRestored()
    {
        await MigrateAsync();
        Operator administrator = await SeedAdministratorAsync();
        string token = OperatorCreateTestAuthentication.CreateToken(administrator);

        OperatorCreateAuditFailureProbe baselineProbe = new();
        await using (OperatorCreateApiFactory baseline = CreateFailingWriterFactory(baselineProbe))
        using (HttpClient baselineClient = baseline.CreateClient())
        {
            using HttpResponseMessage response = await OperatorCreateTestAuthentication.SendCreateAsync(
                baselineClient,
                CreatePayload(),
                token,
                CorrelationId);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(0L, await CountOperatorsAsync());
            Assert.Equal(0L, await CountAuditsAsync());
            Assert.Equal(1, baselineProbe.InvocationCount);
        }

        OperatorCreateAuditFailureProbe mutatedProbe = new();
        await using (OperatorCreateApiFactory mutated = CreateMutatedFactory(mutatedProbe))
        using (HttpClient mutatedClient = mutated.CreateClient())
        {
            using HttpResponseMessage response = await OperatorCreateTestAuthentication.SendCreateAsync(
                mutatedClient,
                CreatePayload(),
                token,
                CorrelationId);
            string body = await response.Content.ReadAsStringAsync();

            long remainingOperators = await CountOperatorsAsync();
            Assert.True(
                remainingOperators == 1,
                "OPR-CREATE-AUD-01: expected Operator creation to remain after required success-Audit failure, proving same-transaction atomicity was lost. "
                + $"Operators={remainingOperators}; Status={response.StatusCode}; Body={body}");
            Assert.Equal(0L, await CountAuditsAsync());
            Assert.DoesNotContain("operatorIdentifier", body, StringComparison.Ordinal);
            Assert.Equal(1, mutatedProbe.InvocationCount);
        }

        await DeleteCreatedOperatorsAsync();

        OperatorCreateAuditFailureProbe restoredProbe = new();
        await using (OperatorCreateApiFactory restored = CreateFailingWriterFactory(restoredProbe))
        using (HttpClient restoredClient = restored.CreateClient())
        {
            using HttpResponseMessage response = await OperatorCreateTestAuthentication.SendCreateAsync(
                restoredClient,
                CreatePayload(),
                token,
                CorrelationId);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(0L, await CountOperatorsAsync());
            Assert.Equal(0L, await CountAuditsAsync());
            Assert.Equal(1, restoredProbe.InvocationCount);
        }
    }

    private OperatorCreateApiFactory CreateFailingWriterFactory(OperatorCreateAuditFailureProbe probe) =>
        new(
            Database.ConnectionString,
            services =>
            {
                services.AddSingleton(probe);
                OperatorCreateTestAuthentication.ReplaceAuditWriter<FailingOperatorCreateAuditWriter>(services);
            });

    private OperatorCreateApiFactory CreateMutatedFactory(OperatorCreateAuditFailureProbe probe) =>
        new(
            Database.ConnectionString,
            services =>
            {
                services.AddSingleton(probe);
                OperatorCreateTestAuthentication.ReplaceAuditWriter<CommitThenFailCreateAuditWriter>(services);
            });

    private async Task MigrateAsync()
    {
        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, TimeSpan.FromSeconds(120));
        Assert.True(run.ExitCode == MigratorExitCode.Success, $"Migration failed. Output:{Environment.NewLine}{run.Output}");
    }

    private async Task<Operator> SeedAdministratorAsync()
    {
        Operator created = OperatorFactory.Create(
            "opr.create.aud01.admin",
            AdministratorPassword,
            OperatorRole.Administrator,
            FrozenUtcNow,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        await using BankDbContext context = new(options.Options);
        context.Operators.Add(created);
        await context.SaveChangesAsync();
        return created;
    }

    private async Task<long> CountOperatorsAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"""
             SELECT count(*) FROM {OperatorPersistence.TableName}
             WHERE {OperatorPersistence.NormalizedUserNameColumn} = @normalized;
             """,
            connection);
        command.Parameters.AddWithValue("normalized", LoginIdentifier.ToUpperInvariant());
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private async Task<long> CountAuditsAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT count(*) FROM audit_records WHERE correlation_id = @correlation_id;",
            connection);
        command.Parameters.AddWithValue("correlation_id", CorrelationId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private async Task DeleteCreatedOperatorsAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"""
             DELETE FROM {OperatorPersistence.TableName}
             WHERE {OperatorPersistence.NormalizedUserNameColumn} = @normalized;
             """,
            connection);
        command.Parameters.AddWithValue("normalized", LoginIdentifier.ToUpperInvariant());
        await command.ExecuteNonQueryAsync();
    }

    private static object CreatePayload() =>
        new
        {
            loginIdentifier = LoginIdentifier,
            password = "aud-01-password",
            role = "teller",
        };
}
