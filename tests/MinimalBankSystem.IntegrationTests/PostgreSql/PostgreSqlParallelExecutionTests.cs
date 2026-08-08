namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection(PostgreSqlTestCollections.Name)]
[Trait(PostgreSqlTestCollections.CategoryTraitName, PostgreSqlTestCollections.CategoryTraitValue)]
public sealed class PostgreSqlParallelExecutionTests(PostgreSqlContainerFixture container)
{
    private const int WorkerCount = 8;
    private const int RowsPerWorker = 25;

    [Fact]
    public async Task IndependentDatabasesRunConcurrentlyWithoutInterference()
    {
        await container.EnsureStartedAsync();

        string[] databaseNames = Enumerable.Range(0, WorkerCount)
            .Select(index => PostgreSqlTestDatabase.CreateDatabaseName($"Parallel{index}"))
            .ToArray();

        await Task.WhenAll(databaseNames.Select(RunWorkerAsync));

        foreach (string databaseName in databaseNames)
        {
            Assert.False(
                await PostgreSqlTestDatabase.ExistsAsync(container, databaseName),
                $"Database '{databaseName}' was not removed by the worker cleanup.");
        }
    }

    private async Task RunWorkerAsync(string databaseName)
    {
        await PostgreSqlTestDatabase.CreateAsync(container, databaseName);
        try
        {
            string connectionString = container.GetDatabaseConnectionString(databaseName);
            const string table = "parallel_probe";
            await PostgreSqlTestSql.ExecuteNonQueryAsync(
                connectionString,
                $"CREATE TABLE {table} (value integer)");

            for (int i = 0; i < RowsPerWorker; i++)
            {
                await PostgreSqlTestSql.ExecuteNonQueryAsync(
                    connectionString,
                    $"INSERT INTO {table} (value) VALUES ({i})");
            }

            object? count = await PostgreSqlTestSql.ExecuteScalarAsync(
                connectionString,
                $"SELECT count(*) FROM {table}");

            Assert.Equal((long)RowsPerWorker, count);
        }
        finally
        {
            await PostgreSqlTestDatabase.DropAsync(container, databaseName);
        }
    }
}
