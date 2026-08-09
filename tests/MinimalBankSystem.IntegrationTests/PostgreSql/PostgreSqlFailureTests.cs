using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class PostgreSqlFailureTests
{
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

    [Fact]
    public async Task FailedTestcontainersDisposeDoesNotRetryTheActualDockerRemoval()
    {
        DockerCliContainerResourceCleanup resourceCleanup = new(null);
        FailingDeleteContainer container = CreateFailingDeleteContainer();
        string? containerId = null;

        try
        {
            await container.StartAsync();
            containerId = container.Id;

            InvalidOperationException firstFailure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => container.DisposeAsync().AsTask());

            Assert.Contains("intentional Docker removal failure", firstFailure.Message, StringComparison.Ordinal);
            Assert.True(await resourceCleanup.ExistsAsync(containerId));

            await container.DisposeAsync();

            Assert.True(await resourceCleanup.ExistsAsync(containerId));

            await resourceCleanup.RemoveAsync(containerId);

            Assert.False(await resourceCleanup.ExistsAsync(containerId));
        }
        finally
        {
            if (containerId is not null && await resourceCleanup.ExistsAsync(containerId))
            {
                await resourceCleanup.RemoveAsync(containerId);
            }
        }
    }

    [Fact]
    public async Task CleanupOwnerReportsTheOriginalFailureAfterIndependentDockerCleanup()
    {
        DockerCliContainerResourceCleanup resourceCleanup = new(null);
        FailingDeleteContainer container = CreateFailingDeleteContainer();
        string? containerId = null;

        try
        {
            await container.StartAsync();
            containerId = container.Id;
            using ContainerCleanupOwner cleanupOwner = new(container.DisposeAsync, resourceCleanup);
            cleanupOwner.CaptureContainer(containerId);

            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => cleanupOwner.DisposeAsync());

            Assert.Contains("Testcontainers failed to dispose", failure.Message, StringComparison.Ordinal);
            Assert.NotNull(failure.InnerException);
            Assert.True(cleanupOwner.IsReleased);
            Assert.False(await resourceCleanup.ExistsAsync(containerId));
        }
        finally
        {
            if (containerId is not null && await resourceCleanup.ExistsAsync(containerId))
            {
                await resourceCleanup.RemoveAsync(containerId);
            }
        }
    }

    [Fact]
    public async Task StartupFailureRetainsCleanupOwnershipUntilIndependentRetrySucceeds()
    {
        InvalidOperationException startupFailure = new("intentional startup failure");
        FailingStartupContainer candidate = new(startupFailure);
        FailingOnceContainerResourceCleanup resourceCleanup = new();
        PostgreSqlContainerFixture fixture = new(() => candidate, resourceCleanup);

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.InitializeAsync());

        Assert.Contains("Failed to start and connect", failure.Message, StringComparison.Ordinal);
        Assert.Contains("intentional startup failure", failure.ToString(), StringComparison.Ordinal);
        Assert.Contains("intentional cleanup failure", failure.ToString(), StringComparison.Ordinal);
        Assert.True(fixture.HasOutstandingContainerCleanup);
        Assert.Equal(1, candidate.DisposeCallCount);

        await fixture.DisposeAsync();

        Assert.False(fixture.HasOutstandingContainerCleanup);
        Assert.Equal(1, candidate.DisposeCallCount);
        Assert.False(resourceCleanup.ResourceExists);
    }

    private static FailingDeleteContainer CreateFailingDeleteContainer() =>
        new FailingDeleteContainerBuilder(PostgreSqlContainerFixture.ImageReference)
            .WithEnvironment("POSTGRES_DB", "fixture_admin")
            .WithEnvironment("POSTGRES_PASSWORD", "test-only-password")
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithPortBinding(5432, true)
            .Build();

    private sealed class FailingDeleteContainer(IContainerConfiguration configuration)
        : DockerContainer(configuration)
    {
        protected override Task UnsafeDeleteAsync(CancellationToken cancellationToken = default) =>
            Task.FromException(new InvalidOperationException("intentional Docker removal failure"));
    }

    private sealed class FailingDeleteContainerBuilder :
        ContainerBuilder<FailingDeleteContainerBuilder, FailingDeleteContainer, IContainerConfiguration>
    {
        public FailingDeleteContainerBuilder(string image)
            : this(new ContainerConfiguration())
        {
            DockerResourceConfiguration = Init().WithImage(image).DockerResourceConfiguration;
        }

        private FailingDeleteContainerBuilder(IContainerConfiguration configuration)
            : base(configuration)
        {
            DockerResourceConfiguration = configuration;
        }

        protected override IContainerConfiguration DockerResourceConfiguration { get; }

        public override FailingDeleteContainer Build()
        {
            Validate();
            return new FailingDeleteContainer(DockerResourceConfiguration);
        }

        protected override FailingDeleteContainerBuilder Init() => base.Init();

        protected override FailingDeleteContainerBuilder Clone(
            IResourceConfiguration<CreateContainerParameters> resourceConfiguration) =>
            Merge(DockerResourceConfiguration, new ContainerConfiguration(resourceConfiguration));

        protected override FailingDeleteContainerBuilder Clone(
            IContainerConfiguration resourceConfiguration) =>
            Merge(DockerResourceConfiguration, new ContainerConfiguration(resourceConfiguration));

        protected override FailingDeleteContainerBuilder Merge(
            IContainerConfiguration oldValue,
            IContainerConfiguration newValue) =>
            new(new ContainerConfiguration(oldValue, newValue));
    }

    private sealed class FailingStartupContainer(Exception startupFailure) : IPostgreSqlTestContainer
    {
        private const string ContainerId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        public int DisposeCallCount { get; private set; }

        public IImage Image { get; } = new DockerImage(PostgreSqlContainerFixture.ImageReference);

        public string Id => ContainerId;

        public string GetConnectionString() => throw new NotSupportedException();

        public Task StartAsync(CancellationToken cancellationToken) => Task.FromException(startupFailure);

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;
            return ValueTask.FromException(new InvalidOperationException("intentional cleanup failure"));
        }
    }

    private sealed class FailingOnceContainerResourceCleanup : IContainerResourceCleanup
    {
        public bool ResourceExists { get; private set; } = true;

        public Task<bool> ExistsAsync(string containerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ResourceExists);

        public Task RemoveAsync(string containerId, CancellationToken cancellationToken = default)
        {
            if (ResourceExists)
            {
                ResourceExists = false;
                return Task.FromException(new InvalidOperationException("intentional cleanup failure"));
            }

            return Task.CompletedTask;
        }
    }
}
