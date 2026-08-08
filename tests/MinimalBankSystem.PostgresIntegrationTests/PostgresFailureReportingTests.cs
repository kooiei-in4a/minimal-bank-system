using MinimalBankSystem.PostgresIntegrationTests.Fixtures;
using Npgsql;

namespace MinimalBankSystem.PostgresIntegrationTests;

/// <summary>
/// Proves that lifecycle problems become explicit test failures.
/// </summary>
/// <remarks>
/// These tests are cluster-scoped: they start an extra container and drop a database behind the
/// fixture's back, so they join <see cref="PostgresClusterScope"/> and never run beside each other.
/// </remarks>
[Collection(PostgresClusterScope.Name)]
[Trait(PostgresTestCategories.Category, PostgresTestCategories.PostgresIntegration)]
public sealed class PostgresFailureReportingTests
{
    [Fact]
    public async Task AnUnreachableContainerRuntimeFailsTheTest()
    {
        using CancellationTokenSource cancellation = new(TimeSpan.FromMinutes(2));

        PostgresTestInfrastructureException failure =
            await Assert.ThrowsAsync<PostgresTestInfrastructureException>(async () =>
            {
                PostgresTestServer unexpected =
                    await PostgresTestServer.StartAsync("tcp://127.0.0.1:1", cancellation.Token);
                await unexpected.DisposeAsync();
            });

        Assert.Contains(PostgresTestImage.Reference, failure.Message, StringComparison.Ordinal);
        Assert.NotNull(failure.InnerException);
    }

    [Fact]
    public async Task AnUnreachableServerFailsTheTest()
    {
        await using NpgsqlDataSource unreachable = new NpgsqlDataSourceBuilder(
            "Host=127.0.0.1;Port=1;Database=postgres;Username=postgres;Password=unused;Timeout=2")
            .Build();

        PostgresTestInfrastructureException failure =
            await Assert.ThrowsAsync<PostgresTestInfrastructureException>(
                () => PostgresTestServer.VerifyServerAsync(unreachable));

        Assert.Contains("127.0.0.1:1/postgres", failure.Message, StringComparison.Ordinal);
        Assert.NotNull(failure.InnerException);
    }

    [Fact]
    public async Task AFailedCleanupFailsTheTest()
    {
        PostgresTestServer server = await PostgresTestServer.SharedAsync();
        PostgresTestDatabase database = await server.CreateDatabaseAsync("cleanup_failure");

        // Remove the database behind the fixture's back so that its own DROP has to fail.
        await server.ExecuteClusterStatementAsync($"DROP DATABASE \"{database.Name}\" WITH (FORCE)");

        PostgresTestCleanupException failure =
            await Assert.ThrowsAsync<PostgresTestCleanupException>(async () => await database.DisposeAsync());

        Assert.Equal(database.Name, failure.DatabaseName);
        Assert.Contains(database.Name, failure.Message, StringComparison.Ordinal);
        Assert.IsType<PostgresException>(failure.InnerException);
    }
}
