using Npgsql;

namespace MinimalBankSystem.PostgresIntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class FixtureLifecycleTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task ContainerAcceptsConnectionsAndRunsAQuery()
    {
        await using NpgsqlConnection connection = new(fixture.Container.GetConnectionString());
        await connection.OpenAsync();

        await using NpgsqlCommand command = new("SELECT 1", connection);
        object? result = await command.ExecuteScalarAsync();

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ContainerRunsThePinnedPostgres18ServerVersion()
    {
        await using NpgsqlConnection connection = new(fixture.Container.GetConnectionString());
        await connection.OpenAsync();

        await using NpgsqlCommand command = new("SHOW server_version", connection);
        string? version = (string?)await command.ExecuteScalarAsync();

        Assert.StartsWith("18.", version, StringComparison.Ordinal);
    }

    [Fact]
    public void ContainerImageIsPinnedToTheApprovedRepositoryTagAndDigest()
    {
        Assert.Equal(PostgresImage.Repository, fixture.Container.Image.Repository);
        Assert.Equal(PostgresImage.Tag, fixture.Container.Image.Tag);
        Assert.Equal(PostgresImage.Digest, fixture.Container.Image.Digest);
    }

    [Fact]
    public async Task CreateDatabaseProvisionsAConnectableDatabaseAndDisposeDropsIt()
    {
        string databaseName;

        await using (PostgresTestDatabase database = await fixture.CreateDatabaseAsync())
        {
            databaseName = database.Name;

            await using NpgsqlConnection connection = new(database.ConnectionString);
            await connection.OpenAsync();

            await using NpgsqlCommand command = new("SELECT current_database()", connection);
            object? currentDatabase = await command.ExecuteScalarAsync();

            Assert.Equal(databaseName, currentDatabase);
        }

        await using NpgsqlConnection admin = new(fixture.Container.GetConnectionString());
        await admin.OpenAsync();

        await using NpgsqlCommand exists = new(
            "SELECT COUNT(*) FROM pg_database WHERE datname = @name",
            admin);
        exists.Parameters.AddWithValue("name", databaseName);
        long remaining = (long)(await exists.ExecuteScalarAsync())!;

        Assert.Equal(0, remaining);
    }
}
