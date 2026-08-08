using MinimalBankSystem.IntegrationTests.Fixtures;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection("PostgreSQL")]
[Trait("Category", "PostgreSQL")]
public class PostgreSqlContainerLifecycleTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _containerFixture;
    private readonly PostgreSqlSchemaFixture _schemaFixture;

    public PostgreSqlContainerLifecycleTests(PostgreSqlContainerFixture containerFixture)
    {
        _containerFixture = containerFixture;
        _schemaFixture = new PostgreSqlSchemaFixture(containerFixture);
    }

    public async Task InitializeAsync() => await _schemaFixture.InitializeAsync();

    public async Task DisposeAsync() => await _schemaFixture.DisposeAsync();

    [Fact]
    public void ContainerIsNotNull()
    {
        Assert.NotNull(_containerFixture.Container);
    }

    [Fact]
    public async Task CanOpenConnection()
    {
        await using var connection = await _schemaFixture.CreateOpenConnectionAsync();
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    [Fact]
    public async Task CanExecuteSimpleQuery()
    {
        await using var connection = await _schemaFixture.CreateOpenConnectionAsync();

        await using var command = new NpgsqlCommand("SELECT 1", connection);
        var result = await command.ExecuteScalarAsync();

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task CanCreateTable()
    {
        await using var connection = await _schemaFixture.CreateOpenConnectionAsync();

        await using var command = new NpgsqlCommand(
            "CREATE TABLE test_table (id SERIAL PRIMARY KEY, name TEXT NOT NULL)",
            connection);
        await command.ExecuteNonQueryAsync();

        await using var verifyCommand = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = @schema AND table_name = 'test_table')",
            connection);
        verifyCommand.Parameters.AddWithValue("schema", _schemaFixture.SchemaName);
        var exists = (bool)(await verifyCommand.ExecuteScalarAsync())!;

        Assert.True(exists);
    }

    [Fact]
    public async Task QueryReturnsCorrectVersion()
    {
        await using var connection = await _schemaFixture.CreateOpenConnectionAsync();

        await using var command = new NpgsqlCommand("SELECT version()", connection);
        var version = (string)(await command.ExecuteScalarAsync())!;

        Assert.Contains("PostgreSQL 18", version);
    }
}
