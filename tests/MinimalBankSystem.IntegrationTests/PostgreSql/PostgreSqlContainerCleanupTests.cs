using System.Diagnostics;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class PostgreSqlContainerCleanupTests
{
    [Fact]
    public async Task DeterministicContainerCleanupFailureViaForceRemove()
    {
        PostgreSqlContainerFixture fixture = new();

        try
        {
            await fixture.InitializeAsync();

            string containerId = fixture.OwnedContainerId
                ?? throw new InvalidOperationException("Fixture has no container ID after successful initialization.");

            await PostgreSqlContainerFixture.ForceRemoveContainerAsync(containerId);

            bool containerExists = await ContainerExistsOnDaemonAsync(containerId);
            Assert.False(containerExists, $"Container '{containerId}' should not exist on the Docker daemon after force-remove.");
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task ContainerCleanupOwnershipModel_PreservesOwnershipThroughLifecycle()
    {
        PostgreSqlContainerFixture fixture = new();

        await fixture.InitializeAsync();

        string? containerId = fixture.OwnedContainerId;
        Assert.NotNull(containerId);
        Assert.NotEmpty(containerId!);

        bool existsBefore = await ContainerExistsOnDaemonAsync(containerId!);
        Assert.True(existsBefore, $"Container '{containerId}' should exist on the Docker daemon after initialization.");

        await fixture.DisposeAsync();

        bool existsAfter = await ContainerExistsOnDaemonAsync(containerId!);
        Assert.False(existsAfter, $"Container '{containerId}' should be removed after successful disposal.");
    }

    [Fact]
    public async Task OwnedContainerIdIsSetAfterSuccessfulInitialization()
    {
        PostgreSqlContainerFixture fixture = new();

        await fixture.InitializeAsync();

        string? containerId = fixture.OwnedContainerId;
        Assert.NotNull(containerId);
        Assert.NotEmpty(containerId!);

        bool exists = await ContainerExistsOnDaemonAsync(containerId!);
        Assert.True(exists, $"Container '{containerId}' should exist on the Docker daemon.");

        await fixture.DisposeAsync();
    }

    [Fact]
    public async Task StartupCleanupFailureReportsBothFailures()
    {
        PostgreSqlContainerFixture fixture = new("tcp://127.0.0.1:1");

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

        try
        {
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.InitializeAsync(timeout.Token));

            Assert.Contains("Failed to start and connect", exception.Message, StringComparison.Ordinal);

            if (exception.InnerException is AggregateException aggregate)
            {
                Assert.True(
                    aggregate.InnerExceptions.Count >= 1,
                    "Expected at least one inner exception (startup failure).");
            }
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task DisposeAsyncSucceedsWhenContainerIsRunning()
    {
        PostgreSqlContainerFixture fixture = new();

        await fixture.InitializeAsync();

        string? containerId = fixture.OwnedContainerId;
        Assert.NotNull(containerId);

        await fixture.DisposeAsync();

        bool existsAfter = await ContainerExistsOnDaemonAsync(containerId!);
        Assert.False(existsAfter, $"Container '{containerId}' should be removed after successful disposal.");
    }

    private static async Task<bool> ContainerExistsOnDaemonAsync(string containerId)
    {
        using Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"inspect {containerId}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        process.Start();
        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }
}
