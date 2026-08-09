using System.Diagnostics;
using System.Text;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

internal sealed class DockerCliContainerReclaimer : IDockerContainerReclaimer
{
    private readonly string? dockerHost;

    public DockerCliContainerReclaimer(string? dockerHost = null)
    {
        this.dockerHost = dockerHost;
    }

    public Task<bool> ExistsAsync(string containerId, CancellationToken cancellationToken = default) =>
        InspectAsync(containerId, cancellationToken);

    public async Task RemoveAsync(string containerId, CancellationToken cancellationToken = default)
    {
        ProcessResult result = await RunDockerAsync(
            $"rm -f {containerId}",
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to remove Docker container '{containerId}'. docker rm exited with code {result.ExitCode}: {result.StandardError}");
        }
    }

    private async Task<bool> InspectAsync(string containerId, CancellationToken cancellationToken)
    {
        ProcessResult result = await RunDockerAsync(
            $"inspect -f \"{{{{.Id}}}}\" {containerId}",
            cancellationToken);

        return result.ExitCode == 0;
    }

    private async Task<ProcessResult> RunDockerAsync(string arguments, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "docker",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (dockerHost is not null)
        {
            startInfo.Environment["DOCKER_HOST"] = dockerHost;
        }

        using Process process = new() { StartInfo = startInfo };
        StringBuilder standardOutput = new();
        StringBuilder standardError = new();

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                standardOutput.AppendLine(eventArgs.Data);
            }
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                standardError.AppendLine(eventArgs.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start the docker CLI for container reclamation.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return new ProcessResult(
            process.ExitCode,
            standardOutput.ToString().Trim(),
            standardError.ToString().Trim());
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
