using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class PostgreSqlContainerCleanupTests
{
    [Fact]
    public async Task ContainerCleanupFailureIsVisibleAndRetainsOwnershipUntilIndependentReclaimSucceeds()
    {
        DockerCliContainerReclaimer innerReclaimer = new();
        DelegatingDockerContainerReclaimer reclaimer = new(innerReclaimer);
        ScriptedContainerDisposeInvoker disposeInvoker = new();

        reclaimer.OnRemove = (_, attempt) =>
        {
            if (attempt == 1)
            {
                throw new InvalidOperationException("Injected deterministic Docker remove failure.");
            }

            return Task.CompletedTask;
        };

        disposeInvoker.OnInvoke = (_, attempt, _) =>
        {
            if (attempt == 1)
            {
                throw new InvalidOperationException("Injected Testcontainers dispose failure.");
            }

            return ValueTask.CompletedTask;
        };

        PostgreSqlContainerFixture fixture = new(
            dockerEndpoint: null,
            reclaimer: reclaimer,
            disposeInvoker: disposeInvoker);

        try
        {
            await fixture.InitializeAsync();
            string containerId = fixture.OwnedContainerId
                ?? throw new InvalidOperationException("Expected a started container id.");

            InvalidOperationException firstFailure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.DisposeAsync());

            Assert.Contains("Failed to reclaim the PostgreSQL test container", firstFailure.Message, StringComparison.Ordinal);
            Assert.Contains(containerId, firstFailure.Message, StringComparison.Ordinal);
            Assert.NotNull(firstFailure.InnerException);
            Assert.Equal(containerId, fixture.OwnedContainerId);
            Assert.True(await innerReclaimer.ExistsAsync(containerId));
            Assert.Equal(1, disposeInvoker.Attempts);
            Assert.Equal(1, reclaimer.RemoveAttempts);

            await fixture.DisposeAsync();

            Assert.Null(fixture.OwnedContainerId);
            Assert.False(await innerReclaimer.ExistsAsync(containerId));
            Assert.Equal(1, disposeInvoker.Attempts);
            Assert.Equal(2, reclaimer.RemoveAttempts);
        }
        finally
        {
            if (fixture.OwnedContainerId is not null)
            {
                await innerReclaimer.RemoveAsync(fixture.OwnedContainerId);
            }
        }
    }

    [Fact]
    public async Task PoisonedTestcontainersDisposeIsNotRetriedAndDoesNotFalseSuccess()
    {
        DockerCliContainerReclaimer innerReclaimer = new();
        DelegatingDockerContainerReclaimer reclaimer = new(innerReclaimer);
        ScriptedContainerDisposeInvoker disposeInvoker = new();

        disposeInvoker.OnInvoke = (container, attempt, _) =>
        {
            if (attempt == 1)
            {
                throw new InvalidOperationException("Injected Testcontainers dispose failure.");
            }

            return container.DisposeAsync();
        };

        PostgreSqlContainerFixture fixture = new(
            dockerEndpoint: null,
            reclaimer: reclaimer,
            disposeInvoker: disposeInvoker);

        try
        {
            await fixture.InitializeAsync();
            string containerId = fixture.OwnedContainerId
                ?? throw new InvalidOperationException("Expected a started container id.");

            await fixture.DisposeAsync();

            Assert.Null(fixture.OwnedContainerId);
            Assert.False(await innerReclaimer.ExistsAsync(containerId));
            Assert.Equal(1, disposeInvoker.Attempts);
            Assert.Equal(1, reclaimer.RemoveAttempts);
        }
        finally
        {
            if (fixture.OwnedContainerId is not null)
            {
                await innerReclaimer.RemoveAsync(fixture.OwnedContainerId);
            }
        }
    }

    [Fact]
    public async Task ActualDockerContainerIsRemovedAfterSuccessfulFixtureDispose()
    {
        DockerCliContainerReclaimer reclaimer = new();
        PostgreSqlContainerFixture fixture = new(
            dockerEndpoint: null,
            reclaimer: reclaimer,
            disposeInvoker: null);

        await fixture.InitializeAsync();
        string containerId = fixture.OwnedContainerId
            ?? throw new InvalidOperationException("Expected a started container id.");
        Assert.True(await reclaimer.ExistsAsync(containerId));

        await fixture.DisposeAsync();

        Assert.Null(fixture.OwnedContainerId);
        Assert.False(await reclaimer.ExistsAsync(containerId));
    }

    [Fact]
    public async Task StartupPrimaryFailureAndCleanupFailureAreBothPreserved()
    {
        DockerCliContainerReclaimer innerReclaimer = new();
        DelegatingDockerContainerReclaimer reclaimer = new(innerReclaimer);
        ScriptedContainerDisposeInvoker disposeInvoker = new();

        disposeInvoker.OnInvoke = (_, _, _) =>
            throw new InvalidOperationException("Injected Testcontainers dispose failure.");

        reclaimer.OnRemove = (_, _) =>
            throw new InvalidOperationException("Injected Docker remove failure.");

        PostgreSqlContainerFixture fixture = new(
            dockerEndpoint: null,
            reclaimer: reclaimer,
            disposeInvoker: disposeInvoker,
            failConnectionAfterContainerStartForTests: true);

        try
        {
            InvalidOperationException startupFailure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.InitializeAsync());

            Assert.Contains("Failed to start and connect", startupFailure.Message, StringComparison.Ordinal);
            AggregateException aggregate = Assert.IsType<AggregateException>(startupFailure.InnerException);
            Assert.Equal(2, aggregate.InnerExceptions.Count);
            Assert.Contains(
                aggregate.InnerExceptions,
                exception => exception.Message.Contains("Intentional post-start failure", StringComparison.Ordinal));
            Assert.Contains(
                aggregate.InnerExceptions,
                exception => exception.Message.Contains("Failed to reclaim the PostgreSQL test container", StringComparison.Ordinal));

            string containerId = fixture.OwnedContainerId
                ?? throw new InvalidOperationException("Expected retained container ownership after startup cleanup failure.");
            Assert.True(await innerReclaimer.ExistsAsync(containerId));

            disposeInvoker.OnInvoke = null;
            reclaimer.OnRemove = null;

            await fixture.DisposeAsync();
            Assert.Null(fixture.OwnedContainerId);
            Assert.False(await innerReclaimer.ExistsAsync(containerId));
        }
        finally
        {
            if (fixture.OwnedContainerId is not null)
            {
                await innerReclaimer.RemoveAsync(fixture.OwnedContainerId);
            }
        }
    }

    private sealed class DelegatingDockerContainerReclaimer(IDockerContainerReclaimer inner) : IDockerContainerReclaimer
    {
        public int RemoveAttempts { get; private set; }

        public Func<string, int, Task>? OnRemove { get; set; }

        public Task<bool> ExistsAsync(string containerId, CancellationToken cancellationToken = default) =>
            inner.ExistsAsync(containerId, cancellationToken);

        public async Task RemoveAsync(string containerId, CancellationToken cancellationToken = default)
        {
            RemoveAttempts++;

            if (OnRemove is not null)
            {
                await OnRemove(containerId, RemoveAttempts);
            }

            await inner.RemoveAsync(containerId, cancellationToken);
        }
    }

    private sealed class ScriptedContainerDisposeInvoker : IContainerDisposeInvoker
    {
        public int Attempts { get; private set; }

        public Func<PostgreSqlContainer, int, CancellationToken, ValueTask>? OnInvoke { get; set; }

        public ValueTask InvokeAsync(PostgreSqlContainer container, CancellationToken cancellationToken = default)
        {
            Attempts++;

            if (OnInvoke is null)
            {
                return container.DisposeAsync();
            }

            return OnInvoke(container, Attempts, cancellationToken);
        }
    }
}
