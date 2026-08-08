using Npgsql;
using MinimalBankSystem.IntegrationTests.Fixtures;

namespace MinimalBankSystem.IntegrationTests;

/// <summary>
/// Tests that verify parallel execution support.
/// This class uses a different collection to run in parallel with ParallelExecutionTestsA.
/// </summary>
public class ParallelExecutionTestsB
{
    private readonly PostgreSqlFixture _fixture;

    public ParallelExecutionTestsB()
    {
        _fixture = new PostgreSqlFixture();
    }

    [Fact]
    public async Task ParallelTestBShouldCreateTable()
    {
        await _fixture.InitializeAsync();

        try
        {
            await _fixture.ExecuteSqlAsync("CREATE TABLE parallel_test_b (id INT PRIMARY KEY, value TEXT)");
            await _fixture.ExecuteSqlAsync("INSERT INTO parallel_test_b VALUES (1, 'test_b')");

            await using var connection = await _fixture.CreateConnectionAsync();
            await using var command = new NpgsqlCommand("SELECT value FROM parallel_test_b WHERE id = 1", connection);
            var result = await command.ExecuteScalarAsync();

            Assert.Equal("test_b", result?.ToString());
        }
        finally
        {
            await _fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task ParallelTestBShouldIsolateFromOtherCollections()
    {
        await _fixture.InitializeAsync();

        try
        {
            // This test should not see tables from other collections
            await using var connection = await _fixture.CreateConnectionAsync();
            await using var command = new NpgsqlCommand(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_name LIKE 'parallel_test_a%'",
                connection);
            var count = await command.ExecuteScalarAsync();

            Assert.Equal(0L, count);
        }
        finally
        {
            await _fixture.DisposeAsync();
        }
    }
}
