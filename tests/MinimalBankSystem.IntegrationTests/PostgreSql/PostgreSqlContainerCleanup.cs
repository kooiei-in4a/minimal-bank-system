using System.Diagnostics;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

internal interface IPostgreSqlTestContainer : IAsyncDisposable
{
    IImage Image { get; }

    string Id { get; }

    string GetConnectionString();

    Task StartAsync(CancellationToken cancellationToken);
}

internal sealed class TestcontainersPostgreSqlTestContainer(PostgreSqlContainer container)
    : IPostgreSqlTestContainer
{
    public IImage Image => container.Image;

    public string Id => container.Id;

    public string GetConnectionString() => container.GetConnectionString();

    public Task StartAsync(CancellationToken cancellationToken) => container.StartAsync(cancellationToken);

    public ValueTask DisposeAsync() => container.DisposeAsync();
}

internal interface IContainerResourceCleanup
{
    Task<bool> ExistsAsync(string containerId, CancellationToken cancellationToken = default);

    Task RemoveAsync(string containerId, CancellationToken cancellationToken = default);
}

internal sealed class DockerCliContainerResourceCleanup(string? dockerEndpoint)
    : IContainerResourceCleanup
{
    public async Task<bool> ExistsAsync(string containerId, CancellationToken cancellationToken = default)
    {
        ValidateContainerId(containerId);

        int inspectExitCode = await RunDockerAsync(
            ["container", "inspect", containerId],
            cancellationToken);

        if (inspectExitCode == 0)
        {
            return true;
        }

        await EnsureDockerIsAvailableAsync(cancellationToken);
        return false;
    }

    public async Task RemoveAsync(string containerId, CancellationToken cancellationToken = default)
    {
        ValidateContainerId(containerId);

        if (!await ExistsAsync(containerId, cancellationToken))
        {
            return;
        }

        int removeExitCode = await RunDockerAsync(
            ["container", "rm", "--force", containerId],
            cancellationToken);

        if (removeExitCode != 0 && await ExistsAsync(containerId, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Independent Docker cleanup failed for PostgreSQL test container '{containerId}' " +
                $"with exit code {removeExitCode}.");
        }

        if (await ExistsAsync(containerId, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Independent Docker cleanup completed without removing PostgreSQL test container '{containerId}'.");
        }
    }

    private async Task EnsureDockerIsAvailableAsync(CancellationToken cancellationToken)
    {
        int versionExitCode = await RunDockerAsync(
            ["version", "--format", "{{.Server.Version}}"],
            cancellationToken);

        if (versionExitCode != 0)
        {
            throw new InvalidOperationException(
                "Docker could not confirm the final state of the PostgreSQL test container.");
        }
    }

    private async Task<int> RunDockerAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new("docker")
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        if (dockerEndpoint is not null)
        {
            startInfo.Environment["DOCKER_HOST"] = dockerEndpoint;
        }

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };

        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start the Docker CLI for PostgreSQL test cleanup.");
        }

        Task readStandardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task readStandardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await Task.WhenAll(process.WaitForExitAsync(cancellationToken), readStandardOutput, readStandardError);
        return process.ExitCode;
    }

    private static void ValidateContainerId(string containerId)
    {
        if (containerId.Length is < 12 or > 64 ||
            containerId.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException(
                "Refusing to run independent cleanup for an invalid PostgreSQL test container ID.");
        }
    }
}

internal sealed class ContainerCleanupOwner(
    Func<ValueTask> disposeTestcontainersInstanceAsync,
    IContainerResourceCleanup resourceCleanup) : IDisposable
{
    private readonly SemaphoreSlim cleanupGate = new(1, 1);
    private string? containerId;
    private bool testcontainersDisposeAttempted;

    internal bool HasOutstandingResource => containerId is not null;

    internal bool IsReleased => containerId is null;

    internal void CaptureContainer(string value)
    {
        if (containerId is not null && !StringComparer.Ordinal.Equals(containerId, value))
        {
            throw new InvalidOperationException("PostgreSQL test container cleanup ownership cannot change containers.");
        }

        containerId = value;
    }

    internal async Task DisposeAsync(CancellationToken cancellationToken = default)
    {
        await cleanupGate.WaitAsync(CancellationToken.None);

        try
        {
            Exception? testcontainersDisposeException = null;

            if (!testcontainersDisposeAttempted)
            {
                testcontainersDisposeAttempted = true;

                try
                {
                    await disposeTestcontainersInstanceAsync();
                }
                catch (Exception exception)
                {
                    testcontainersDisposeException = exception;
                }

                if (testcontainersDisposeException is null)
                {
                    containerId = null;
                    return;
                }
            }

            if (containerId is null)
            {
                if (testcontainersDisposeException is not null)
                {
                    throw new InvalidOperationException(
                        "Testcontainers failed to dispose a PostgreSQL test container before its resource identity was available.",
                        testcontainersDisposeException);
                }

                return;
            }

            try
            {
                await resourceCleanup.RemoveAsync(containerId, cancellationToken);
            }
            catch (Exception cleanupException)
            {
                if (testcontainersDisposeException is not null)
                {
                    throw new AggregateException(testcontainersDisposeException, cleanupException);
                }

                throw new InvalidOperationException(
                    $"Independent cleanup failed for PostgreSQL test container '{containerId}'.",
                    cleanupException);
            }

            containerId = null;

            if (testcontainersDisposeException is not null)
            {
                throw new InvalidOperationException(
                    "Testcontainers failed to dispose the PostgreSQL test container. " +
                    "An independent Docker cleanup path removed and verified the container.",
                    testcontainersDisposeException);
            }
        }
        finally
        {
            cleanupGate.Release();
        }
    }

    public void Dispose() => cleanupGate.Dispose();
}
