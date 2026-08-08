using Npgsql;
using MinimalBankSystem.IntegrationTests.Fixtures;

namespace MinimalBankSystem.IntegrationTests;

/// <summary>
/// Tests for PostgreSQL fixture lifecycle management.
/// </summary>
public class PostgreSqlFixtureLifecycleTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public PostgreSqlFixtureLifecycleTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void ConnectionStringShouldNotBeEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(_fixture.ConnectionString));
    }

    [Fact]
    public void ConnectionStringShouldContainDatabaseName()
    {
        Assert.Contains(_fixture.DatabaseName, _fixture.ConnectionString);
    }

    [Fact]
    public async Task CreateConnectionAsyncShouldReturnOpenConnection()
    {
        await using var connection = await _fixture.CreateConnectionAsync();

        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    [Fact]
    public async Task ExecuteSqlAsyncShouldNotThrow()
    {
        const string sql = "SELECT 1";

        await _fixture.ExecuteSqlAsync(sql);
    }

    [Fact]
    public async Task ConnectionShouldConnectToCorrectDatabase()
    {
        await using var connection = await _fixture.CreateConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT current_database()", connection);
        var result = await command.ExecuteScalarAsync();

        Assert.Equal(_fixture.DatabaseName, result?.ToString());
    }
}
