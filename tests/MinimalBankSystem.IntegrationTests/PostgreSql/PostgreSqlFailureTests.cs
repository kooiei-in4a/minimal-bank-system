using Npgsql;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class PostgreSqlFailureTests
{
    [Fact]
    public async Task CleanupFailuresRetainIdentityAndRetryBypassesThePoisonedInstance()
    {
        PoisoningContainerDisposer disposer = new();
        PostgreSqlContainerFixture fixture = new(
            disposer,
            new FailOnceContainerResourceOwnerFactory(
                new DockerContainerResourceOwnerFactory()));
        string? containerId = null;

        try
        {
            await fixture.InitializeAsync();
            containerId = fixture.Container.Id;

            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.DisposeAsync());

            Assert.Contains("and independent Docker cleanup also failed", failure.Message, StringComparison.Ordinal);
            Assert.Contains(
                PoisoningContainerDisposer.FailureMessage,
                EnumerateExceptions(failure).Select(exception => exception.Message));
            Assert.Contains(
                FailOnceContainerResourceOwner.FailureMessage,
                EnumerateExceptions(failure).Select(exception => exception.Message));
            Assert.True(fixture.HasPendingContainerCleanup);
            Assert.Equal(1, disposer.CallCount);
            Assert.True(await DockerContainerResourceProbe.ExistsAsync(containerId));

            await fixture.DisposeAsync();

            Assert.False(fixture.HasPendingContainerCleanup);
            Assert.Equal(1, disposer.CallCount);
            Assert.False(await DockerContainerResourceProbe.ExistsAsync(containerId));
        }
        finally
        {
            if (fixture.HasPendingContainerCleanup)
            {
                await EnsureFixtureCleanupAsync(fixture);
            }
        }
    }

    [Fact]
    public async Task StartupAndCleanupFailuresRemainVisibleUntilIndependentRetryRemovesTheContainer()
    {
        const string startupFailureMessage = "Injected startup verification failure.";
        PoisoningContainerDisposer disposer = new();
        string? containerId = null;
        PostgreSqlContainerFixture fixture = new(
            disposer,
            new FailOnceContainerResourceOwnerFactory(
                new DockerContainerResourceOwnerFactory()),
            (candidate, _) =>
            {
                containerId = candidate.Id;
                return Task.FromException<int>(new InvalidOperationException(startupFailureMessage));
            });

        try
        {
            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.InitializeAsync());

            string[] failureMessages = EnumerateExceptions(failure)
                .Select(exception => exception.Message)
                .ToArray();

            Assert.Contains("Failed to start and connect", failure.Message, StringComparison.Ordinal);
            Assert.Contains(startupFailureMessage, failureMessages);
            Assert.Contains(PoisoningContainerDisposer.FailureMessage, failureMessages);
            Assert.Contains(FailOnceContainerResourceOwner.FailureMessage, failureMessages);
            Assert.NotNull(containerId);
            Assert.True(fixture.HasPendingContainerCleanup);
            Assert.Equal(1, disposer.CallCount);
            Assert.True(await DockerContainerResourceProbe.ExistsAsync(containerId));

            await fixture.DisposeAsync();

            Assert.False(fixture.HasPendingContainerCleanup);
            Assert.Equal(1, disposer.CallCount);
            Assert.False(await DockerContainerResourceProbe.ExistsAsync(containerId));
        }
        finally
        {
            if (fixture.HasPendingContainerCleanup)
            {
                await EnsureFixtureCleanupAsync(fixture);
            }
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

    private static IEnumerable<Exception> EnumerateExceptions(Exception exception)
    {
        yield return exception;

        if (exception is AggregateException aggregateException)
        {
            foreach (Exception innerException in aggregateException.InnerExceptions)
            {
                foreach (Exception nestedException in EnumerateExceptions(innerException))
                {
                    yield return nestedException;
                }
            }
        }
        else if (exception.InnerException is not null)
        {
            foreach (Exception nestedException in EnumerateExceptions(exception.InnerException))
            {
                yield return nestedException;
            }
        }
    }

    private static async Task EnsureFixtureCleanupAsync(PostgreSqlContainerFixture fixture)
    {
        try
        {
            await fixture.DisposeAsync();
        }
        finally
        {
            if (fixture.HasPendingContainerCleanup)
            {
                await fixture.DisposeAsync();
            }
        }
    }

    private sealed class PoisoningContainerDisposer : IPostgreSqlContainerDisposer
    {
        public const string FailureMessage = "Injected Testcontainers disposal failure.";

        public int CallCount { get; private set; }

        public ValueTask DisposeAsync(PostgreSqlContainer container)
        {
            _ = container.Id;
            CallCount++;

            if (CallCount == 1)
            {
                throw new IOException(FailureMessage);
            }

            // Models Testcontainers 4.13.0 after its disposed latch has already been set:
            // a second call returns without attempting Docker removal.
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailOnceContainerResourceOwnerFactory(
        IContainerResourceOwnerFactory inner) : IContainerResourceOwnerFactory
    {
        public IContainerResourceOwner Create(string ownershipLabel, string ownershipId) =>
            new FailOnceContainerResourceOwner(inner.Create(ownershipLabel, ownershipId));
    }

    private sealed class FailOnceContainerResourceOwner(
        IContainerResourceOwner inner) : IContainerResourceOwner
    {
        public const string FailureMessage = "Injected independent Docker removal failure.";

        private int removeCallCount;

        public Task<IReadOnlyList<string>> GetContainerIdsAsync(
            CancellationToken cancellationToken = default) =>
            inner.GetContainerIdsAsync(cancellationToken);

        public Task RemoveContainersAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref removeCallCount) == 1)
            {
                throw new IOException(FailureMessage);
            }

            return inner.RemoveContainersAsync(cancellationToken);
        }

        public void Dispose() => inner.Dispose();
    }
}
