using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection("PostgreSql")]
public sealed class PostgreSqlContainerLifecycleTests(PostgreSqlContainerFixture fixture)
{
    [Fact]
    public async Task ContainerStartsAndAcceptsConnection()
    {
        await using NpgsqlConnection connection = new(fixture.ConnectionString);
        await connection.OpenAsync();

        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    [Fact]
    public async Task PostgreSqlVersionIs18()
    {
        await using NpgsqlConnection connection = new(fixture.ConnectionString);
        await connection.OpenAsync();

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SHOW server_version";
        object? result = await command.ExecuteScalarAsync();

        string version = Assert.IsType<string>(result);
        Assert.StartsWith("18.", version);
    }

    [Fact]
    public async Task ImageMatchesPinnedDigest()
    {
        Assert.Equal(
            PostgreSqlContainerFixture.ImageReference,
            fixture.Container.Image.FullName);
    }

    [Fact]
    public async Task CanCreateAndDropDatabase()
    {
        await using PostgreSqlTestDatabase db = await PostgreSqlTestDatabase.CreateAsync(fixture.ConnectionString);

        await using NpgsqlConnection connection = new(db.TestConnectionString);
        await connection.OpenAsync();

        Assert.Equal(System.Data.ConnectionState.Open, connection.State);

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT current_database()";
        object? result = await command.ExecuteScalarAsync();

        Assert.Equal(db.DatabaseName, result);
    }
}
