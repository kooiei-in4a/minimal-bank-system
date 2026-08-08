using MinimalBankSystem.IntegrationTests.Fixtures;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection("PostgreSQL")]
[Trait("Category", "PostgreSQL")]
public class PostgreSqlFailureTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _containerFixture;
    private readonly PostgreSqlSchemaFixture _schemaFixture;

    public PostgreSqlFailureTests(PostgreSqlContainerFixture containerFixture)
    {
        _containerFixture = containerFixture;
        _schemaFixture = new PostgreSqlSchemaFixture(containerFixture);
    }

    public async Task InitializeAsync() => await _schemaFixture.InitializeAsync();

    public async Task DisposeAsync() => await _schemaFixture.DisposeAsync();

    [Fact]
    public async Task InvalidConnectionThrowsNpgsqlException()
    {
        var invalidConnectionString =
            new NpgsqlConnectionStringBuilder(_containerFixture.GetConnectionString())
            {
                Host = "nonexistent-host",
                Timeout = 2
            }.ConnectionString;

        await using var connection = new NpgsqlConnection(invalidConnectionString);

        await Assert.ThrowsAsync<NpgsqlException>(async () =>
        {
            await connection.OpenAsync();
        });
    }

    [Fact]
    public async Task InvalidDatabaseThrowsPostgresException()
    {
        var invalidConnectionString =
            new NpgsqlConnectionStringBuilder(_containerFixture.GetConnectionString())
            {
                Database = "nonexistent_database"
            }.ConnectionString;

        await using var connection = new NpgsqlConnection(invalidConnectionString);

        await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await connection.OpenAsync();
        });
    }

    [Fact]
    public async Task InvalidSqlThrowsPostgresException()
    {
        await using var connection = await _schemaFixture.CreateOpenConnectionAsync();

        await using var command = new NpgsqlCommand(
            "SELECT * FROM nonexistent_table", connection);

        await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await command.ExecuteNonQueryAsync();
        });
    }

    [Fact]
    public async Task DuplicateSchemaThrowsPostgresException()
    {
        var connectionString = _containerFixture.GetConnectionString();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            $"CREATE SCHEMA \"{_schemaFixture.SchemaName}\"", connection);

        await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await command.ExecuteNonQueryAsync();
        });
    }
}
