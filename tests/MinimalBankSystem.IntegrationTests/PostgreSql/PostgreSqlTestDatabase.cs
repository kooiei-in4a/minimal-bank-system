using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Isolated test database owned by a single test class or explicit test scope.
/// Each instance creates a dedicated database on the shared container and drops it on disposal.
/// </summary>
internal sealed class PostgreSqlTestDatabase : IAsyncDisposable
{
    private readonly string _adminConnectionString;
    private readonly string _databaseName;
    private bool _disposed;

    internal string ConnectionString { get; }

    private PostgreSqlTestDatabase(string adminConnectionString, string databaseName, string connectionString)
    {
        _adminConnectionString = adminConnectionString;
        _databaseName = databaseName;
        ConnectionString = connectionString;
    }

    internal static async Task<PostgreSqlTestDatabase> CreateAsync(SharedPostgreSqlContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);

        string adminConnectionString = container.Container.GetConnectionString();
        string databaseName = $"test_{Guid.NewGuid():N}";

        await using NpgsqlConnection connection = new(adminConnectionString);
        await connection.OpenAsync();

        await using NpgsqlCommand createCommand = connection.CreateCommand();
        createCommand.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await createCommand.ExecuteNonQueryAsync();

        NpgsqlConnectionStringBuilder builder = new(adminConnectionString)
        {
            Database = databaseName,
        };

        return new PostgreSqlTestDatabase(adminConnectionString, databaseName, builder.ConnectionString);
    }

    internal async ValueTask DisposeAsync(bool terminateBackends)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await PostgreSqlDatabaseCleanup.DropDatabaseAsync(
            _adminConnectionString,
            _databaseName,
            terminateBackends);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(terminateBackends: true);
    }
}
