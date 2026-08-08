using MinimalBankSystem.IntegrationTests.Fixtures;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection("PostgreSQL")]
[Trait("Category", "PostgreSQL")]
public class PostgreSqlParallelTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _containerFixture;
    private readonly PostgreSqlSchemaFixture _schemaFixture;

    public PostgreSqlParallelTests(PostgreSqlContainerFixture containerFixture)
    {
        _containerFixture = containerFixture;
        _schemaFixture = new PostgreSqlSchemaFixture(containerFixture);
    }

    public async Task InitializeAsync() => await _schemaFixture.InitializeAsync();

    public async Task DisposeAsync() => await _schemaFixture.DisposeAsync();

    [Fact]
    public async Task SharedContainerIsAccessible()
    {
        await using var connection = new NpgsqlConnection(
            _containerFixture.GetConnectionString());
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand("SELECT 1", connection);
        var result = await command.ExecuteScalarAsync();

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task IndependentSchemaIsUnique()
    {
        await using var connection = new NpgsqlConnection(
            _containerFixture.GetConnectionString());
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT schema_name FROM information_schema.schemata WHERE schema_name = @schema",
            connection);
        command.Parameters.AddWithValue("schema", _schemaFixture.SchemaName);
        var schemaName = (string?)(await command.ExecuteScalarAsync());

        Assert.Equal(_schemaFixture.SchemaName, schemaName);
    }

    [Fact]
    public async Task ConcurrentQueriesSucceed()
    {
        var tasks = Enumerable.Range(0, 5).Select(async i =>
        {
            await using var connection = await _schemaFixture.CreateOpenConnectionAsync();

            await using var command = new NpgsqlCommand("SELECT @i", connection);
            command.Parameters.AddWithValue("i", i);
            var result = await command.ExecuteScalarAsync();

            return (int)result!;
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(5, results.Length);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(i, results[i]);
        }
    }

    [Fact]
    public async Task TableIsolationBetweenSchemas()
    {
        await using var connection = await _schemaFixture.CreateOpenConnectionAsync();

        await using var command = new NpgsqlCommand(
            "CREATE TABLE parallel_test_table (id SERIAL PRIMARY KEY)", connection);
        await command.ExecuteNonQueryAsync();

        await using var verifyCommand = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = @schema AND table_name = 'parallel_test_table')",
            connection);
        verifyCommand.Parameters.AddWithValue("schema", _schemaFixture.SchemaName);
        var exists = (bool)(await verifyCommand.ExecuteScalarAsync())!;

        Assert.True(exists);
    }
}
