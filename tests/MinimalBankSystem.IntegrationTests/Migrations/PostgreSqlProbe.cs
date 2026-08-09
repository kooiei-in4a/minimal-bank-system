using Npgsql;

namespace MinimalBankSystem.IntegrationTests.Migrations;

internal static class PostgreSqlProbe
{
    public static async Task<IReadOnlyList<string>> GetAppliedMigrationIdsAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        if (!await RelationExistsAsync(connectionString, "public.\"__EFMigrationsHistory\"", cancellationToken))
        {
            return [];
        }

        string[]? rows = await QuerySingleColumnAsync<string>(
            connectionString,
            "SELECT \"MigrationId\" FROM public.\"__EFMigrationsHistory\" ORDER BY \"MigrationId\";",
            cancellationToken);
        return rows ?? [];
    }

    public static async Task<IReadOnlyList<string>> GetUserTablesAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        string[]? rows = await QuerySingleColumnAsync<string>(
            connectionString,
            "SELECT table_name FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_type = 'BASE TABLE' ORDER BY table_name;",
            cancellationToken);
        return rows ?? [];
    }

    public static async Task<IReadOnlyList<string>> GetUserSequencesAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        string[]? rows = await QuerySingleColumnAsync<string>(
            connectionString,
            "SELECT sequence_name FROM information_schema.sequences " +
            "WHERE sequence_schema = 'public' ORDER BY sequence_name;",
            cancellationToken);
        return rows ?? [];
    }

    public static async Task<IReadOnlyList<string>> GetUserTriggersAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        string[]? rows = await QuerySingleColumnAsync<string>(
            connectionString,
            "SELECT trigger_name FROM information_schema.triggers " +
            "WHERE trigger_schema = 'public' ORDER BY trigger_name;",
            cancellationToken);
        return rows ?? [];
    }

    private static async Task<string[]?> QuerySingleColumnAsync<T>(
        string connectionString,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand command = new(commandText, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        List<string> values = [];

        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return values.Count == 0 ? null : values.ToArray();
    }

    private static async Task<bool> RelationExistsAsync(
        string connectionString,
        string qualifiedName,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand command = new(
            "SELECT to_regclass($1) IS NOT NULL;",
            connection);
        command.Parameters.AddWithValue(qualifiedName);
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }
}
