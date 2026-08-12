using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Real PostgreSQL proof that readiness is stronger than connectivity and that probing does not
/// evolve the FND-04 migration state.
/// </summary>
[Trait("Category", "PostgreSqlIntegration")]
public sealed class HealthReadinessTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly TimeSpan MigrationBudget = TimeSpan.FromSeconds(120);

    [Fact]
    public async Task ReadinessRejectsAReachableUnmigratedDatabaseWithoutCreatingSchema()
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

        // No marker table, migration history, or business state may be created by the API probe.
        Assert.Empty(await ReadPublicTablesAsync());
    }

    [Fact]
    public async Task ExplicitMigrationMakesTheAlreadyRunningApiReadyWithoutChangingDataDuringProbe()
    {
        await using HealthApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using (HttpResponseMessage beforeMigration = await client.GetAsync(HealthContract.ReadyPath))
        {
            await HealthContractTests.AssertNotReadyAsync(beforeMigration);
        }

        MigratorRun migration = await MigratorProcess.RunAsync(Database.ConnectionString, MigrationBudget);
        Assert.Equal(MigratorExitCode.Success, migration.ExitCode);

        string[] tablesBeforeProbe = await ReadPublicTablesAsync();
        string[] historyBeforeProbe = await ReadMigrationHistoryAsync();
        Assert.Single(historyBeforeProbe);

        using (HttpResponseMessage afterMigration = await client.GetAsync(HealthContract.ReadyPath))
        {
            await HealthContractTests.AssertReadyAsync(afterMigration);
        }

        Assert.Equal(tablesBeforeProbe, await ReadPublicTablesAsync());
        Assert.Equal(historyBeforeProbe, await ReadMigrationHistoryAsync());
    }

    [Fact]
    public async Task ReadinessFailsWhenMigrationHistoryBecomesIncompleteWhileConnectivityRemains()
    {
        MigratorRun migration = await MigratorProcess.RunAsync(Database.ConnectionString, MigrationBudget);
        Assert.Equal(MigratorExitCode.Success, migration.ExitCode);

        await using HealthApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using (HttpResponseMessage initiallyReady = await client.GetAsync(HealthContract.ReadyPath))
        {
            await HealthContractTests.AssertReadyAsync(initiallyReady);
        }

        await ExecuteNonQueryAsync(
            $"DELETE FROM {BankPersistence.MigrationsHistorySchema}.\"{BankPersistence.MigrationsHistoryTableName}\";");

        // PostgreSQL stays reachable; rejecting this state proves readiness consults EF metadata.
        using (HttpResponseMessage afterHistoryRemoval = await client.GetAsync(HealthContract.ReadyPath))
        {
            await HealthContractTests.AssertNotReadyAsync(afterHistoryRemoval);
        }

        using (HttpResponseMessage live = await client.GetAsync(HealthContract.LivePath))
        {
            await HealthContractTests.AssertLiveAsync(live);
        }
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
            $"SELECT \"MigrationId\" FROM {BankPersistence.MigrationsHistorySchema}.\"{BankPersistence.MigrationsHistoryTableName}\" ORDER BY \"MigrationId\";");

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
