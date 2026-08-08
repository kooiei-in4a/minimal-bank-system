using Npgsql;
using MinimalBankSystem.IntegrationTests.Fixtures;

namespace MinimalBankSystem.IntegrationTests;

/// <summary>
/// Tests for database isolation between test instances.
/// </summary>
public class PostgreSqlFixtureIsolationTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public PostgreSqlFixtureIsolationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DatabaseShouldBeEmptyOnFirstAccess()
    {
        await using var connection = await _fixture.CreateConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public'",
            connection);
        var count = await command.ExecuteScalarAsync();

        Assert.Equal(0L, count);
    }

    [Fact]
    public async Task DropAllTablesAsyncShouldRemoveAllTables()
    {
        // Create a test table
        await _fixture.ExecuteSqlAsync("CREATE TABLE test_isolation (id INT PRIMARY KEY, value TEXT)");

        // Verify table exists
        await using (var connection = await _fixture.CreateConnectionAsync())
        await using (var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'test_isolation'",
            connection))
        {
            var count = await command.ExecuteScalarAsync();
            Assert.Equal(1L, count);
        }

        // Drop all tables
        await _fixture.DropAllTablesAsync();

        // Verify table is gone
        await using (var connection = await _fixture.CreateConnectionAsync())
        await using (var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'test_isolation'",
            connection))
        {
            var count = await command.ExecuteScalarAsync();
            Assert.Equal(0L, count);
        }
    }

    [Fact]
    public async Task EachFixtureInstanceShouldHaveUniqueDatabaseName()
    {
        var fixture1 = new PostgreSqlFixture();
        var fixture2 = new PostgreSqlFixture();

        try
        {
            Assert.NotEqual(fixture1.DatabaseName, fixture2.DatabaseName);
        }
        finally
        {
            await fixture1.DisposeAsync();
            await fixture2.DisposeAsync();
        }
    }
}
