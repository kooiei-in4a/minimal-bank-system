using Npgsql;
using MinimalBankSystem.IntegrationTests.Fixtures;

namespace MinimalBankSystem.IntegrationTests;

/// <summary>
/// Tests for container and connection failure handling.
/// </summary>
public class PostgreSqlFixtureFailureTests
{
    [Fact]
    public async Task InvalidConnectionStringShouldThrowNpgsqlException()
    {
        var connection = new NpgsqlConnection("Host=localhost;Port=1;Database=nonexistent;Username=invalid;Password=invalid");

        await Assert.ThrowsAsync<NpgsqlException>(async () =>
        {
            await connection.OpenAsync();
        });
    }

    [Fact]
    public async Task InvalidSqlShouldThrowNpgsqlException()
    {
        var fixture = new PostgreSqlFixture();
        await fixture.InitializeAsync();

        try
        {
            await Assert.ThrowsAnyAsync<NpgsqlException>(async () =>
            {
                await fixture.ExecuteSqlAsync("INVALID SQL STATEMENT");
            });
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }
}
