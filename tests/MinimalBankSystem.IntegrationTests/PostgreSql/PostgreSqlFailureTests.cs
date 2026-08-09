using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class PostgreSqlFailureTests
{
    [Fact]
    public async Task ContainerCleanupFailureRetainsIndependentOwnerUntilActualRemoval()
    {
        int disposeCalls = 0;
        PostgreSqlContainerFixture fixture = new(
            dockerEndpoint: null,
            disposeContainer: _ =>
            {
                disposeCalls++;
                return ValueTask.FromException(
                    new InvalidOperationException("injected poisoned Testcontainers dispose failure"));
            },
            cleanupFactory: (resourceId, endpoint) =>
                new FailFirstContainerCleanup(new DockerContainerCleanup(resourceId, endpoint)));

        await fixture.InitializeAsync();
        string resourceId = fixture.Container.Id;

        try
        {
            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.DisposeAsync());

            Assert.Contains("injected poisoned Testcontainers dispose failure", failure.ToString(), StringComparison.Ordinal);
            Assert.Equal(1, disposeCalls);
            Assert.Equal(resourceId, fixture.PendingContainerResourceId);
            Assert.True(fixture.HasPendingContainerCleanup);
            Assert.True(await DockerContainerCleanup.ExistsAsync(resourceId, null));

            await fixture.DisposeAsync();

            Assert.Equal(1, disposeCalls);
            Assert.Null(fixture.PendingContainerResourceId);
            Assert.False(fixture.HasPendingContainerCleanup);
            Assert.False(await DockerContainerCleanup.ExistsAsync(resourceId, null));
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task StartupFailureAndPartialCleanupFailureRemainVisibleWithOwnerRetained()
    {
        int disposeCalls = 0;
        PostgreSqlContainerFixture fixture = new(
            dockerEndpoint: null,
            startupValidation: static (_, _) =>
                Task.FromException(new InvalidOperationException("injected startup primary failure")),
            disposeContainer: _ =>
            {
                disposeCalls++;
                return ValueTask.FromException(
                    new InvalidOperationException("injected poisoned Testcontainers dispose failure"));
            },
            cleanupFactory: (resourceId, endpoint) =>
                new FailFirstContainerCleanup(new DockerContainerCleanup(resourceId, endpoint)));

        try
        {
            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.InitializeAsync());

            Assert.Contains("injected startup primary failure", failure.ToString(), StringComparison.Ordinal);
            Assert.Contains("injected poisoned Testcontainers dispose failure", failure.ToString(), StringComparison.Ordinal);
            Assert.Contains("injected deterministic container cleanup failure", failure.ToString(), StringComparison.Ordinal);
            Assert.Equal(1, disposeCalls);

            string? resourceId = fixture.PendingContainerResourceId;
            Assert.NotNull(resourceId);
            Assert.True(fixture.HasPendingContainerCleanup);
            Assert.True(await DockerContainerCleanup.ExistsAsync(resourceId!, null));

            await fixture.DisposeAsync();

            Assert.Null(fixture.PendingContainerResourceId);
            Assert.False(fixture.HasPendingContainerCleanup);
            Assert.False(await DockerContainerCleanup.ExistsAsync(resourceId!, null));
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task UnreachableDockerEndpointIsAnExplicitStartupFailure()
    {
        PostgreSqlContainerFixture fixture = new("tcp://127.0.0.1:1");

        try
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));

            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.InitializeAsync(timeout.Token));

            Assert.Contains("Failed to start and connect", failure.Message, StringComparison.Ordinal);
            Assert.Contains(PostgreSqlContainerFixture.ImageReference, failure.Message, StringComparison.Ordinal);
            Assert.NotNull(failure.InnerException);
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task UnreachablePostgreSqlEndpointIsAnExplicitConnectionFailure()
    {
        NpgsqlConnectionStringBuilder unreachable = new()
        {
            Host = "127.0.0.1",
            Port = 1,
            Database = "postgres",
            Username = "postgres",
            Password = "test-only-unused",
            Pooling = false,
            Timeout = 2,
            CommandTimeout = 2,
        };

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => PostgreSqlContainerFixture.OpenConnectionAsync(
                unreachable.ConnectionString,
                "running the intentional connection failure verification"));

        Assert.Contains("PostgreSQL connection failed", failure.Message, StringComparison.Ordinal);
        Assert.Contains("intentional connection failure", failure.Message, StringComparison.Ordinal);
        Assert.NotNull(failure.InnerException);
    }

    private sealed class FailFirstContainerCleanup(IContainerCleanup inner) : IContainerCleanup
    {
        private int attempts;

        public string ResourceId => inner.ResourceId;

        public Task RemoveAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref attempts, 1) == 0)
            {
                return Task.FromException(
                    new InvalidOperationException("injected deterministic container cleanup failure"));
            }

            return inner.RemoveAsync(cancellationToken);
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
