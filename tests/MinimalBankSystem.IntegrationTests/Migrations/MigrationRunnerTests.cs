using MinimalBankSystem.Migrator;

namespace MinimalBankSystem.IntegrationTests.Migrations;

public sealed class MigrationRunnerTests
{
    [Fact]
    public void TimeoutBudgetIsBoundedAtSixtySeconds()
    {
        Assert.Equal(60, MigrationRunner.TimeoutSeconds);
    }

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
    public async Task MigrateDelegateReceivesATokenThatCancelsAfterTheBudget()
    {
        CancellationToken? observedToken = null;

        await MigrationRunner.RunAsync(
            token =>
            {
                observedToken = token;
                return Task.CompletedTask;
            },
            new StringWriter(),
            new StringWriter());

        Assert.NotNull(observedToken);
        Assert.True(observedToken!.Value.CanBeCanceled);
    }
}
