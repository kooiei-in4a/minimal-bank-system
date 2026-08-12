using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// FND-06 readiness evidence against a real PostgreSQL server: connectivity alone is not
/// readiness, the canonical FND-04 migration history is the only migration authority, and probing
/// health never changes stored state.
/// </summary>
[Trait("Category", "PostgreSqlIntegration")]
public sealed class HealthReadinessTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const int SessionSettleAttempts = 20;

    private static readonly TimeSpan MigrationBudget = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan SessionSettleInterval = TimeSpan.FromMilliseconds(250);

    [Fact]
    public async Task ReadinessRejectsAReachableButUnmigratedDatabaseWithoutTouchingTheSchema()
    {
        Assert.Empty(await ReadPublicTablesAsync());

        await using HealthApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using (HttpResponseMessage live = await client.GetAsync(HealthContract.LivePath))
        {
            await HealthContractTests.AssertLiveAsync(live);
        }

        using (HttpResponseMessage ready = await client.GetAsync(HealthContract.ReadyPath))
        {
            await HealthContractTests.AssertNotReadyAsync(ready);
        }

        // AC-07 and the FND-04 no-auto-migration contract: probing readiness creates nothing,
        // not even the migration history table.
        Assert.Empty(await ReadPublicTablesAsync());
    }

    [Fact]
    public async Task ReadinessSucceedsOnlyAfterTheExplicitMigratorAppliedTheCanonicalMigration()
    {
        await using HealthApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using (HttpResponseMessage before = await client.GetAsync(HealthContract.ReadyPath))
        {
            await HealthContractTests.AssertNotReadyAsync(before);
        }

        MigratorRun migration = await MigratorProcess.RunAsync(
            Database.ConnectionString,
            MigrationBudget);
        Assert.Equal(MigratorExitCode.Success, migration.ExitCode);

        string[] tablesAfterMigration = await ReadPublicTablesAsync();
        string[] historyAfterMigration = await ReadMigrationHistoryAsync();
        Assert.Single(historyAfterMigration);

        // The already-running API observes the change without a restart.
        using (HttpResponseMessage after = await client.GetAsync(HealthContract.ReadyPath))
        {
            await HealthContractTests.AssertReadyAsync(after);
        }

        using (HttpResponseMessage live = await client.GetAsync(HealthContract.LivePath))
        {
            await HealthContractTests.AssertLiveAsync(live);
        }

        // AC-07: health probing is read-only.
        Assert.Equal(tablesAfterMigration, await ReadPublicTablesAsync());
        Assert.Equal(historyAfterMigration, await ReadMigrationHistoryAsync());
    }

    [Fact]
    public async Task ReadinessFailsAgainWhenTheAppliedMigrationRecordIsRemovedWhilePostgreSqlStaysUp()
    {
        MigratorRun migration = await MigratorProcess.RunAsync(
            Database.ConnectionString,
            MigrationBudget);
        Assert.Equal(MigratorExitCode.Success, migration.ExitCode);

        await using HealthApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using (HttpResponseMessage ready = await client.GetAsync(HealthContract.ReadyPath))
        {
            await HealthContractTests.AssertReadyAsync(ready);
        }

        await ExecuteNonQueryAsync(
            $"""
             DELETE FROM {BankPersistence.MigrationsHistorySchema}."{BankPersistence.MigrationsHistoryTableName}";
             """);

        // Connectivity is unchanged, so only the EF migration authority can reject this.
        using (HttpResponseMessage ready = await client.GetAsync(HealthContract.ReadyPath))
        {
            await HealthContractTests.AssertNotReadyAsync(ready);
        }

        using (HttpResponseMessage live = await client.GetAsync(HealthContract.LivePath))
        {
            await HealthContractTests.AssertLiveAsync(live);
        }
    }

    [Fact]
    public async Task RepeatedReadyProbesLeaveMigrationStateAndSessionCountUnchanged()
    {
        MigratorRun migration = await MigratorProcess.RunAsync(
            Database.ConnectionString,
            MigrationBudget);
        Assert.Equal(MigratorExitCode.Success, migration.ExitCode);

        string[] tables = await ReadPublicTablesAsync();
        string[] history = await ReadMigrationHistoryAsync();

        await using HealthApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        for (int probe = 0; probe < 5; probe++)
        {
            using HttpResponseMessage ready = await client.GetAsync(HealthContract.ReadyPath);
            await HealthContractTests.AssertReadyAsync(ready);
        }

        Assert.Equal(tables, await ReadPublicTablesAsync());
        Assert.Equal(history, await ReadMigrationHistoryAsync());

        // Health probing must not leave sessions behind on the readiness database. PostgreSQL
        // retires a backend asynchronously after the client disconnects, so this settles.
        Assert.Equal(0L, await WaitForOtherSessionsToSettleAsync());
    }

    private async Task<long> WaitForOtherSessionsToSettleAsync()
    {
        long observed = await CountOtherSessionsAsync();

        for (int attempt = 0; observed != 0 && attempt < SessionSettleAttempts; attempt++)
        {
            await Task.Delay(SessionSettleInterval);
            observed = await CountOtherSessionsAsync();
        }

        return observed;
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

    private async Task<long> CountOtherSessionsAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            """
            SELECT count(*)
            FROM pg_stat_activity
            WHERE datname = current_database() AND pid <> pg_backend_pid();
            """,
            connection);

        return Assert.IsType<long>(await command.ExecuteScalarAsync());
    }

    private async Task ExecuteNonQueryAsync(string commandText)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        await command.ExecuteNonQueryAsync();
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
}
