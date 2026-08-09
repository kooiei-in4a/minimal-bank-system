namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class PostgreSqlCleanupFailureTests
{
    [Fact]
    public async Task ContainerDisposalFailureIsVisibleAndIdIsRetainedForFinalCleanup()
    {
        await using PostgreSqlContainerFixture fixture = new();
        await fixture.InitializeAsync();

        string containerId = fixture.ContainerId
            ?? throw new InvalidOperationException("Expected the container to have a captured Id before disposal.");

        FailableContainerActivator.ArmPostgreSqlContainerDisposeFailure(fixture.Container);

        Exception caught = null!;
        try
        {
            await fixture.DisposeAsync();
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);
        Assert.IsType<InvalidOperationException>(caught);
        Assert.NotNull(caught.InnerException);

        Assert.True(
            fixture.IsContainerDisposalFailed,
            "Disposal did not fail; failed state was not retained.");

        Assert.Contains("Failed to dispose the PostgreSQL test container", caught.Message, StringComparison.Ordinal);
        Assert.Contains(PostgreSqlContainerFixture.ImageReference, caught.Message, StringComparison.Ordinal);
        Assert.Contains("retains ownership", caught.Message, StringComparison.Ordinal);

        Assert.True(
            string.Equals(containerId, fixture.ContainerId, StringComparison.Ordinal),
            "ContainerId was not retained after a failed dispose.");
        Assert.False(fixture.IsContainerFinalized);

        Assert.True(
            await ContainerResourceInspector.ExistsAsync(containerId, default),
            "Container is no longer on the daemon after the failed dispose, but the retained Id must correspond to a still-existing resource until the final cleanup path runs.");

        await fixture.ForceContainerRemoveAsync(default);
        Assert.False(await ContainerResourceInspector.ExistsAsync(containerId, default));

        fixture.ReleaseContainerForTest();
        Assert.True(fixture.IsContainerFinalized);
    }

    [Fact]
    public async Task SameInstanceDisposeIsNoOpAfterFirstFailureAndDoesNotMaskTheOriginalFailure()
    {
        await using PostgreSqlContainerFixture fixture = new();
        await fixture.InitializeAsync();

        string containerId = fixture.ContainerId
            ?? throw new InvalidOperationException("Expected the container to have a captured Id before disposal.");

        FailableContainerActivator.ArmPostgreSqlContainerDisposeFailure(fixture.Container);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.DisposeAsync());

        await fixture.DisposeAsync();
        await fixture.DisposeAsync();

        Assert.True(fixture.IsContainerDisposalFailed);
        Assert.Equal(containerId, fixture.ContainerId);

        await fixture.ForceContainerRemoveAsync(default);
        fixture.ReleaseContainerForTest();
    }

    [Fact]
    public async Task FinalCleanupPathRemovesTheOrphanedResourceIndependentlyOfTheTestcontainersInstance()
    {
        await using PostgreSqlContainerFixture fixture = new();
        await fixture.InitializeAsync();

        string containerId = fixture.ContainerId
            ?? throw new InvalidOperationException("Expected the container to have a captured Id before disposal.");

        FailableContainerActivator.ArmPostgreSqlContainerDisposeFailure(fixture.Container);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.DisposeAsync());

        await fixture.ForceContainerRemoveAsync(default);

        Assert.False(await ContainerResourceInspector.ExistsAsync(containerId, default));
        Assert.Equal(containerId, fixture.ContainerId);

        fixture.ReleaseContainerForTest();
    }

    [Fact]
    public async Task StartupPrimaryFailureIsReportedAlongsideContainerDisposalFailure()
    {
        PostgreSqlContainerFixture fixture = new("tcp://127.0.0.1:1");

        InvalidOperationException startupFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.InitializeAsync());

        Assert.Contains("Failed to start and connect", startupFailure.Message, StringComparison.Ordinal);
        Assert.NotNull(startupFailure.InnerException);

        Assert.False(fixture.IsContainerDisposalFailed);
        Assert.Null(fixture.ContainerId);
    }
}
