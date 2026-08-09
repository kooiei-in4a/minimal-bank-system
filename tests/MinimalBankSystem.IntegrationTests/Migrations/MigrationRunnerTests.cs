using MinimalBankSystem.Migrator;

namespace MinimalBankSystem.IntegrationTests.Migrations;

public sealed class MigrationRunnerTests
{
    [Fact]
    public async Task SuccessfulMigrationReturnsZeroAndLogsCompletion()
    {
        StringWriter output = new();
        StringWriter error = new();

        int exitCode = await MigrationRunner.RunAsync(_ => Task.CompletedTask, output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("completed successfully", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task CancellationIsNotSwallowedIntoSuccess()
    {
        StringWriter output = new();
        StringWriter error = new();

        int exitCode = await MigrationRunner.RunAsync(
            _ => throw new OperationCanceledException("simulated bounded-execution timeout"),
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Contains("did not complete", error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("completed successfully", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenericFailureIsNotSwallowedIntoSuccess()
    {
        StringWriter output = new();
        StringWriter error = new();

        int exitCode = await MigrationRunner.RunAsync(
            _ => throw new InvalidOperationException("simulated connection failure"),
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Contains("Migration failed", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("completed successfully", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MigrationIsScheduledToBeCancelledAfterExactlySixtySeconds()
    {
        ControllableTimeProvider timeProvider = new();

        await MigrationRunner.RunAsync(
            _ => Task.CompletedTask,
            new StringWriter(),
            new StringWriter(),
            timeProvider);

        ControllableTimer budgetTimer = Assert.Single(timeProvider.Timers);
        Assert.Equal(TimeSpan.FromSeconds(60), budgetTimer.DueTime);
    }

    [Fact]
    public async Task ElapsingTheBudgetCancelsTheMigrateDelegateAndReturnsNonZero()
    {
        ControllableTimeProvider timeProvider = new();
        StringWriter output = new();
        StringWriter error = new();
        TaskCompletionSource migrationStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<int> run = MigrationRunner.RunAsync(
            async token =>
            {
                migrationStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            },
            output,
            error,
            timeProvider);

        await migrationStarted.Task;
        Assert.Single(timeProvider.Timers).Elapse();

        int exitCode = await run;

        Assert.Equal(1, exitCode);
        Assert.Contains("60s bounded execution budget", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("completed successfully", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Records the timers the bounded-execution <see cref="CancellationTokenSource"/> schedules so a
/// test can assert the exact budget and elapse it without waiting for real time to pass.
/// </summary>
internal sealed class ControllableTimeProvider : TimeProvider
{
    private readonly List<ControllableTimer> timers = [];

    public IReadOnlyList<ControllableTimer> Timers => timers;

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        ControllableTimer timer = new(callback, state, dueTime, period);
        timers.Add(timer);
        return timer;
    }
}

internal sealed class ControllableTimer(
    TimerCallback callback,
    object? state,
    TimeSpan dueTime,
    TimeSpan period) : ITimer
{
    public TimeSpan DueTime { get; private set; } = dueTime;

    public TimeSpan Period { get; private set; } = period;

    public void Elapse() => callback(state);

    public bool Change(TimeSpan newDueTime, TimeSpan newPeriod)
    {
        DueTime = newDueTime;
        Period = newPeriod;
        return true;
    }

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
