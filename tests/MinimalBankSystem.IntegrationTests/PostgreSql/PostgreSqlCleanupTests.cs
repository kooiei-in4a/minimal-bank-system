using MinimalBankSystem.IntegrationTests.Fixtures;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection("PostgreSQL")]
[Trait("Category", "PostgreSQL")]
public class PostgreSqlCleanupTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _containerFixture;
    private readonly PostgreSqlSchemaFixture _schemaFixture;

    public PostgreSqlCleanupTests(PostgreSqlContainerFixture containerFixture)
    {
        _containerFixture = containerFixture;
        _schemaFixture = new PostgreSqlSchemaFixture(containerFixture);
    }

    public async Task InitializeAsync() => await _schemaFixture.InitializeAsync();

    public async Task DisposeAsync() => await _schemaFixture.DisposeAsync();

    [Fact]
    public async Task SchemaExistsBeforeTest()
    {
        await using var connection = new NpgsqlConnection(
            _containerFixture.GetConnectionString());
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = @schema)",
            connection);
        command.Parameters.AddWithValue("schema", _schemaFixture.SchemaName);
        var exists = (bool)(await command.ExecuteScalarAsync())!;

        Assert.True(exists, $"Schema '{_schemaFixture.SchemaName}' should exist during test.");
    }

    [Fact]
    public async Task TableCreatedInOwnSchema()
    {
        await using var connection = await _schemaFixture.CreateOpenConnectionAsync();

        await using var command = new NpgsqlCommand(
            "CREATE TABLE cleanup_test_table (id SERIAL PRIMARY KEY, value TEXT)", connection);
        await command.ExecuteNonQueryAsync();

        await using var insertCommand = new NpgsqlCommand(
            "INSERT INTO cleanup_test_table (value) VALUES ('test_data') RETURNING id", connection);
        var id = await insertCommand.ExecuteScalarAsync();

        Assert.NotNull(id);
    }

    [Fact]
    public async Task DataInsertedCanBeReadBack()
    {
        await using var connection = await _schemaFixture.CreateOpenConnectionAsync();

        await using var createCommand = new NpgsqlCommand(
            "CREATE TABLE readback_table (id SERIAL PRIMARY KEY, name TEXT NOT NULL)", connection);
        await createCommand.ExecuteNonQueryAsync();

        await using var insertCommand = new NpgsqlCommand(
            "INSERT INTO readback_table (name) VALUES ('isolation_proof') RETURNING id", connection);
        var id = (int)(await insertCommand.ExecuteScalarAsync())!;

        await using var selectCommand = new NpgsqlCommand(
            "SELECT name FROM readback_table WHERE id = @id", connection);
        selectCommand.Parameters.AddWithValue("id", id);
        var name = (string)(await selectCommand.ExecuteScalarAsync())!;

        Assert.Equal("isolation_proof", name);
    }
}
