using Docker.DotNet;
using DotNet.Testcontainers.Configurations;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Deterministic failure-injection tests for the container cleanup lifecycle. They run against
/// <see cref="FakeDockerDaemon" />, an in-process Docker Engine API stub, so they require no
/// Docker daemon. They are not in the <c>PostgreSqlIntegration</c> category for that reason.
/// The stub replaces the process-global <see cref="TestcontainersSettings.ResourceReaperEnabled" />
/// flag, so this class runs in a serialized collection.
/// </summary>
[Collection(TestExecutionCollections.DockerCleanupFailureInjection)]
public sealed class PostgreSqlContainerCleanupFailureTests
{
    [Fact]
    public async Task StartupFailureAndContainerCleanupFailureAreBothVisibleAndOwnershipIsRetained()
    {
        await using FakeDockerDaemon daemon = new();
        daemon.FailContainerRemoval = true;

        bool reaperWasEnabled = TestcontainersSettings.ResourceReaperEnabled;
        TestcontainersSettings.ResourceReaperEnabled = false;

        try
        {
            PostgreSqlContainerFixture fixture = new(daemon.Endpoint);

            try
            {
                InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => fixture.InitializeAsync());

                Assert.Contains("Failed to start and connect", failure.Message, StringComparison.Ordinal);

                AggregateException aggregate = Assert.IsType<AggregateException>(failure.InnerException);
                Assert.Collection(
                    aggregate.InnerExceptions,
                    startup => Assert.Contains("PostgreSQL connection failed", startup.Message, StringComparison.Ordinal),
                    cleanup => Assert.Contains("injected container removal failure", cleanup.Message, StringComparison.Ordinal));

                Assert.Equal(daemon.ContainerId, fixture.PendingContainerId);

                await fixture.Container.DisposeAsync();
                Assert.True(daemon.ContainerExists);
            }
            finally
            {
                daemon.FailContainerRemoval = false;
                await fixture.DisposeAsync();
            }

            Assert.False(daemon.ContainerExists);
            Assert.Null(fixture.PendingContainerId);
        }
        finally
        {
            TestcontainersSettings.ResourceReaperEnabled = reaperWasEnabled;
        }
    }

    [Fact]
    public async Task FailedRemovalRetryPerformsARealDockerCallAndKeepsOwnershipUntilTheDaemonConfirmsRemoval()
    {
        await using FakeDockerDaemon daemon = new();
        daemon.FailContainerRemoval = true;

        bool reaperWasEnabled = TestcontainersSettings.ResourceReaperEnabled;
        TestcontainersSettings.ResourceReaperEnabled = false;

        try
        {
            PostgreSqlContainerFixture fixture = new(daemon.Endpoint);

            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.InitializeAsync());
                int attemptsAfterStartupFailure = daemon.ContainerRemoveAttempts;
                Assert.Equal(1, attemptsAfterStartupFailure);

                InvalidOperationException retryFailure = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => fixture.DisposeAsync());

                Assert.Contains("direct Docker API cleanup path", retryFailure.Message, StringComparison.Ordinal);
                Assert.Equal(attemptsAfterStartupFailure + 1, daemon.ContainerRemoveAttempts);
                Assert.True(daemon.ContainerExists);
                Assert.Equal(daemon.ContainerId, fixture.PendingContainerId);

                daemon.FailContainerRemoval = false;
                await fixture.DisposeAsync();

                Assert.False(daemon.ContainerExists);
                Assert.Null(fixture.PendingContainerId);
            }
            finally
            {
                await fixture.DisposeAsync();
            }
        }
        finally
        {
            TestcontainersSettings.ResourceReaperEnabled = reaperWasEnabled;
        }
    }

    [Fact]
    public async Task StartedContainerDisposeFailureIsVisibleAndFinalCleanupNeverUsesThePoisonedInstance()
    {
        await using FakeDockerDaemon daemon = new();
        daemon.FailContainerRemoval = true;

        bool reaperWasEnabled = TestcontainersSettings.ResourceReaperEnabled;
        TestcontainersSettings.ResourceReaperEnabled = false;

        try
        {
            PostgreSqlContainer candidate = new PostgreSqlBuilder(PostgreSqlContainerFixture.ImageReference)
                .WithDatabase("postgres")
                .WithUsername("postgres")
                .WithPassword("test-only-stub-password")
                .WithDockerEndpoint(daemon.Endpoint)
                .Build();

            await candidate.StartAsync();

            PostgreSqlContainerFixture fixture = new(candidate, daemon.Endpoint);

            try
            {
                InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => fixture.DisposeAsync());

                Assert.Contains("Failed to dispose the PostgreSQL test container", failure.Message, StringComparison.Ordinal);
                Assert.True(daemon.ContainerExists);
                Assert.Equal(daemon.ContainerId, fixture.PendingContainerId);

                await fixture.Container.DisposeAsync();
                Assert.True(daemon.ContainerExists);

                int attempts = daemon.ContainerRemoveAttempts;
                Assert.Equal(1, attempts);

                InvalidOperationException retryFailure = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => fixture.DisposeAsync());

                Assert.Contains("direct Docker API cleanup path", retryFailure.Message, StringComparison.Ordinal);
                Assert.Equal(attempts + 1, daemon.ContainerRemoveAttempts);
                Assert.True(daemon.ContainerExists);
                Assert.Equal(daemon.ContainerId, fixture.PendingContainerId);

                daemon.FailContainerRemoval = false;
                await fixture.DisposeAsync();

                Assert.False(daemon.ContainerExists);
                Assert.Null(fixture.PendingContainerId);
            }
            finally
            {
                await fixture.DisposeAsync();
            }
        }
        finally
        {
            TestcontainersSettings.ResourceReaperEnabled = reaperWasEnabled;
        }
    }
}
