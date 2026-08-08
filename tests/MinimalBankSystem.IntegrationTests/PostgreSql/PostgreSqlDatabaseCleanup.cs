using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Drops an isolated test database. Cleanup failures throw <see cref="PostgreSqlTestCleanupException"/>.
/// </summary>
internal static class PostgreSqlDatabaseCleanup
{
    internal static async Task DropDatabaseAsync(
        string adminConnectionString,
        string databaseName,
        bool terminateBackends = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adminConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        try
        {
            await using NpgsqlConnection connection = new(adminConnectionString);
            await connection.OpenAsync();

            if (terminateBackends)
            {
                await using NpgsqlCommand terminateCommand = connection.CreateCommand();
                terminateCommand.CommandText =
                    "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @databaseName AND pid <> pg_backend_pid()";
                terminateCommand.Parameters.AddWithValue("databaseName", databaseName);
                await terminateCommand.ExecuteNonQueryAsync();
            }

            await using NpgsqlCommand dropCommand = connection.CreateCommand();
            dropCommand.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\"";
            await dropCommand.ExecuteNonQueryAsync();
        }
        catch (Exception ex) when (ex is not PostgreSqlTestCleanupException)
        {
            throw new PostgreSqlTestCleanupException(
                $"Failed to drop test database '{databaseName}'.",
                ex);
        }
    }
}
