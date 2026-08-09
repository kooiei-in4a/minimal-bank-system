using System.Diagnostics;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Removes a Docker container by id through the <c>docker</c> CLI, independent of any
/// Testcontainers container instance. Testcontainers latches its internal disposed state before
/// resource removal is confirmed (see <see cref="PostgreSqlContainerFixture" />), so a poisoned
/// Testcontainers instance can never be trusted to retry its own cleanup. This type talks to the
/// same Docker daemon directly by container id, so cleanup ownership survives independently of
/// that instance. <see cref="PostgreSqlFailureTests.DockerContainerCleanupForceRemovesAndVerifiesRealDaemonState" />
/// verifies its behavior against a genuine daemon.
/// </summary>
internal static class DockerContainerCleanup
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    internal static async Task<bool> ExistsAsync(string containerId, CancellationToken cancellationToken)
    {
        DockerCommandResult result = await RunDockerCommandAsync(
            ["inspect", "--type", "container", containerId],
            cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    /// <summary>
    /// Force-removes the container and verifies the daemon no longer reports it. Returns
    /// <see langword="true" /> only when the container is actually gone afterward, regardless of
    /// the exit code the removal command itself reported.
    /// </summary>
    internal static async Task<bool> TryForceRemoveAsync(string containerId, CancellationToken cancellationToken)
    {
        _ = await RunDockerCommandAsync(
            ["rm", "--force", "--volumes", containerId],
            cancellationToken).ConfigureAwait(false);
        return !await ExistsAsync(containerId, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<DockerCommandResult> RunDockerCommandAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = new(CommandTimeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);

        ProcessStartInfo startInfo = new("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };

        try
        {
            process.Start();

            Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(linked.Token);
            Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(linked.Token);
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);

            string standardOutput = await standardOutputTask.ConfigureAwait(false);
            string standardError = await standardErrorTask.ConfigureAwait(false);
            return new DockerCommandResult(process.ExitCode, standardOutput, standardError);
        }
        catch (Exception exception) when (exception is OperationCanceledException or System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException(
                $"Failed to run 'docker {string.Join(' ', arguments)}'.",
                exception);
        }
    }

    private readonly record struct DockerCommandResult(int ExitCode, string StandardOutput, string StandardError);
}
