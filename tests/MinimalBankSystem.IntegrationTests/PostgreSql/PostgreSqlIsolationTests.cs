using MinimalBankSystem.IntegrationTests.Fixtures;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection("PostgreSQL")]
[Trait("Category", "PostgreSQL")]
public class PostgreSqlIsolationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _containerFixture;
    private readonly PostgreSqlSchemaFixture _schemaFixture;

    public PostgreSqlIsolationTests(PostgreSqlContainerFixture containerFixture)
    {
        _containerFixture = containerFixture;
        _schemaFixture = new PostgreSqlSchemaFixture(containerFixture);
    }

    public async Task InitializeAsync() => await _schemaFixture.InitializeAsync();

    public async Task DisposeAsync() => await _schemaFixture.DisposeAsync();

    [Fact]
    public async Task OwnSchemaIsVisible()
    {
        await using var connection = await _schemaFixture.CreateOpenConnectionAsync();

        await using var command = new NpgsqlCommand(
            "CREATE TABLE isolation_table (id SERIAL PRIMARY KEY)", connection);
        await command.ExecuteNonQueryAsync();

        await using var verifyCommand = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = @schema AND table_name = 'isolation_table')",
            connection);
        verifyCommand.Parameters.AddWithValue("schema", _schemaFixture.SchemaName);
        var exists = (bool)(await verifyCommand.ExecuteScalarAsync())!;

        Assert.True(exists);
    }

    [Fact]
    public async Task OtherSchemasDoNotContainOwnTables()
    {
        await using var connection = await _schemaFixture.CreateOpenConnectionAsync();

        await using var command = new NpgsqlCommand(
            "CREATE TABLE isolation_table (id SERIAL PRIMARY KEY)", connection);
        await command.ExecuteNonQueryAsync();

        await using var schemaCommand = new NpgsqlCommand(
            "SELECT schema_name FROM information_schema.schemata WHERE schema_name LIKE 'test_%' AND schema_name != @current",
            connection);
        schemaCommand.Parameters.AddWithValue("current", _schemaFixture.SchemaName);

        var otherSchemas = new List<string>();
        await using var reader = await schemaCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            otherSchemas.Add(reader.GetString(0));
        }

        foreach (var otherSchema in otherSchemas)
        {
            var otherConnectionString = new NpgsqlConnectionStringBuilder(
                _containerFixture.GetConnectionString())
            {
                SearchPath = otherSchema
            }.ConnectionString;

            await using var checkConnection = new NpgsqlConnection(otherConnectionString);
            await checkConnection.OpenAsync();

            await using var checkCommand = new NpgsqlCommand(
                "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = @schema AND table_name = 'isolation_table')",
                checkConnection);
            checkCommand.Parameters.AddWithValue("schema", otherSchema);
            var exists = (bool)(await checkCommand.ExecuteScalarAsync())!;

            Assert.False(exists,
                $"Table 'isolation_table' should not exist in schema '{otherSchema}'.");
        }
    }

    [Fact]
    public async Task SearchPathIsConfinedToOwnSchema()
    {
        await using var connection = await _schemaFixture.CreateOpenConnectionAsync();

        await using var command = new NpgsqlCommand("SHOW search_path", connection);
        var searchPath = (string)(await command.ExecuteScalarAsync())!;

        Assert.Contains(_schemaFixture.SchemaName, searchPath);
    }
}
