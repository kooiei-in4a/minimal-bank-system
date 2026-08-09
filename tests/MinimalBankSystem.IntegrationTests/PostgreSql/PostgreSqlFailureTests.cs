using Docker.DotNet;
using Docker.DotNet.Models;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class PostgreSqlFailureTests
{
    [Fact]
    public async Task FailedContainerDisposeKeepsAnIndependentOwnerAndNeverRetriesThePoisonedInstance()
    {
        FakeContainerResourceStore resourceStore = new();
        PoisonedTestContainerHandle container = new(resourceStore);
        FailOnceContainerResourceCleanup cleanup = new(resourceStore);
        ContainerResourceOwner owner = new(container);

        Exception? firstFailure = await owner.CleanupAsync(cleanup, CancellationToken.None);

        Assert.NotNull(firstFailure);
        Assert.Contains("injected container removal failure", firstFailure.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, container.DisposeCalls);
        Assert.True(resourceStore.Exists(container.ResourceIdentity));
        Assert.False(owner.IsFinalized);

        Exception? finalFailure = await owner.CleanupAsync(cleanup, CancellationToken.None);

        Assert.Null(finalFailure);
        Assert.Equal(1, container.DisposeCalls);
        Assert.Equal(2, cleanup.Attempts);
        Assert.False(resourceStore.Exists(container.ResourceIdentity));
        Assert.True(owner.IsFinalized);
    }

    [Fact]
    public async Task StartupAndPartialCleanupFailuresRemainVisibleAndIndependentOwnershipSurvives()
    {
        FakeContainerResourceStore resourceStore = new();
        PoisonedTestContainerHandle container = new(resourceStore, failStartup: true);
        FailOnceContainerResourceCleanup cleanup = new(resourceStore);
        PostgreSqlContainerFixture fixture = new(container, cleanup);

        try
        {
            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.InitializeAsync(CancellationToken.None));

            Assert.Contains("Failed to start and connect", failure.Message, StringComparison.Ordinal);
            Assert.Contains("startup primary failure", failure.ToString(), StringComparison.Ordinal);
            Assert.Contains("injected container removal failure", failure.ToString(), StringComparison.Ordinal);
            Assert.True(resourceStore.Exists(container.ResourceIdentity));

            await fixture.DisposeAsync();

            Assert.Equal(1, container.DisposeCalls);
            Assert.False(resourceStore.Exists(container.ResourceIdentity));
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task SuccessfulContainerCleanupRemovesTheActualDockerResource()
    {
        PostgreSqlContainerFixture fixture = new();
        string? resourceId = null;

        try
        {
            await fixture.InitializeAsync(CancellationToken.None);
            resourceId = fixture.Container.Id;

            await fixture.DisposeAsync();

            using DockerClient client = new DockerClientBuilder().Build();
            await Assert.ThrowsAsync<DockerContainerNotFoundException>(
                () => client.Containers.InspectContainerAsync(resourceId));
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

    private sealed class PoisonedTestContainerHandle : ITestContainerHandle
    {
        private bool disposed;

        private readonly bool failStartup;

        public PoisonedTestContainerHandle(
            FakeContainerResourceStore resourceStore,
            bool failStartup = false)
        {
            this.failStartup = failStartup;
            resourceStore.Add(ResourceIdentity);
        }

        public string ResourceIdentity { get; } = $"fake-container-{Guid.NewGuid():N}";

        public string? ResourceId => ResourceIdentity;

        public int DisposeCalls { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken) => failStartup
            ? Task.FromException(new InvalidOperationException("startup primary failure"))
            : Task.CompletedTask;

        public string GetConnectionString() => string.Empty;

        public ValueTask DisposeAsync()
        {
            DisposeCalls++;

            if (disposed)
            {
                return ValueTask.CompletedTask;
            }

            disposed = true;
            throw new InvalidOperationException(
                "injected container removal failure after the Testcontainers disposed-state latch");
        }
    }

    private sealed class FakeContainerResourceStore
    {
        private readonly HashSet<string> resources = [];

        public void Add(string resourceIdentity) => resources.Add(resourceIdentity);

        public bool Exists(string resourceIdentity) => resources.Contains(resourceIdentity);

        public void Remove(string resourceIdentity) => resources.Remove(resourceIdentity);
    }

    private sealed class FailOnceContainerResourceCleanup(
        FakeContainerResourceStore resourceStore) : IContainerResourceCleanup
    {
        public int Attempts { get; private set; }

        public Task RemoveAndVerifyAsync(
            string resourceReference,
            CancellationToken cancellationToken)
        {
            Attempts++;

            if (Attempts == 1)
            {
                return Task.FromException(
                    new InvalidOperationException("injected container removal failure"));
            }

            resourceStore.Remove(resourceReference);
            Assert.False(resourceStore.Exists(resourceReference));
            return Task.CompletedTask;
        }
    }
}
