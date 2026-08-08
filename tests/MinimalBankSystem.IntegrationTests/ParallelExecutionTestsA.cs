using Npgsql;
using MinimalBankSystem.IntegrationTests.Fixtures;

namespace MinimalBankSystem.IntegrationTests;

/// <summary>
/// Tests that verify parallel execution support.
/// This class uses a different collection to run in parallel with ParallelExecutionTestsB.
/// </summary>
[Collection("PostgreSqlCollection")]
public class ParallelExecutionTestsA
{
    private readonly PostgreSqlFixture _fixture;

    public ParallelExecutionTestsA(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ParallelTestAShouldCreateTable()
    {
        await _fixture.ExecuteSqlAsync("CREATE TABLE parallel_test_a (id INT PRIMARY KEY, value TEXT)");
        await _fixture.ExecuteSqlAsync("INSERT INTO parallel_test_a VALUES (1, 'test_a')");

        await using var connection = await _fixture.CreateConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT value FROM parallel_test_a WHERE id = 1", connection);
        var result = await command.ExecuteScalarAsync();

        Assert.Equal("test_a", result?.ToString());
    }

    [Fact]
    public async Task ParallelTestAShouldIsolateFromOtherCollections()
    {
        // This test should not see tables from other collections
        await using var connection = await _fixture.CreateConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_name LIKE 'parallel_test_b%'",
            connection);
        var count = await command.ExecuteScalarAsync();

        Assert.Equal(0L, count);
    }
}
