namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Container cleanup behaviour after a real Docker removal failure.
/// </summary>
[Trait("Category", "PostgreSqlIntegration")]
public sealed class PostgreSqlContainerCleanupTests
{
    private const string StartupFaultMarker = "injected post-start verification failure";

    private static readonly TimeSpan ContainerLifecycleTimeout = TimeSpan.FromMinutes(3);

    [Fact]
    public async Task ContainerCleanupFailureStaysVisibleAndKeepsOwnershipUntilTheContainerIsGone()
    {
        await using DockerEndpointFaultProxy proxy = new();
        using CancellationTokenSource timeout = new(ContainerLifecycleTimeout);

        PostgreSqlContainerFixture fixture = new(proxy.Endpoint);
        await fixture.InitializeAsync(timeout.Token);

        string containerId = Assert.IsType<string>(fixture.UnreclaimedContainerId);
        Assert.True(await DockerEngineEndpoint.ContainerExistsAsync(
            proxy.UpstreamEndpoint,
            containerId,
            timeout.Token));

        try
        {
            // The container is running and reachable; only the Docker control plane it was created
            // through stops answering, so the removal itself is what fails.
            proxy.BreakDockerAccess();

            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.DisposeAsync(timeout.Token));

            Assert.Contains("Failed to remove the PostgreSQL test container", failure.Message, StringComparison.Ordinal);
            Assert.Contains(containerId, failure.Message, StringComparison.Ordinal);
            Assert.NotNull(failure.InnerException);

            Assert.True(await DockerEngineEndpoint.ContainerExistsAsync(
                proxy.UpstreamEndpoint,
                containerId,
                timeout.Token));
            Assert.Equal(containerId, fixture.UnreclaimedContainerId);

            // The root cause, observed directly on the Testcontainers instance the fixture used:
            // its disposed guard was latched by the failed attempt, so disposing it again returns
            // successfully without removing anything.
            await fixture.Container.DisposeAsync();

            Assert.True(await DockerEngineEndpoint.ContainerExistsAsync(
                proxy.UpstreamEndpoint,
                containerId,
                timeout.Token));

            // Retrying cleanup must not read that silence as a removal, and must not hand the
            // still running container back to the poisoned instance either.
            InvalidOperationException retryFailure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.DisposeAsync(timeout.Token));

            Assert.Contains(containerId, retryFailure.Message, StringComparison.Ordinal);
            Assert.Contains(
                $"/containers/{containerId}",
                Assert.IsAssignableFrom<Exception>(retryFailure.InnerException).Message,
                StringComparison.Ordinal);
            Assert.Equal(containerId, fixture.UnreclaimedContainerId);
            Assert.True(await DockerEngineEndpoint.ContainerExistsAsync(
                proxy.UpstreamEndpoint,
                containerId,
                timeout.Token));
        }
        finally
        {
            proxy.RestoreDockerAccess();
        }

        // The retained Docker identity is what finally reclaims the resource.
        await fixture.DisposeAsync(timeout.Token);

        Assert.Null(fixture.UnreclaimedContainerId);
        Assert.False(await DockerEngineEndpoint.ContainerExistsAsync(
            proxy.UpstreamEndpoint,
            containerId,
            timeout.Token));
    }

    [Fact]
    public async Task StartupFailureWithAFailedContainerCleanupKeepsBothErrorsAndTheContainer()
    {
        await using DockerEndpointFaultProxy proxy = new();
        using CancellationTokenSource timeout = new(ContainerLifecycleTimeout);

        PostgreSqlContainerFixture fixture = new(
            proxy.Endpoint,
            _ =>
            {
                proxy.BreakDockerAccess();
                return Task.FromException(new InvalidOperationException(StartupFaultMarker));
            });

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.InitializeAsync(timeout.Token));

        Assert.Contains("Failed to start and connect", failure.Message, StringComparison.Ordinal);

        AggregateException bothFailures = Assert.IsType<AggregateException>(failure.InnerException);

        Assert.Equal(2, bothFailures.InnerExceptions.Count);
        Assert.Contains(
            bothFailures.InnerExceptions,
            exception => exception.Message.Contains(StartupFaultMarker, StringComparison.Ordinal));
        Assert.Contains(
            bothFailures.InnerExceptions,
            exception => !exception.Message.Contains(StartupFaultMarker, StringComparison.Ordinal));

        string containerId = Assert.IsType<string>(fixture.UnreclaimedContainerId);

        Assert.True(await DockerEngineEndpoint.ContainerExistsAsync(
            proxy.UpstreamEndpoint,
            containerId,
            timeout.Token));

        proxy.RestoreDockerAccess();

        await fixture.DisposeAsync(timeout.Token);

        Assert.Null(fixture.UnreclaimedContainerId);
        Assert.False(await DockerEngineEndpoint.ContainerExistsAsync(
            proxy.UpstreamEndpoint,
            containerId,
            timeout.Token));
    }

    [Fact]
    public async Task SuccessfulCleanupReleasesOwnershipAndRemovesTheContainer()
    {
        await using DockerEndpointFaultProxy proxy = new();
        using CancellationTokenSource timeout = new(ContainerLifecycleTimeout);

        PostgreSqlContainerFixture fixture = new(proxy.Endpoint);
        await fixture.InitializeAsync(timeout.Token);

        string containerId = Assert.IsType<string>(fixture.UnreclaimedContainerId);

        await fixture.DisposeAsync(timeout.Token);

        Assert.Null(fixture.UnreclaimedContainerId);
        Assert.False(await DockerEngineEndpoint.ContainerExistsAsync(
            proxy.UpstreamEndpoint,
            containerId,
            timeout.Token));
    }
}
