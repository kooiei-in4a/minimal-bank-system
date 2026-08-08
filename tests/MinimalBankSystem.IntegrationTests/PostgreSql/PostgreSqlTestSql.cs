using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

internal static class PostgreSqlTestSql
{
    public static async Task<int> ExecuteNonQueryAsync(
        string connectionString,
        string sql,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand command = new(sql, connection);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<object?> ExecuteScalarAsync(
        string connectionString,
        string sql,
        CancellationToken cancellationToken = default,
        params NpgsqlParameter[] parameters)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand command = new(sql, connection);
        command.Parameters.AddRange(parameters);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    public static async Task<NpgsqlConnection> OpenConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
