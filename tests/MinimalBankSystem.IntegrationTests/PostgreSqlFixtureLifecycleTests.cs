using Npgsql;

namespace MinimalBankSystem.IntegrationTests;

[Collection(TestExecutionCollections.PostgreSqlIntegration)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class PostgreSqlFixtureLifecycleTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlIntegrationTestBase(fixture)
{
    [Fact]
    public async Task PinnedPostgreSql18ContainerProvidesAnIsolatedDatabaseForTheTest()
    {
        Assert.Equal(
            "postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a",
            PostgreSqlContainerFixture.ImageReference);

        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();

        await using NpgsqlCommand versionCommand = new("SHOW server_version", connection);
        string version = (string)(await versionCommand.ExecuteScalarAsync() ?? string.Empty);
        await using NpgsqlCommand databaseCommand = new("SELECT current_database()", connection);
        string databaseName = (string)(await databaseCommand.ExecuteScalarAsync() ?? string.Empty);

        Assert.StartsWith("18.", version, StringComparison.Ordinal);
        Assert.Equal(Database.Name, databaseName);
    }

    [Fact]
    public async Task DatabaseLeaseCleanupDropsTheDatabase()
    {
        PostgreSqlTestDatabase temporaryDatabase = await Fixture.CreateDatabaseAsync();

        await using (NpgsqlConnection connection = new(temporaryDatabase.ConnectionString))
        {
            await connection.OpenAsync();
            await using NpgsqlCommand command = new("CREATE TABLE lifecycle_probe (id integer PRIMARY KEY)", connection);
            _ = await command.ExecuteNonQueryAsync();
        }

        await temporaryDatabase.DisposeAsync();

        Assert.False(await Fixture.DatabaseExistsAsync(temporaryDatabase.Name));
    }

    [Fact]
    public async Task SeparateDatabasesDoNotShareSchemaState()
    {
        await using PostgreSqlTestDatabase secondDatabase = await Fixture.CreateDatabaseAsync();

        await using (NpgsqlConnection firstConnection = new(Database.ConnectionString))
        {
            await firstConnection.OpenAsync();
            await using NpgsqlCommand createTable = new("CREATE TABLE isolation_probe (id integer PRIMARY KEY)", firstConnection);
            _ = await createTable.ExecuteNonQueryAsync();
        }

        Assert.True(await TableExistsAsync(Database.ConnectionString, "isolation_probe"));
        Assert.False(await TableExistsAsync(secondDatabase.ConnectionString, "isolation_probe"));
    }

    [Fact]
    public async Task ParallelDatabaseLeasesRemainIndependent()
    {
        PostgreSqlTestDatabase[] databases = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => Fixture.CreateDatabaseAsync()));

        try
        {
            Assert.Equal(databases.Length, databases.Select(database => database.Name).Distinct().Count());

            string[] observedDatabaseNames = await Task.WhenAll(databases.Select(GetCurrentDatabaseAsync));

            Assert.Equal(
                databases.Select(database => database.Name).Order(StringComparer.Ordinal),
                observedDatabaseNames.Order(StringComparer.Ordinal));
        }
        finally
        {
            await Task.WhenAll(databases.Select(database => database.DisposeAsync().AsTask()));
        }
    }

    [Fact]
    public async Task CleanupFailureIsReportedInsteadOfBeingIgnored()
    {
        PostgreSqlContainerFixture isolatedFixture = new();

        try
        {
            await isolatedFixture.InitializeAsync();
            PostgreSqlTestDatabase isolatedDatabase = await isolatedFixture.CreateDatabaseAsync();
            await isolatedFixture.DisposeAsync();

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => isolatedDatabase.DisposeAsync().AsTask());

            Assert.Contains("cleanup failed", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(exception.InnerException);
        }
        finally
        {
            await isolatedFixture.DisposeAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(string connectionString, string tableName)
    {
        const string query = "SELECT to_regclass(@tableName) IS NOT NULL";

        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(query, connection);
        command.Parameters.AddWithValue("tableName", tableName);

        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task<string> GetCurrentDatabaseAsync(PostgreSqlTestDatabase database)
    {
        await using NpgsqlConnection connection = new(database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new("SELECT current_database()", connection);

        return (string)(await command.ExecuteScalarAsync() ?? string.Empty);
    }
}
