using System.Net;
using Docker.DotNet;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class PostgreSqlFailureTests
{
    [Fact]
    public async Task UnreachableDockerEndpointIsAnExplicitStartupFailure()
    {
        PostgreSqlContainerFixture fixture = new("tcp://127.0.0.1:1");
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.InitializeAsync(timeout.Token));

        Assert.Contains("Failed to start and connect", failure.Message, StringComparison.Ordinal);
        Assert.Contains(PostgreSqlContainerFixture.ImageReference, failure.Message, StringComparison.Ordinal);
        Assert.NotNull(failure.InnerException);
        Assert.True(fixture.HasPendingContainerCleanup);
        Assert.NotNull(fixture.ContainerOwnershipId);

        InvalidOperationException retryFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.DisposeAsync());

        Assert.Contains("Independent Docker cleanup failed", retryFailure.Message, StringComparison.Ordinal);
        Assert.True(fixture.HasPendingContainerCleanup);
        Assert.NotNull(fixture.ContainerOwnershipId);
    }

    [Fact]
    public async Task FailedTestcontainersDisposeLatchesAndTheLabelOwnerPerformsFinalCleanup()
    {
        IDockerEndpointAuthenticationConfiguration dockerEndpoint = GetDockerEndpoint();
        string ownershipId = Guid.NewGuid().ToString("N");
        using IContainerResourceOwner resourceOwner = new DockerContainerResourceOwnerFactory(dockerEndpoint)
            .Create(PostgreSqlContainerFixture.ContainerOwnershipLabel, ownershipId);
        FailingDeleteContainer container = CreateFailingDeleteContainer(ownershipId, dockerEndpoint);
        string? containerId = null;

        try
        {
            await container.StartAsync();
            containerId = container.Id;

            Assert.Contains(containerId, await resourceOwner.GetContainerIdsAsync());

            InvalidOperationException firstFailure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => container.DisposeAsync().AsTask());

            Assert.Contains("intentional Docker removal failure", firstFailure.Message, StringComparison.Ordinal);
            Assert.True(await DockerContainerResourceProbe.ExistsAsync(containerId, dockerEndpoint));

            await container.DisposeAsync();

            Assert.True(await DockerContainerResourceProbe.ExistsAsync(containerId, dockerEndpoint));

            await resourceOwner.RemoveContainersAsync();

            Assert.Empty(await resourceOwner.GetContainerIdsAsync());
            Assert.False(await DockerContainerResourceProbe.ExistsAsync(containerId, dockerEndpoint));
        }
        finally
        {
            await resourceOwner.RemoveContainersAsync();
        }
    }

    [Fact]
    public async Task OwnershipLabelRemovesAnActualContainerWithoutCandidateId()
    {
        IDockerEndpointAuthenticationConfiguration dockerEndpoint = GetDockerEndpoint();
        string ownershipId = Guid.NewGuid().ToString("N");
        using IContainerResourceOwner resourceOwner = new DockerContainerResourceOwnerFactory(dockerEndpoint)
            .Create(PostgreSqlContainerFixture.ContainerOwnershipLabel, ownershipId);
        PartialCreateFailureContainer container =
            CreatePartialCreateFailureContainer(ownershipId, dockerEndpoint);
        string? observedContainerId = null;

        try
        {
            InvalidOperationException startupFailure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => container.StartAsync());

            Assert.Contains("intentional post-create failure", startupFailure.Message, StringComparison.Ordinal);
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = container.Id;
            });

            observedContainerId = Assert.Single(await resourceOwner.GetContainerIdsAsync());

            await container.DisposeAsync();

            Assert.True(await DockerContainerResourceProbe.ExistsAsync(observedContainerId, dockerEndpoint));

            // The owner receives only the create-before label identity, never candidate.Id.
            await resourceOwner.RemoveContainersAsync();

            Assert.Empty(await resourceOwner.GetContainerIdsAsync());
            Assert.False(await DockerContainerResourceProbe.ExistsAsync(observedContainerId, dockerEndpoint));

            await container.DisposeAsync();
        }
        finally
        {
            await resourceOwner.RemoveContainersAsync();
            await container.DisposeAsync();
        }
    }

    [Fact]
    public async Task CleanupFailuresRetainIdentityAndRetryBypassesThePoisonedInstance()
    {
        IDockerEndpointAuthenticationConfiguration dockerEndpoint = GetDockerEndpoint();
        PoisoningContainerDisposer disposer = new();
        PostgreSqlContainerFixture fixture = new(
            disposer,
            new FailOnceContainerResourceOwnerFactory(
                new DockerContainerResourceOwnerFactory(dockerEndpoint),
                dockerEndpoint));
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
                EnumerateExceptions(failure),
                exception => exception is DockerApiException { StatusCode: HttpStatusCode.Conflict });
            Assert.True(fixture.HasPendingContainerCleanup);
            Assert.NotNull(fixture.ContainerOwnershipId);
            Assert.Equal(1, disposer.CallCount);
            Assert.True(await DockerContainerResourceProbe.ExistsAsync(containerId, dockerEndpoint));

            await fixture.DisposeAsync();

            Assert.False(fixture.HasPendingContainerCleanup);
            Assert.Null(fixture.ContainerOwnershipId);
            Assert.Equal(1, disposer.CallCount);
            Assert.False(await DockerContainerResourceProbe.ExistsAsync(containerId, dockerEndpoint));
        }
        finally
        {
            await EnsureFixtureCleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task NativeFailureRemainsVisibleAfterIndependentCleanupSucceeds()
    {
        IDockerEndpointAuthenticationConfiguration dockerEndpoint = GetDockerEndpoint();
        PoisoningContainerDisposer disposer = new();
        PostgreSqlContainerFixture fixture = new(
            disposer,
            new DockerContainerResourceOwnerFactory(dockerEndpoint));
        string? containerId = null;

        try
        {
            await fixture.InitializeAsync();
            containerId = fixture.Container.Id;

            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.DisposeAsync());

            Assert.Contains("original cleanup failure remains visible", failure.Message, StringComparison.Ordinal);
            Assert.Contains(
                PoisoningContainerDisposer.FailureMessage,
                EnumerateExceptions(failure).Select(exception => exception.Message));
            Assert.False(fixture.HasPendingContainerCleanup);
            Assert.Null(fixture.ContainerOwnershipId);
            Assert.Equal(1, disposer.CallCount);
            Assert.False(await DockerContainerResourceProbe.ExistsAsync(containerId, dockerEndpoint));
        }
        finally
        {
            await EnsureFixtureCleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task IndependentFailureRetainsOwnerAfterNativeSuccessAndRetriesIndependently()
    {
        IDockerEndpointAuthenticationConfiguration dockerEndpoint = GetDockerEndpoint();
        SuccessfulNoOpContainerDisposer disposer = new();
        PostgreSqlContainerFixture fixture = new(
            disposer,
            new FailOnceContainerResourceOwnerFactory(
                new DockerContainerResourceOwnerFactory(dockerEndpoint),
                dockerEndpoint));
        string? containerId = null;

        try
        {
            await fixture.InitializeAsync();
            containerId = fixture.Container.Id;

            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.DisposeAsync());

            Assert.Contains("Independent Docker cleanup failed", failure.Message, StringComparison.Ordinal);
            Assert.Contains(
                EnumerateExceptions(failure),
                exception => exception is DockerApiException { StatusCode: HttpStatusCode.Conflict });
            Assert.True(fixture.HasPendingContainerCleanup);
            Assert.NotNull(fixture.ContainerOwnershipId);
            Assert.Equal(1, disposer.CallCount);
            Assert.True(await DockerContainerResourceProbe.ExistsAsync(containerId, dockerEndpoint));

            await fixture.DisposeAsync();

            Assert.False(fixture.HasPendingContainerCleanup);
            Assert.Null(fixture.ContainerOwnershipId);
            Assert.Equal(1, disposer.CallCount);
            Assert.False(await DockerContainerResourceProbe.ExistsAsync(containerId, dockerEndpoint));
        }
        finally
        {
            await EnsureFixtureCleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task ConcurrentCleanupCallsSerializeNativeDisposeAndIndependentRetry()
    {
        IDockerEndpointAuthenticationConfiguration dockerEndpoint = GetDockerEndpoint();
        SuccessfulNoOpContainerDisposer disposer = new();
        PostgreSqlContainerFixture fixture = new(
            disposer,
            new FailOnceContainerResourceOwnerFactory(
                new DockerContainerResourceOwnerFactory(dockerEndpoint),
                dockerEndpoint));
        string? containerId = null;

        try
        {
            await fixture.InitializeAsync();
            containerId = fixture.Container.Id;

            Task firstCleanup = fixture.DisposeAsync();
            Task concurrentCleanup = fixture.DisposeAsync();

            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => Task.WhenAll(firstCleanup, concurrentCleanup));

            Assert.Contains("Independent Docker cleanup failed", failure.Message, StringComparison.Ordinal);
            Assert.Contains(
                EnumerateExceptions(failure),
                exception => exception is DockerApiException { StatusCode: HttpStatusCode.Conflict });
            Assert.Equal(1, disposer.CallCount);
            Assert.False(fixture.HasPendingContainerCleanup);
            Assert.Null(fixture.ContainerOwnershipId);
            Assert.False(await DockerContainerResourceProbe.ExistsAsync(containerId, dockerEndpoint));
        }
        finally
        {
            await EnsureFixtureCleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task StartupAndCleanupFailuresRemainVisibleUntilIndependentRetryRemovesTheContainer()
    {
        const string startupFailureMessage = "Injected startup verification failure.";
        IDockerEndpointAuthenticationConfiguration dockerEndpoint = GetDockerEndpoint();
        PoisoningContainerDisposer disposer = new();
        string? containerId = null;
        PostgreSqlContainerFixture fixture = new(
            disposer,
            new FailOnceContainerResourceOwnerFactory(
                new DockerContainerResourceOwnerFactory(dockerEndpoint),
                dockerEndpoint),
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
            Assert.Contains(
                EnumerateExceptions(failure),
                exception => exception is DockerApiException { StatusCode: HttpStatusCode.Conflict });
            Assert.NotNull(containerId);
            Assert.True(fixture.HasPendingContainerCleanup);
            Assert.NotNull(fixture.ContainerOwnershipId);
            Assert.Equal(1, disposer.CallCount);
            Assert.True(await DockerContainerResourceProbe.ExistsAsync(containerId, dockerEndpoint));

            await fixture.DisposeAsync();

            Assert.False(fixture.HasPendingContainerCleanup);
            Assert.Null(fixture.ContainerOwnershipId);
            Assert.Equal(1, disposer.CallCount);
            Assert.False(await DockerContainerResourceProbe.ExistsAsync(containerId, dockerEndpoint));
        }
        finally
        {
            await EnsureFixtureCleanupAsync(fixture);
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

    private static IDockerEndpointAuthenticationConfiguration GetDockerEndpoint() =>
        TestcontainersSettings.OS.DockerEndpointAuthConfig
        ?? throw new InvalidOperationException(
            "Testcontainers could not resolve the Docker endpoint required by this integration test.");

    private static PartialCreateFailureContainer CreatePartialCreateFailureContainer(
        string ownershipId,
        IDockerEndpointAuthenticationConfiguration dockerEndpoint) =>
        new PartialCreateFailureContainerBuilder(PostgreSqlContainerFixture.ImageReference)
            .WithDockerEndpoint(dockerEndpoint)
            .WithEnvironment("POSTGRES_DB", "fixture_admin")
            .WithEnvironment("POSTGRES_PASSWORD", "test-only-password")
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithLabel(PostgreSqlContainerFixture.ContainerOwnershipLabel, ownershipId)
            .WithPortBinding(5432, true)
            .Build();

    private static FailingDeleteContainer CreateFailingDeleteContainer(
        string ownershipId,
        IDockerEndpointAuthenticationConfiguration dockerEndpoint) =>
        new FailingDeleteContainerBuilder(PostgreSqlContainerFixture.ImageReference)
            .WithDockerEndpoint(dockerEndpoint)
            .WithEnvironment("POSTGRES_DB", "fixture_admin")
            .WithEnvironment("POSTGRES_PASSWORD", "test-only-password")
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithLabel(PostgreSqlContainerFixture.ContainerOwnershipLabel, ownershipId)
            .WithPortBinding(5432, true)
            .Build();

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
        if (fixture.HasPendingContainerCleanup)
        {
            await fixture.DisposeAsync();
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

            // This models Testcontainers 4.13.0 after its disposed latch has been set.
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SuccessfulNoOpContainerDisposer : IPostgreSqlContainerDisposer
    {
        public int CallCount { get; private set; }

        public ValueTask DisposeAsync(PostgreSqlContainer container)
        {
            _ = container.Id;
            CallCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailOnceContainerResourceOwnerFactory(
        IContainerResourceOwnerFactory inner,
        IDockerEndpointAuthenticationConfiguration dockerEndpoint) : IContainerResourceOwnerFactory
    {
        public IContainerResourceOwner Create(string ownershipLabel, string ownershipId) =>
            new FailOnceContainerResourceOwner(
                inner.Create(ownershipLabel, ownershipId),
                dockerEndpoint);
    }

    private sealed class FailOnceContainerResourceOwner(
        IContainerResourceOwner inner,
        IDockerEndpointAuthenticationConfiguration dockerEndpoint) : IContainerResourceOwner
    {
        private int removeCallCount;

        public Task<IReadOnlyList<string>> GetContainerIdsAsync(
            CancellationToken cancellationToken = default) =>
            inner.GetContainerIdsAsync(cancellationToken);

        public async Task RemoveContainersAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref removeCallCount) == 1)
            {
                IReadOnlyList<string> containerIds = await inner.GetContainerIdsAsync(cancellationToken);
                string containerId = Assert.Single(containerIds);
                using DockerClient client = dockerEndpoint.GetDockerClientBuilder().Build();

                // Reach the Docker remove endpoint and deterministically receive daemon-side 409.
                await client.Containers.RemoveContainerAsync(
                    containerId,
                    new ContainerRemoveParameters
                    {
                        Force = false,
                        RemoveVolumes = true,
                    },
                    cancellationToken);

                throw new InvalidOperationException(
                    "Expected Docker to reject removal of the running container.");
            }

            await inner.RemoveContainersAsync(cancellationToken);
        }

        public void Dispose() => inner.Dispose();
    }

    private sealed class PartialCreateFailureContainer(IContainerConfiguration configuration)
        : DockerContainer(configuration)
    {
        private bool hideCreatedResource;

        protected override async Task UnsafeCreateAsync(CancellationToken cancellationToken = default)
        {
            await base.UnsafeCreateAsync(cancellationToken);
            hideCreatedResource = true;
            throw new InvalidOperationException("intentional post-create failure");
        }

        protected override bool Exists() => !hideCreatedResource && base.Exists();
    }

    private sealed class PartialCreateFailureContainerBuilder :
        ContainerBuilder<PartialCreateFailureContainerBuilder, PartialCreateFailureContainer, IContainerConfiguration>
    {
        public PartialCreateFailureContainerBuilder(string image)
            : this(new ContainerConfiguration())
        {
            DockerResourceConfiguration = Init().WithImage(image).DockerResourceConfiguration;
        }

        private PartialCreateFailureContainerBuilder(IContainerConfiguration configuration)
            : base(configuration)
        {
            DockerResourceConfiguration = configuration;
        }

        protected override IContainerConfiguration DockerResourceConfiguration { get; }

        public override PartialCreateFailureContainer Build()
        {
            Validate();
            return new PartialCreateFailureContainer(DockerResourceConfiguration);
        }

        protected override PartialCreateFailureContainerBuilder Init() => base.Init();

        protected override PartialCreateFailureContainerBuilder Clone(
            IResourceConfiguration<CreateContainerParameters> resourceConfiguration) =>
            Merge(DockerResourceConfiguration, new ContainerConfiguration(resourceConfiguration));

        protected override PartialCreateFailureContainerBuilder Clone(
            IContainerConfiguration resourceConfiguration) =>
            Merge(DockerResourceConfiguration, new ContainerConfiguration(resourceConfiguration));

        protected override PartialCreateFailureContainerBuilder Merge(
            IContainerConfiguration oldValue,
            IContainerConfiguration newValue) =>
            new(new ContainerConfiguration(oldValue, newValue));
    }

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
}
