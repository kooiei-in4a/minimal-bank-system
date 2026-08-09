using Npgsql;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class PostgreSqlFailureTests
{
    [Fact]
    public async Task NativeContainerDisposeFailureFallsBackToIndependentCleanupAndSurfacesTheOriginalFailure()
    {
        // T-01: inject a deterministic container cleanup failure. A real Docker-daemon
        // connectivity fault (severed mid-lifecycle) is the genuine cause in production, but
        // reproducing that reliably needs a custom network relay in front of the daemon transport;
        // an attempt at that was dropped after repeated intermittent connection resets made it
        // unusable as a deterministic test (see PR discussion). Injecting the failure at the two
        // I/O boundaries CleanupContainerAsync owns — the native Testcontainers dispose call and
        // the independent DockerContainerCleanup fallback — exercises the exact same production
        // orchestration deterministically and without flakiness. A companion test
        // (DockerContainerCleanupForceRemovesAndVerifiesRealDaemonState) separately proves the
        // fallback's real-Docker mechanics against a genuine daemon.
        int nativeDisposeCallCount = 0;
        int forceRemoveCallCount = 0;
        InvalidOperationException simulatedNativeFailure = new("simulated Testcontainers dispose failure");

        PostgreSqlContainer container = new PostgreSqlBuilder(PostgreSqlContainerFixture.ImageReference).Build();
        PostgreSqlContainerFixture fixture = new(
            container,
            containerId: "simulated-container-id",
            containerDisposeOverride: () =>
            {
                nativeDisposeCallCount++;
                throw simulatedNativeFailure;
            },
            forceRemoveOverride: (id, cancellationToken) =>
            {
                forceRemoveCallCount++;
                Assert.Equal("simulated-container-id", id);
                return Task.FromResult(true);
            });

        // T-02: the first container cleanup failure propagates to the caller.
        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.DisposeAsync());

        Assert.Contains("Testcontainers reported a failure", failure.Message, StringComparison.Ordinal);
        Assert.Contains("simulated-container-id", failure.Message, StringComparison.Ordinal);
        Assert.Contains("verified removed", failure.Message, StringComparison.Ordinal);
        Assert.Same(simulatedNativeFailure, failure.InnerException);
        Assert.Equal(1, nativeDisposeCallCount);
        Assert.Equal(1, forceRemoveCallCount);

        // T-03/R-02: a further dispose call must not re-invoke the poisoned native path — that
        // would assume a second DisposeAsync() on the same Testcontainers instance can retry
        // cleanup, which the 4.13.0 disposed-state latch makes false. Ownership was already
        // released above once the fallback verified removal, so this call is an inert no-op.
        await fixture.DisposeAsync();
        Assert.Equal(1, nativeDisposeCallCount);
        Assert.Equal(1, forceRemoveCallCount);
    }

    [Fact]
    public async Task DoubleCleanupFailureRetainsOwnershipAndNeverRetriesThePoisonedNativeDispose()
    {
        // T-05-adjacent: covers the case the primary failure-path test above does not — both the
        // native dispose and the independent fallback fail on the same attempt — and confirms a
        // later retry succeeds through the fallback alone, still never touching the poisoned
        // native instance again (R-02/R-03/R-04).
        int nativeDisposeCallCount = 0;
        int forceRemoveCallCount = 0;
        bool secondForceRemoveSucceeds = false;

        PostgreSqlContainer container = new PostgreSqlBuilder(PostgreSqlContainerFixture.ImageReference).Build();
        PostgreSqlContainerFixture fixture = new(
            container,
            containerId: "simulated-container-id",
            containerDisposeOverride: () =>
            {
                nativeDisposeCallCount++;
                throw new InvalidOperationException("simulated Testcontainers dispose failure");
            },
            forceRemoveOverride: (id, cancellationToken) =>
            {
                forceRemoveCallCount++;
                return Task.FromResult(secondForceRemoveSucceeds);
            });

        InvalidOperationException firstFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.DisposeAsync());

        Assert.Contains("through both", firstFailure.Message, StringComparison.Ordinal);
        Assert.Contains("retains ownership", firstFailure.Message, StringComparison.Ordinal);
        Assert.Equal(1, nativeDisposeCallCount);
        Assert.Equal(1, forceRemoveCallCount);

        secondForceRemoveSucceeds = true;

        // The retry succeeds through the independent fallback alone; the native Testcontainers
        // instance — already known to be poisoned — is never disposed a second time.
        await fixture.DisposeAsync();

        Assert.Equal(1, nativeDisposeCallCount);
        Assert.Equal(2, forceRemoveCallCount);
    }

    [Fact]
    public async Task DockerContainerCleanupForceRemovesAndVerifiesRealDaemonState()
    {
        // T-04 (option A), against a genuine Docker daemon rather than a double: prove
        // DockerContainerCleanup — the independent low-level path CleanupContainerAsync falls back
        // to — actually observes and changes real container state, not just a wrapper field.
        PostgreSqlContainerFixture fixture = new();

        using (CancellationTokenSource startTimeout = new(TimeSpan.FromSeconds(60)))
        {
            await fixture.InitializeAsync(startTimeout.Token);
        }

        string containerId = fixture.ContainerId
            ?? throw new InvalidOperationException("The fixture did not capture a Docker container id after start-up.");

        using (CancellationTokenSource existsTimeout = new(TimeSpan.FromSeconds(30)))
        {
            Assert.True(await DockerContainerCleanup.ExistsAsync(containerId, existsTimeout.Token));
        }

        using (CancellationTokenSource removeTimeout = new(TimeSpan.FromSeconds(30)))
        {
            Assert.True(await DockerContainerCleanup.TryForceRemoveAsync(containerId, removeTimeout.Token));
        }

        using (CancellationTokenSource verifyTimeout = new(TimeSpan.FromSeconds(30)))
        {
            Assert.False(await DockerContainerCleanup.ExistsAsync(containerId, verifyTimeout.Token));
        }

        // The container is already gone; Testcontainers' own live existence pre-check
        // (TestcontainersClient.RemoveAsync/StopAsync) recognizes that and disposes cleanly.
        await fixture.DisposeAsync();
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
}
