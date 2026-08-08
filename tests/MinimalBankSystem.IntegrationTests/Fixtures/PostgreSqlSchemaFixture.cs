using Npgsql;

namespace MinimalBankSystem.IntegrationTests.Fixtures;

public sealed class PostgreSqlSchemaFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _containerFixture;
    private readonly string _schemaName;

    public string ConnectionString { get; private set; } = string.Empty;

    public string SchemaName => _schemaName;

    public PostgreSqlSchemaFixture(PostgreSqlContainerFixture containerFixture)
    {
        _containerFixture = containerFixture;
        _schemaName = $"test_{Guid.NewGuid():N}";
    }

    public async Task InitializeAsync()
    {
        var masterConnectionString = _containerFixture.GetConnectionString();

        await using (var connection = new NpgsqlConnection(masterConnectionString))
        {
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(
                $"CREATE SCHEMA \"{_schemaName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(masterConnectionString)
        {
            SearchPath = _schemaName
        };
        ConnectionString = builder.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        var masterConnectionString = _containerFixture.GetConnectionString();

        await using var connection = new NpgsqlConnection(masterConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            $"DROP SCHEMA IF EXISTS \"{_schemaName}\" CASCADE", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<NpgsqlConnection> CreateOpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}
