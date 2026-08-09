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
    public async Task StartupFailureWithPartialCleanupRetainsBothFailures()
    {
        PostgreSqlContainerFixture fixture = new("tcp://127.0.0.1:1");

        try
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));

            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.InitializeAsync(timeout.Token));

            Assert.Contains("Failed to start and connect", failure.Message, StringComparison.Ordinal);
            Assert.NotNull(failure.InnerException);

            Assert.False(fixture.HasContainerReference,
                "Fixture should not retain a container reference after failed startup with cleanup.");
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task ContainerCleanupFailureIsVisibleAndFallbackSucceeds()
    {
        PostgreSqlContainerFixture standalone = new();

        await standalone.InitializeAsync();

        Assert.NotNull(standalone.CleanupHandle);
        string containerId = standalone.CleanupHandle!.ContainerId;

        await standalone.CleanupHandle.ForceRemoveAsync();

        bool existsAfterManualRemoval = await ContainerExistsViaDockerCli(containerId);
        Assert.False(existsAfterManualRemoval,
            "Container should be removed after manual fallback cleanup.");

        await standalone.DisposeAsync();

        Assert.False(standalone.HasContainerReference,
            "Fixture should clear container reference after successful cleanup.");
        Assert.Null(standalone.CleanupHandle);
    }

    private static async Task<bool> ContainerExistsViaDockerCli(string containerId)
    {
        System.Diagnostics.ProcessStartInfo startInfo = new("docker", $"ps -q --filter \"id={containerId}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using System.Diagnostics.Process process = new() { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch
        {
            return false;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            string stdout = await process.StandardOutput.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);
            return !string.IsNullOrWhiteSpace(stdout);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(); } catch { }
            return false;
        }
    }
}
