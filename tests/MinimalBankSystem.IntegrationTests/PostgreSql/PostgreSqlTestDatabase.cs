using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

public sealed class PostgreSqlTestDatabase : IAsyncDisposable
{
    private readonly string _masterConnectionString;
    private readonly string _databaseName;
    private bool _disposed;

    private PostgreSqlTestDatabase(string masterConnectionString, string databaseName, string testConnectionString)
    {
        _masterConnectionString = masterConnectionString;
        _databaseName = databaseName;
        TestConnectionString = testConnectionString;
    }

    public string TestConnectionString { get; }

    public string DatabaseName => _databaseName;

    public static async Task<PostgreSqlTestDatabase> CreateAsync(string masterConnectionString)
    {
        string databaseName = "test_" + Guid.NewGuid().ToString("N");

        await using (NpgsqlConnection masterConnection = new(masterConnectionString))
        {
            await masterConnection.OpenAsync();
            await using NpgsqlCommand createCmd = masterConnection.CreateCommand();
            createCmd.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await createCmd.ExecuteNonQueryAsync();
        }

        NpgsqlConnectionStringBuilder builder = new(masterConnectionString)
        {
            Database = databaseName,
        };

        string testConnectionString = builder.ConnectionString;

        return new PostgreSqlTestDatabase(masterConnectionString, databaseName, testConnectionString);
    }

    public async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        NpgsqlConnection connection = new(TestConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            await using NpgsqlConnection masterConnection = new(_masterConnectionString);
            await masterConnection.OpenAsync();

            await using NpgsqlCommand terminateCmd = masterConnection.CreateCommand();
            terminateCmd.CommandText =
                $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{_databaseName}' AND pid <> pg_backend_pid()";
            await terminateCmd.ExecuteNonQueryAsync();

            await using NpgsqlCommand dropCmd = masterConnection.CreateCommand();
            dropCmd.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\"";
            await dropCmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to clean up test database '{_databaseName}'. " +
                $"Manual cleanup may be required.",
                ex);
        }
    }
}
