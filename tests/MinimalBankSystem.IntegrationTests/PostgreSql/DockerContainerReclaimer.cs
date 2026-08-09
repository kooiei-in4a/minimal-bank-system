using System.Diagnostics;
using System.Text;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Independent Docker resource cleanup that does not rely on a Testcontainers instance.
/// </summary>
internal interface IDockerContainerReclaimer
{
    Task RemoveForceAsync(string containerId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string containerId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reclaims containers through the Docker CLI so cleanup ownership survives a poisoned
/// Testcontainers <c>DisposeAsync</c> latch.
/// </summary>
internal sealed class CliDockerContainerReclaimer : IDockerContainerReclaimer
{
    public Task RemoveForceAsync(string containerId, CancellationToken cancellationToken = default) =>
        RunDockerAsync(["rm", "-f", "--", containerId], treatMissingContainerAsSuccess: true, cancellationToken);

    public async Task<bool> ExistsAsync(string containerId, CancellationToken cancellationToken = default)
    {
        DockerCommandResult result = await RunDockerAsync(
            ["inspect", "--type", "container", "--", containerId],
            treatMissingContainerAsSuccess: true,
            cancellationToken);

        // Missing containers are normalized to exit code 0 above; distinguish by stderr text.
        if (IsMissingContainer(result.Stderr, result.Stdout))
        {
            return false;
        }

        return result.ExitCode == 0;
    }

    private static async Task<DockerCommandResult> RunDockerAsync(
        string[] arguments,
        bool treatMissingContainerAsSuccess,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "docker",
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

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start the Docker CLI for container reclaim.");
        }

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        string stdout = await stdoutTask;
        string stderr = await stderrTask;

        if (process.ExitCode == 0)
        {
            return new DockerCommandResult(process.ExitCode, stdout, stderr);
        }

        if (treatMissingContainerAsSuccess && IsMissingContainer(stderr, stdout))
        {
            return new DockerCommandResult(0, stdout, stderr);
        }

        StringBuilder message = new();
        message.Append("Docker CLI command failed with exit code ")
            .Append(process.ExitCode)
            .Append(": docker ")
            .Append(string.Join(' ', arguments));

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            message.Append(" — ").Append(stderr.Trim());
        }

        throw new InvalidOperationException(message.ToString());
    }

    private static bool IsMissingContainer(string stderr, string stdout)
    {
        string combined = string.Concat(stderr, Environment.NewLine, stdout);
        return combined.Contains("No such container", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record DockerCommandResult(int ExitCode, string Stdout, string Stderr);
}
