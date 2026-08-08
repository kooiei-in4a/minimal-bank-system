using System.Collections.Concurrent;
using MinimalBankSystem.PostgresIntegrationTests.Fixtures;

namespace MinimalBankSystem.PostgresIntegrationTests;

/// <summary>
/// The meeting point the two parallel participants use to prove they overlap.
/// </summary>
/// <remarks>
/// Each participant publishes what it sees, then blocks until the other class has entered its own
/// test body. If xUnit ran the two classes one after the other, the first one would never observe
/// the second and the test fails on the rendezvous timeout rather than passing silently.
/// </remarks>
internal static class ParallelExecutionRendezvous
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    public static readonly TaskCompletionSource FirstArrived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static readonly TaskCompletionSource SecondArrived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static readonly ConcurrentDictionary<string, string> DatabaseNames =
        new(StringComparer.Ordinal);

    public static readonly ConcurrentDictionary<string, string> ContainerIds =
        new(StringComparer.Ordinal);

    public static async Task RendezvousAsync(
        PostgresIntegrationTestState state,
        TaskCompletionSource arrived,
        Task otherArrived)
    {
        DatabaseNames[state.ClassName] = state.DatabaseName;
        ContainerIds[state.ClassName] = state.ContainerId;
        arrived.TrySetResult();

        using CancellationTokenSource stopWaiting = new();
        Task completed = await Task.WhenAny(otherArrived, Task.Delay(Timeout, stopWaiting.Token));
        await stopWaiting.CancelAsync();

        Assert.True(
            completed == otherArrived,
            $"'{state.ClassName}' waited {Timeout.TotalSeconds:F0}s inside its test body without the " +
            "other PostgreSQL test class starting, so the declared parallel range does not hold.");

        Assert.Equal(2, DatabaseNames.Count);
        Assert.Equal(2, DatabaseNames.Values.Distinct(StringComparer.Ordinal).Count());
        Assert.Single(ContainerIds.Values.Distinct(StringComparer.Ordinal));
    }
}

/// <summary>The facts a parallel participant publishes about itself.</summary>
internal sealed record PostgresIntegrationTestState(
    string ClassName,
    string DatabaseName,
    string ContainerId);

/// <summary>
/// Proves the declared parallel range: this class and
/// <see cref="PostgresParallelExecutionSecondTests"/> really do run at the same time, on one shared
/// container, each inside its own database.
/// </summary>
[Trait(PostgresTestCategories.Category, PostgresTestCategories.PostgresIntegration)]
public sealed class PostgresParallelExecutionFirstTests : PostgresIntegrationTest
{
    [Fact]
    public async Task RunsBesideTheOtherPostgresTestClass()
    {
        await Database.ExecuteAsync("CREATE TABLE parallel_probe (id integer PRIMARY KEY)");

        await ParallelExecutionRendezvous.RendezvousAsync(
            new PostgresIntegrationTestState(nameof(PostgresParallelExecutionFirstTests), Database.Name, Server.ContainerId),
            ParallelExecutionRendezvous.FirstArrived,
            ParallelExecutionRendezvous.SecondArrived.Task);

        await Database.ExecuteAsync("INSERT INTO parallel_probe (id) VALUES (1)");
        Assert.Equal(1L, await Database.ExecuteScalarAsync<long>("SELECT count(*) FROM parallel_probe"));
    }
}

/// <summary>
/// The other half of the parallel-range proof described on
/// <see cref="PostgresParallelExecutionFirstTests"/>.
/// </summary>
[Trait(PostgresTestCategories.Category, PostgresTestCategories.PostgresIntegration)]
public sealed class PostgresParallelExecutionSecondTests : PostgresIntegrationTest
{
    [Fact]
    public async Task RunsBesideTheOtherPostgresTestClass()
    {
        await Database.ExecuteAsync("CREATE TABLE parallel_probe (id integer PRIMARY KEY)");

        await ParallelExecutionRendezvous.RendezvousAsync(
            new PostgresIntegrationTestState(nameof(PostgresParallelExecutionSecondTests), Database.Name, Server.ContainerId),
            ParallelExecutionRendezvous.SecondArrived,
            ParallelExecutionRendezvous.FirstArrived.Task);

        await Database.ExecuteAsync("INSERT INTO parallel_probe (id) VALUES (2)");
        Assert.Equal(1L, await Database.ExecuteScalarAsync<long>("SELECT count(*) FROM parallel_probe"));
    }
}
