using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class PostgreSqlParallelExecutionTests(
    PostgreSqlContainerFixture fixture) : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task IndependentDatabaseScopesCanExecuteAtTheSameTime()
    {
        Task<PostgreSqlTestDatabase> leftTask = fixture.CreateDatabaseAsync();
        Task<PostgreSqlTestDatabase> rightTask = fixture.CreateDatabaseAsync();
        PostgreSqlTestDatabase[] databases = await Task.WhenAll(leftTask, rightTask);

        await using PostgreSqlTestDatabase left = databases[0];
        await using PostgreSqlTestDatabase right = databases[1];

        Task<ExecutionInterval> leftExecution = MeasureServerExecutionAsync(left.ConnectionString);
        Task<ExecutionInterval> rightExecution = MeasureServerExecutionAsync(right.ConnectionString);
        ExecutionInterval[] intervals = await Task.WhenAll(leftExecution, rightExecution);

        Assert.True(
            intervals[0].StartedAt < intervals[1].FinishedAt &&
            intervals[1].StartedAt < intervals[0].FinishedAt,
            $"Expected overlapping PostgreSQL work, but observed {intervals[0]} and {intervals[1]}.");
    }

    private static async Task<ExecutionInterval> MeasureServerExecutionAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT statement_timestamp(), pg_sleep(1), clock_timestamp();",
            connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        return new ExecutionInterval(reader.GetDateTime(0), reader.GetDateTime(2));
    }

    private sealed record ExecutionInterval(DateTime StartedAt, DateTime FinishedAt);
}
