using Npgsql;

namespace MinimalBankSystem.IntegrationTests;

public sealed class PostgreSqlFixtureTests(PostgreSqlTestFixture fixture)
    : IClassFixture<PostgreSqlTestFixture>
{
    [Fact]
    [Trait("Category", PostgreSqlTestFixture.Category)]
    public async Task StartsPinnedPostgreSql18AndCleansUpEachDatabase()
    {
        Assert.Equal(
            "postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a",
            PostgreSqlTestFixture.ImageReference);

        string databaseName;
        await using (PostgreSqlDatabaseLease database = await fixture.CreateDatabaseAsync())
        {
            databaseName = database.DatabaseName;

            await using NpgsqlConnection connection = new(database.ConnectionString);
            await connection.OpenAsync();

            await using NpgsqlCommand versionCommand = connection.CreateCommand();
            versionCommand.CommandText = "SHOW server_version;";
            string version = (string)(await versionCommand.ExecuteScalarAsync())!;
            Assert.StartsWith("18.", version, StringComparison.Ordinal);

            await using NpgsqlCommand databaseCommand = connection.CreateCommand();
            databaseCommand.CommandText = "SELECT current_database();";
            Assert.Equal(databaseName, await databaseCommand.ExecuteScalarAsync());
        }

        await using NpgsqlConnection adminConnection = new(fixture.AdminConnectionString);
        await adminConnection.OpenAsync();
        await using NpgsqlCommand existsCommand = adminConnection.CreateCommand();
        existsCommand.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = $1);";
        existsCommand.Parameters.AddWithValue(databaseName);

        Assert.False((bool)(await existsCommand.ExecuteScalarAsync())!);
    }
}
