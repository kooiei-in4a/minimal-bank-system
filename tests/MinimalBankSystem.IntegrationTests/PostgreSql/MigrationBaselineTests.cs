extern alias api;

using System.Data.Common;
using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Auditing;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>Real PostgreSQL evidence for the FND-04 migration machinery.</summary>
[Trait("Category", "PostgreSqlIntegration")]
public sealed class MigrationBaselineTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string SecretSentinel = "FND04_REJECTED_PASSWORD_SENTINEL_8C9314F2";
    private const string FoundationMigration = "20260809113338_InitialFoundation";

    private static readonly string[] ExpectedLatestPublicTables =
    [
        BankPersistence.MigrationsHistoryTableName,
        AuditPersistence.TableName,
        OperatorPersistence.TableName,
    ];

    private static readonly TimeSpan NormalRunBudget = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan BlockedRunBudget = TimeSpan.FromSeconds(180);

    [Fact]
    public async Task ExplicitMigratorAppliesTheBaselineToACleanDatabase()
    {
        Assert.Empty(await ReadPublicTablesAsync());
        Assert.False(await MigrationHistoryExistsAsync());

        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, NormalRunBudget);

        Assert.True(
            run.ExitCode == MigratorExitCode.Success,
            $"Expected success, got exit code {run.ExitCode}. Output:\n{run.Output}");

        Assert.Equal(
            ["20260809113338_InitialFoundation", OperatorPersistence.IdentityMigrationId, AuditPersistence.AuditMigrationId],
            await ReadMigrationHistoryAsync());
        Assert.Equal(ExpectedLatestPublicTables, await ReadPublicTablesAsync());
    }

    [Fact]
    public async Task ImmediatelyPreviousOperatorSchemaUpgradesWithoutLosingRepresentativeRows()
    {
        await using BankDbContext context = CreateContext();
        IMigrator migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(OperatorPersistence.IdentityMigrationId);

        Operator representative = OperatorFactory.Create(
            "audit.migration.viewer",
            "AUDIT_MIGRATION_REPRESENTATIVE_PASSWORD_NOT_A_CREDENTIAL",
            OperatorRole.Viewer,
            new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero),
            "audit-migration-security-stamp");
        context.Operators.Add(representative);
        await context.SaveChangesAsync();

        Assert.Equal(
            ["20260809113338_InitialFoundation", OperatorPersistence.IdentityMigrationId],
            await ReadMigrationHistoryAsync());
        Assert.Equal(
            [BankPersistence.MigrationsHistoryTableName, OperatorPersistence.TableName],
            await ReadPublicTablesAsync());

        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, NormalRunBudget);
        Assert.True(
            run.ExitCode == MigratorExitCode.Success,
            $"Expected success, got exit code {run.ExitCode}. Output:\n{run.Output}");

        Assert.Equal(
            ["20260809113338_InitialFoundation", OperatorPersistence.IdentityMigrationId, AuditPersistence.AuditMigrationId],
            await ReadMigrationHistoryAsync());
        Assert.Equal(ExpectedLatestPublicTables, await ReadPublicTablesAsync());

        await using BankDbContext readContext = CreateContext();
        Operator stored = await readContext.Operators.AsNoTracking().SingleAsync();
        Assert.Equal(representative.Id, stored.Id);
        Assert.Equal(representative.UserName, stored.UserName);
        Assert.Equal(representative.Role, stored.Role);
    }

    [Fact]
    public async Task RunningTheMigratorTwiceLeavesTheHistoryUnchanged()
    {
        MigratorRun first = await MigratorProcess.RunAsync(Database.ConnectionString, NormalRunBudget);
        Assert.Equal(MigratorExitCode.Success, first.ExitCode);
        string[] afterFirst = await ReadMigrationHistoryAsync();

        MigratorRun second = await MigratorProcess.RunAsync(Database.ConnectionString, NormalRunBudget);

        Assert.True(
            second.ExitCode == MigratorExitCode.Success,
            $"A no-op migration must succeed. Output:\n{second.Output}");
        Assert.Equal(afterFirst, await ReadMigrationHistoryAsync());
        Assert.Equal(ExpectedLatestPublicTables, await ReadPublicTablesAsync());
    }

    [Fact]
    public async Task MigratorExitsNonZeroWhenNoConnectionStringIsConfigured()
    {
        MigratorRun run = await MigratorProcess.RunAsync(connectionString: null, NormalRunBudget);

        Assert.NotEqual(MigratorExitCode.Success, run.ExitCode);
        Assert.Empty(await ReadPublicTablesAsync());
    }

    [Fact]
    public async Task MigratorExitsNonZeroWhenTheServerIsUnreachable()
    {
        NpgsqlConnectionStringBuilder unreachable = new(Database.ConnectionString)
        {
            Port = 1,
            Timeout = 5,
        };

        MigratorRun run = await MigratorProcess.RunAsync(unreachable.ConnectionString, NormalRunBudget);

        Assert.NotEqual(MigratorExitCode.Success, run.ExitCode);
        Assert.Empty(await ReadPublicTablesAsync());
    }

    [Fact]
    public async Task MigratorExitsNonZeroWhenCredentialsAreRejectedWithoutDisclosingThePassword()
    {
        NpgsqlConnectionStringBuilder rejected = new(Database.ConnectionString)
        {
            Password = SecretSentinel,
        };

        MigratorRun run = await MigratorProcess.RunAsync(rejected.ConnectionString, NormalRunBudget);

        Assert.NotEqual(MigratorExitCode.Success, run.ExitCode);
        Assert.DoesNotContain(SecretSentinel, run.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretSentinel, run.StandardError, StringComparison.Ordinal);
        Assert.Empty(await ReadPublicTablesAsync());
    }

    [Fact]
    public async Task MigratorExitsNonZeroWhenMigrationHistoryIsMalformed()
    {
        await ExecuteNonQueryAsync(
            $"""
             CREATE TABLE {BankPersistence.MigrationsHistorySchema}."{BankPersistence.MigrationsHistoryTableName}" (
                 "MigrationId" character varying(150) NOT NULL,
                 CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
             );
             """);

        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, NormalRunBudget);

        Assert.NotEqual(MigratorExitCode.Success, run.ExitCode);
        Assert.Empty(await ReadMigrationHistoryIdsFromMalformedTableAsync());
    }

    [Fact]
    public async Task MigrationExecutionStopsAtTheFixedBudgetInsteadOfHanging()
    {
        await using NpgsqlConnection blocker = await PostgreSqlContainerFixture.OpenConnectionAsync(
            Database.ConnectionString,
            "holding the migration history lock");
        await using NpgsqlTransaction blocking = await blocker.BeginTransactionAsync();
        await using (NpgsqlCommand create = new(
            $"""
             CREATE TABLE {BankPersistence.MigrationsHistorySchema}."{BankPersistence.MigrationsHistoryTableName}" (
                 "MigrationId" character varying(150) NOT NULL,
                 "ProductVersion" character varying(32) NOT NULL,
                 CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
             );
             """,
            blocker,
            blocking))
        {
            await create.ExecuteNonQueryAsync();
        }

        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, BlockedRunBudget);

        await blocking.RollbackAsync();

        Assert.Equal(MigratorExitCode.Timeout, run.ExitCode);
        Assert.InRange(
            run.Duration,
            BankPersistence.MigrationTimeout - TimeSpan.FromSeconds(10),
            BankPersistence.MigrationTimeout + TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task NormalApiStartupNeverCreatesOrChangesSchema()
    {
        Assert.Empty(await ReadPublicTablesAsync());

        await StartApiAndIssueRequestAsync();

        Assert.Empty(await ReadPublicTablesAsync());
        Assert.False(await MigrationHistoryExistsAsync());

        MigratorRun migration = await MigratorProcess.RunAsync(Database.ConnectionString, NormalRunBudget);
        Assert.Equal(MigratorExitCode.Success, migration.ExitCode);

        string[] historyAfterMigration = await ReadMigrationHistoryAsync();
        string[] tablesAfterMigration = await ReadPublicTablesAsync();
        Assert.Equal(3, historyAfterMigration.Length);
        Assert.Equal(ExpectedLatestPublicTables, tablesAfterMigration);

        await StartApiAndIssueRequestAsync();

        Assert.Equal(historyAfterMigration, await ReadMigrationHistoryAsync());
        Assert.Equal(tablesAfterMigration, await ReadPublicTablesAsync());
    }

    [Fact]
    public async Task ApiResolvesTheSamePostgreSqlContextAsTheMigratorWithoutTouchingTheSchema()
    {
        await using MigrationApiFactory factory = new(Database.ConnectionString);
        using (HttpClient client = factory.CreateClient())
        {
            using HttpResponseMessage response = await client.GetAsync(new Uri("/", UriKind.Relative));
            Assert.NotNull(response);
        }

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            BankDbContext context = scope.ServiceProvider.GetRequiredService<BankDbContext>();

            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
            Assert.Equal(typeof(BankDbContext).Assembly, context.GetService<IMigrationsAssembly>().Assembly);
            Assert.Equal(Database.ConnectionString, context.Database.GetConnectionString());
        }

        Assert.Empty(await ReadPublicTablesAsync());
    }

    [Fact]
    public async Task AuditDownSucceedsWhileHistoryIsEmptyAndPreservesThePriorContract()
    {
        await using BankDbContext context = CreateContext();
        IMigrator migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync();

        Operator representative = OperatorFactory.Create(
            "audit.down.viewer",
            "AUDIT_DOWN_REPRESENTATIVE_PASSWORD_NOT_A_CREDENTIAL",
            OperatorRole.Viewer,
            new DateTimeOffset(2026, 8, 15, 1, 0, 0, TimeSpan.Zero),
            "audit-down-security-stamp");
        context.Operators.Add(representative);
        await context.SaveChangesAsync();

        await migrator.MigrateAsync(OperatorPersistence.IdentityMigrationId);

        Assert.Equal(
            ["20260809113338_InitialFoundation", OperatorPersistence.IdentityMigrationId],
            await ReadMigrationHistoryAsync());
        Assert.Equal(
            [BankPersistence.MigrationsHistoryTableName, OperatorPersistence.TableName],
            await ReadPublicTablesAsync());
        Assert.Equal(representative.Id, (await context.Operators.AsNoTracking().SingleAsync()).Id);
    }

    [Fact]
    public async Task AuditDownWithHistoryFailsAtTheApprovedBackupRestoreBoundary()
    {
        await using BankDbContext context = CreateContext();
        IMigrator migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync();

        context.AuditRecords.Add(AuditRecord.Create(
            Guid.CreateVersion7(),
            OperatorRole.Administrator,
            "verification.audit.rollback-boundary",
            "rollback-target",
            AuditResult.Success,
            failureBusinessErrorCode: null,
            "correlation-audit-rollback",
            new DateTimeOffset(2026, 8, 15, 2, 0, 0, TimeSpan.Zero)));
        await context.SaveChangesAsync();

        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(() =>
            migrator.MigrateAsync(OperatorPersistence.IdentityMigrationId));

        Assert.Contains(
            AuditPersistence.RollbackRequiresRestoreMarker,
            exception.MessageText,
            StringComparison.Ordinal);
        Assert.Equal(3, (await ReadMigrationHistoryAsync()).Length);
        Assert.Equal(ExpectedLatestPublicTables, await ReadPublicTablesAsync());
        Assert.Equal(1L, await ReadLongAsync($"SELECT count(*) FROM {AuditPersistence.TableName};"));
    }

    [Fact]
    public async Task AuditDownBlocksTheRuntimePrincipalBeforeItsEmptyCheckUntilTheDestructiveTransitionCompletes()
    {
        const long advisoryLockKey = 7_463_021;
        const string gateFunction = "audit_down_concurrency_gate";
        const string gateTrigger = "audit_down_concurrency_trigger";

        await using AuditRuntimeBoundary boundary = await AuditRuntimeBoundary.CreateAsync(Database.ConnectionString);
        await boundary.MigrateAsync();
        await ExecuteNonQueryAsync(
            $"""
             CREATE FUNCTION public.{gateFunction}()
             RETURNS event_trigger
             LANGUAGE plpgsql
             AS $audit_down_concurrency_gate$
             BEGIN
                 PERFORM pg_advisory_xact_lock({advisoryLockKey});
             END;
             $audit_down_concurrency_gate$;

             CREATE EVENT TRIGGER {gateTrigger}
             ON ddl_command_start
             WHEN TAG IN ('DROP TABLE')
             EXECUTE FUNCTION public.{gateFunction}();
             """);

        await using NpgsqlConnection gateConnection = new(Database.ConnectionString);
        await gateConnection.OpenAsync();
        await using NpgsqlTransaction gateTransaction = await gateConnection.BeginTransactionAsync();
        await using (NpgsqlCommand acquireGate = new(
                         $"SELECT pg_advisory_xact_lock({advisoryLockKey});",
                         gateConnection,
                         gateTransaction))
        {
            await acquireGate.ExecuteNonQueryAsync();
        }

        bool gateReleased = false;

        try
        {
            await using BankDbContext migratorContext = CreateContext(boundary.MigratorConnectionString);
            await migratorContext.Database.OpenConnectionAsync();
            int migratorBackendPid = await ReadBackendPidAsync(migratorContext.Database.GetDbConnection());
            IMigrator migrator = migratorContext.GetService<IMigrator>();
            Task downTask = migrator.MigrateAsync(OperatorPersistence.IdentityMigrationId);

            await WaitForAsync(
                () => MigrationIsHoldingTheAuditLockAndWaitingAtTheDropGateAsync(migratorBackendPid),
                "The EF/Npgsql Down execution did not retain ACCESS EXCLUSIVE on audit_records through DROP TABLE.");

            NpgsqlConnectionStringBuilder runtimeConnection = new(boundary.RuntimeConnectionString)
            {
                ApplicationName = "audit-down-runtime-writer",
            };
            await using NpgsqlConnection runtimeWriter = new(runtimeConnection.ConnectionString);
            await runtimeWriter.OpenAsync();
            int runtimeBackendPid = await ReadBackendPidAsync(runtimeWriter);
            await using NpgsqlCommand runtimeInsert = new(
                """
                INSERT INTO public.audit_records (
                    audit_id, actor_identifier, actor_role, operation_identifier, target_identifier,
                    result, failure_business_error_code, correlation_id, audit_time
                ) VALUES (
                    '01821815-0000-7000-8000-000000000001',
                    '01821815-0000-7000-8000-000000000002',
                    'administrator',
                    'verification.audit.down-concurrency',
                    'audit-down-concurrency-target',
                    'success',
                    NULL,
                    'audit-down-concurrency-correlation',
                    TIMESTAMPTZ '2026-08-15 03:00:00+00'
                );
                """,
                runtimeWriter);
            Task<int> runtimeInsertTask = runtimeInsert.ExecuteNonQueryAsync();

            await WaitForAsync(
                () => RuntimeWriterIsWaitingForTheAuditTableLockAsync(runtimeBackendPid),
                "The runtime-principal INSERT was not waiting behind the migration-held audit_records lock.");
            Assert.False(runtimeInsertTask.IsCompleted);

            await gateTransaction.CommitAsync();
            gateReleased = true;
            await downTask;

            PostgresException writerFailure = await Assert.ThrowsAsync<PostgresException>(
                async () => await runtimeInsertTask);
            Assert.Equal(PostgresErrorCodes.UndefinedTable, writerFailure.SqlState);

            Assert.Equal(
                [FoundationMigration, OperatorPersistence.IdentityMigrationId],
                await ReadMigrationHistoryAsync());
            Assert.Equal(
                [BankPersistence.MigrationsHistoryTableName, OperatorPersistence.TableName],
                await ReadPublicTablesAsync());
        }
        finally
        {
            if (!gateReleased)
            {
                await gateTransaction.RollbackAsync();
            }

            await ExecuteNonQueryAsync(
                $"""
                 DROP EVENT TRIGGER IF EXISTS {gateTrigger};
                 DROP FUNCTION IF EXISTS public.{gateFunction}();
                 """);
        }
    }

    private async Task StartApiAndIssueRequestAsync()
    {
        await using MigrationApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/", UriKind.Relative));
        Assert.NotNull(response);
    }

    private Task<string[]> ReadPublicTablesAsync() =>
        ReadStringsAsync(
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
            ORDER BY table_name;
            """);

    private Task<string[]> ReadMigrationHistoryAsync() =>
        ReadStringsAsync(
            $"""
             SELECT "MigrationId"
             FROM {BankPersistence.MigrationsHistorySchema}."{BankPersistence.MigrationsHistoryTableName}"
             ORDER BY "MigrationId";
             """);

    private Task<string[]> ReadMigrationHistoryIdsFromMalformedTableAsync() =>
        ReadMigrationHistoryAsync();

    private async Task<bool> MigrationHistoryExistsAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"SELECT to_regclass('{BankPersistence.MigrationsHistorySchema}.\"{BankPersistence.MigrationsHistoryTableName}\"') IS NOT NULL;",
            connection);

        return Assert.IsType<bool>(await command.ExecuteScalarAsync());
    }

    private async Task ExecuteNonQueryAsync(string commandText)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<bool> MigrationIsHoldingTheAuditLockAndWaitingAtTheDropGateAsync(int backendPid)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"""
             SELECT
                 EXISTS (
                     SELECT 1
                     FROM pg_locks
                     WHERE pid = {backendPid}
                       AND relation = 'public.audit_records'::regclass
                       AND mode = 'AccessExclusiveLock'
                       AND granted)
                 AND EXISTS (
                     SELECT 1
                     FROM pg_stat_activity
                     WHERE pid = {backendPid}
                       AND wait_event_type = 'Lock'
                       AND wait_event = 'advisory');
             """,
            connection);

        return Assert.IsType<bool>(await command.ExecuteScalarAsync());
    }

    private async Task<bool> RuntimeWriterIsWaitingForTheAuditTableLockAsync(int backendPid)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"""
             SELECT
                 EXISTS (
                     SELECT 1
                     FROM pg_locks
                     WHERE pid = {backendPid}
                       AND relation = 'public.audit_records'::regclass
                       AND NOT granted)
                 AND EXISTS (
                     SELECT 1
                     FROM pg_stat_activity
                     WHERE pid = {backendPid}
                       AND wait_event_type = 'Lock');
             """,
            connection);

        return Assert.IsType<bool>(await command.ExecuteScalarAsync());
    }

    private static async Task<int> ReadBackendPidAsync(DbConnection connection)
    {
        await using NpgsqlCommand command = new("SELECT pg_backend_pid();", (NpgsqlConnection)connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private async Task<long> ReadLongAsync(string commandText)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        return Assert.IsType<long>(await command.ExecuteScalarAsync());
    }

    private async Task<string[]> ReadStringsAsync(string commandText)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        List<string> values = [];

        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return [.. values];
    }

    private static async Task WaitForAsync(Func<Task<bool>> condition, string failureMessage)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));

        while (!timeout.IsCancellationRequested)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }

        throw new Xunit.Sdk.XunitException(failureMessage);
    }

    private BankDbContext CreateContext(string? connectionString = null)
    {
        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(
            connectionString ?? Database.ConnectionString,
            BankPersistence.MigrationTimeoutSeconds);
        return new BankDbContext(options.Options);
    }

    private sealed class MigrationApiFactory(string connectionString) : WebApplicationFactory<api::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting(JwtAuthnOptions.SigningKeyConfigurationKey, TestJwtConfiguration.SigningKey);
            builder.UseSetting(
                $"ConnectionStrings:{BankPersistence.ConnectionStringName}",
                connectionString);
        }
    }
}
