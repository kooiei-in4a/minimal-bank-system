using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Proves per-test database isolation and concurrent use without shared mutable state.
/// </summary>
[Trait("Category", PostgreSqlTestCategories.Category)]
public sealed class PostgreSqlIsolationTests
{
    [Fact]
    public async Task SeparateTestsDoNotObserveEachOthersTables()
    {
        await using PostgreSqlTestDatabase first = await PostgreSqlTestDatabase.CreateAsync();
        await using PostgreSqlTestDatabase second = await PostgreSqlTestDatabase.CreateAsync();

        Assert.NotEqual(first.DatabaseName, second.DatabaseName);

        await using (NpgsqlConnection connection = new(first.ConnectionString))
        {
            await connection.OpenAsync();
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE isolation_probe(value text NOT NULL);";
            await command.ExecuteNonQueryAsync();
        }

        await using (NpgsqlConnection connection = new(second.ConnectionString))
        {
            await connection.OpenAsync();
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_name = 'isolation_probe';
                """;
            long count = (long)(await command.ExecuteScalarAsync())!;
            Assert.Equal(0, count);
        }
    }

    [Fact]
    public async Task ParallelWorkersWithDistinctDatabasesDoNotInterfere()
    {
        const string leftMarker = "left-worker";
        const string rightMarker = "right-worker";

        string[] observed = await Task.WhenAll(
            WriteAndReadMarkerAsync(leftMarker),
            WriteAndReadMarkerAsync(rightMarker));

        Assert.Equal([leftMarker, rightMarker], observed.Order(StringComparer.Ordinal).ToArray());
    }

    private static async Task<string> WriteAndReadMarkerAsync(string marker)
    {
        await using PostgreSqlTestDatabase database = await PostgreSqlTestDatabase.CreateAsync();
        await using NpgsqlConnection connection = new(database.ConnectionString);
        await connection.OpenAsync();

        await using (NpgsqlCommand create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE parallel_probe(value text NOT NULL);";
            await create.ExecuteNonQueryAsync();
        }

        await using (NpgsqlCommand insert = connection.CreateCommand())
        {
            insert.CommandText = "INSERT INTO parallel_probe(value) VALUES (@value);";
            insert.Parameters.AddWithValue("value", marker);
            await insert.ExecuteNonQueryAsync();
        }

        await using NpgsqlCommand read = connection.CreateCommand();
        read.CommandText = "SELECT value FROM parallel_probe;";
        return (string)(await read.ExecuteScalarAsync())!;
    }
}
