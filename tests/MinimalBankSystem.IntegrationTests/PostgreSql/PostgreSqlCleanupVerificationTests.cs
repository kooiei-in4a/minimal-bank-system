using MinimalBankSystem.IntegrationTests.Fixtures;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection("PostgreSQL")]
[Trait("Category", "PostgreSQL")]
public class PostgreSqlCleanupVerificationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _containerFixture;
    private readonly PostgreSqlSchemaFixture _schemaFixture;

    public PostgreSqlCleanupVerificationTests(PostgreSqlContainerFixture containerFixture)
    {
        _containerFixture = containerFixture;
        _schemaFixture = new PostgreSqlSchemaFixture(containerFixture);
    }

    public async Task InitializeAsync() => await _schemaFixture.InitializeAsync();

    public async Task DisposeAsync() => await _schemaFixture.DisposeAsync();

    [Fact]
    public async Task SchemaExistsDuringTest()
    {
        await using var connection = new NpgsqlConnection(
            _containerFixture.GetConnectionString());
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = @schema)",
            connection);
        command.Parameters.AddWithValue("schema", _schemaFixture.SchemaName);
        var exists = (bool)(await command.ExecuteScalarAsync())!;

        Assert.True(exists, "Schema should exist during test execution.");
    }

    [Fact]
    public async Task TableAndDataCreatedInSchema()
    {
        await using var connection = await _schemaFixture.CreateOpenConnectionAsync();

        await using var createCommand = new NpgsqlCommand(
            "CREATE TABLE cleanup_verify_table (id SERIAL PRIMARY KEY, data TEXT NOT NULL)",
            connection);
        await createCommand.ExecuteNonQueryAsync();

        await using var insertCommand = new NpgsqlCommand(
            "INSERT INTO cleanup_verify_table (data) VALUES ('test') RETURNING id", connection);
        var id = await insertCommand.ExecuteScalarAsync();
        Assert.NotNull(id);

        await using var selectCommand = new NpgsqlCommand(
            "SELECT data FROM cleanup_verify_table WHERE id = @id", connection);
        selectCommand.Parameters.AddWithValue("id", id);
        var data = (string)(await selectCommand.ExecuteScalarAsync())!;
        Assert.Equal("test", data);
    }
}
