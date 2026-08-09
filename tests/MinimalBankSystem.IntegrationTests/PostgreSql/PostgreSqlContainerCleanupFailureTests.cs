using System.Diagnostics;
using System.Reflection;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class PostgreSqlContainerCleanupFailureTests
{
    [Fact]
    public async Task PoisonedTestcontainersInstanceIsNotReusedForCleanup()
    {
        PostgreSqlContainerFixture fixture = new();
        await fixture.InitializeAsync();

        string? containerId = fixture.RetainedContainerId;
        Assert.NotNull(containerId);

        PostgreSqlContainer testcontainersInstance = fixture.Container;

        PoisonResourceDisposedFlag(testcontainersInstance);

        DateTime beforeSecondCall = DateTime.UtcNow;
        await testcontainersInstance.DisposeAsync();
        DateTime afterSecondCall = DateTime.UtcNow;
        double elapsedMs = (afterSecondCall - beforeSecondCall).TotalMilliseconds;

        Assert.True(elapsedMs < 500,
            $"DisposeAsync after poisoning should be a no-op (elapsed {elapsedMs:F0}ms).");

        Assert.NotNull(fixture.RetainedContainerId);
        Assert.Equal(containerId, fixture.RetainedContainerId);

        Assert.True(await DockerContainerExistsAsync(containerId),
            $"Container {containerId} should still exist after no-op DisposeAsync.");

        await PostgreSqlContainerFixture.ForceRemoveContainerAsync(containerId);

        Assert.False(await DockerContainerExistsAsync(containerId),
            $"Container {containerId} should be removed after direct Docker cleanup.");
    }

    [Fact]
    public async Task CleanupFailureIsVisibleAndContainerIdentityIsPreserved()
    {
        PostgreSqlContainerFixture fixture = new();
        await fixture.InitializeAsync();

        string? containerId = fixture.RetainedContainerId;
        Assert.NotNull(containerId);

        PostgreSqlContainer testcontainersInstance = fixture.Container;

        PoisonResourceDisposedFlag(testcontainersInstance);

        await testcontainersInstance.DisposeAsync();

        Assert.NotNull(fixture.RetainedContainerId);
        Assert.Equal(containerId, fixture.RetainedContainerId);

        Assert.True(await DockerContainerExistsAsync(containerId));

        await fixture.DisposeAsync();

        Assert.False(await DockerContainerExistsAsync(containerId));
    }

    [Fact]
    public async Task PoisonedInstanceCleanupFallsBackToDockerDirectRemoval()
    {
        PostgreSqlContainerFixture fixture = new();
        await fixture.InitializeAsync();

        string? containerId = fixture.RetainedContainerId;
        Assert.NotNull(containerId);
        Assert.True(await DockerContainerExistsAsync(containerId));

        PostgreSqlContainer testcontainersInstance = fixture.Container;

        PoisonResourceDisposedFlag(testcontainersInstance);

        await testcontainersInstance.DisposeAsync();

        Assert.NotNull(fixture.RetainedContainerId);
        Assert.True(await DockerContainerExistsAsync(containerId));

        await fixture.DisposeAsync();

        Assert.Null(fixture.RetainedContainerId);
        Assert.False(await DockerContainerExistsAsync(containerId));
    }

    [Fact]
    public async Task CleanupFailureDoesNotBreakDatabaseLifecycle()
    {
        PostgreSqlContainerFixture fixture = new();

        await fixture.InitializeAsync();

        PostgreSqlTestDatabase database = await fixture.CreateDatabaseAsync();
        Assert.True(await fixture.DatabaseExistsAsync(database.DatabaseName));

        await database.DisposeAsync();
        Assert.False(await fixture.DatabaseExistsAsync(database.DatabaseName));

        PostgreSqlTestDatabase another = await fixture.CreateDatabaseAsync();
        Assert.NotEqual(database.DatabaseName, another.DatabaseName);
        Assert.True(await fixture.DatabaseExistsAsync(another.DatabaseName));

        await another.DisposeAsync();
        Assert.False(await fixture.DatabaseExistsAsync(another.DatabaseName));

        await fixture.DisposeAsync();
    }

    [Fact]
    public async Task ContainerDoesNotRemainAfterNormalCleanup()
    {
        PostgreSqlContainerFixture fixture = new();
        await fixture.InitializeAsync();

        string? containerId = fixture.RetainedContainerId;
        Assert.NotNull(containerId);
        Assert.True(await DockerContainerExistsAsync(containerId));

        await fixture.DisposeAsync();

        Assert.False(await DockerContainerExistsAsync(containerId));
        Assert.Null(fixture.RetainedContainerId);
    }

    [Fact]
    public async Task StartupFailureDoesNotLeaveOrphanedContainerAfterFailedCleanup()
    {
        string unreachableEndpoint = "tcp://127.0.0.1:1";
        PostgreSqlContainerFixture fixture = new(unreachableEndpoint);

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.InitializeAsync());

        Assert.Contains("Failed to start and connect", failure.Message, StringComparison.Ordinal);
        Assert.Contains(PostgreSqlContainerFixture.ImageReference, failure.Message, StringComparison.Ordinal);
        Assert.NotNull(failure.InnerException);

        Assert.Null(fixture.RetainedContainerId);
    }

    [Fact]
    public async Task RepeatedDisposeAfterCleanupDoesNotThrow()
    {
        PostgreSqlContainerFixture fixture = new();
        await fixture.InitializeAsync();

        string? containerId = fixture.RetainedContainerId;
        Assert.NotNull(containerId);

        await fixture.DisposeAsync();
        Assert.Null(fixture.RetainedContainerId);

        await fixture.DisposeAsync();
        Assert.Null(fixture.RetainedContainerId);
    }

    private static void PoisonResourceDisposedFlag(PostgreSqlContainer container)
    {
        Type? resourceType = typeof(PostgreSqlContainer).BaseType?.BaseType;
        Assert.NotNull(resourceType);

        FieldInfo? field = resourceType.GetField(
            "_disposed",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(field);

        field.SetValue(container, 1);
    }

    private static async Task<bool> DockerContainerExistsAsync(string containerId)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"inspect --format '{{{{.Id}}}}' {containerId}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();
        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }
}
