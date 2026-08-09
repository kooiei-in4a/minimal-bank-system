using System.Reflection;
using DotNet.Testcontainers;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class PostgreSqlContainerCleanupTests
{
    [Fact]
    public async Task ContainerCleanupFailureRemainsVisibleAndIndependentlyReclaimable()
    {
        CliDockerContainerReclaimer daemon = new();
        ControllableDockerContainerReclaimer reclaimer = new(daemon, remainingRemoveFailures: 1);
        PostgreSqlContainerFixture fixture = new(
            dockerEndpoint: null,
            containerReclaimer: reclaimer);

        await fixture.InitializeAsync();
        Assert.False(string.IsNullOrWhiteSpace(fixture.OwnedContainerId));
        string containerId = fixture.OwnedContainerId!;

        try
        {
            LatchTestcontainersDisposedState(fixture.Container);

            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                fixture.DisposeAsync);

            Assert.Contains("Failed to dispose the PostgreSQL test container", failure.Message, StringComparison.Ordinal);
            Assert.Contains("independent reclaim path", failure.Message, StringComparison.Ordinal);
            Assert.NotNull(failure.InnerException);
            Assert.Equal(containerId, fixture.OwnedContainerId);
            Assert.True(
                await daemon.ExistsAsync(containerId),
                "Cleanup failure must leave the Docker container present and owned.");

            await fixture.Container.DisposeAsync();
            Assert.True(
                await daemon.ExistsAsync(containerId),
                "A poisoned Testcontainers DisposeAsync no-op must not be treated as resource removal success.");
            Assert.Equal(containerId, fixture.OwnedContainerId);

            await fixture.DisposeAsync();

            Assert.Null(fixture.OwnedContainerId);
            Assert.False(
                await daemon.ExistsAsync(containerId),
                "Final independent reclaim must remove the Docker container from the daemon.");
        }
        finally
        {
            if (fixture.OwnedContainerId is not null)
            {
                await fixture.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task StartupPrimaryFailureAndCleanupFailureBothRemainVisibleWithOwnershipRetained()
    {
        CliDockerContainerReclaimer daemon = new();
        ControllableDockerContainerReclaimer reclaimer = new(daemon, remainingRemoveFailures: 1);
        Exception primaryFault = new InvalidOperationException(
            "Injected primary startup fault after the container started.");
        PostgreSqlContainerFixture fixture = new(
            dockerEndpoint: null,
            containerReclaimer: reclaimer,
            startupFaultAfterContainerStart: primaryFault);

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.InitializeAsync());

        try
        {
            Assert.Contains("Failed to start and connect", failure.Message, StringComparison.Ordinal);
            AggregateException aggregate = Assert.IsType<AggregateException>(failure.InnerException);
            Assert.Contains(primaryFault, aggregate.InnerExceptions);
            Assert.Contains(
                aggregate.InnerExceptions,
                exception => exception.Message.Contains("injected container remove failure", StringComparison.Ordinal));

            Assert.False(string.IsNullOrWhiteSpace(fixture.OwnedContainerId));
            string containerId = fixture.OwnedContainerId!;
            Assert.True(
                await daemon.ExistsAsync(containerId),
                "Partial startup cleanup failure must retain Docker container ownership.");

            await fixture.DisposeAsync();

            Assert.Null(fixture.OwnedContainerId);
            Assert.False(await daemon.ExistsAsync(containerId));
        }
        finally
        {
            if (fixture.OwnedContainerId is not null)
            {
                await fixture.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Mirrors Testcontainers 4.13.0 <c>Resource.Disposed</c>: the getter latches disposed
    /// state via <c>Interlocked.CompareExchange</c> before Docker removal completes. Reading it
    /// once leaves a live daemon resource with a poisoned managed instance whose later
    /// <c>DisposeAsync</c> becomes a no-op.
    /// </summary>
    private static void LatchTestcontainersDisposedState(PostgreSqlContainer container)
    {
        Type? type = container.GetType();
        while (type is not null && type != typeof(Resource))
        {
            type = type.BaseType;
        }

        Assert.NotNull(type);

        PropertyInfo? disposed = type.GetProperty("Disposed", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(disposed);

        object? first = disposed.GetValue(container);
        object? second = disposed.GetValue(container);
        Assert.False(
            Assert.IsType<bool>(first),
            "First Disposed read should report false while latching the disposed flag.");
        Assert.True(
            Assert.IsType<bool>(second),
            "Second Disposed read should report true after the latch.");
    }
}

/// <summary>
/// Test-only reclaimer that fails Docker removal a fixed number of times, then delegates.
/// </summary>
internal sealed class ControllableDockerContainerReclaimer : IDockerContainerReclaimer
{
    private readonly IDockerContainerReclaimer inner;
    private int remainingRemoveFailures;

    public ControllableDockerContainerReclaimer(
        IDockerContainerReclaimer inner,
        int remainingRemoveFailures)
    {
        this.inner = inner;
        this.remainingRemoveFailures = remainingRemoveFailures;
    }

    public Task<bool> ExistsAsync(string containerId, CancellationToken cancellationToken = default) =>
        inner.ExistsAsync(containerId, cancellationToken);

    public Task RemoveForceAsync(string containerId, CancellationToken cancellationToken = default)
    {
        if (remainingRemoveFailures > 0)
        {
            remainingRemoveFailures--;
            return Task.FromException(
                new InvalidOperationException(
                    $"Deterministic injected container remove failure for '{containerId}'."));
        }

        return inner.RemoveForceAsync(containerId, cancellationToken);
    }
}
