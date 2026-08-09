using System.Diagnostics;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

internal static class ContainerResourceInspector
{
    public static async Task<bool> ExistsAsync(string containerId, CancellationToken cancellationToken)
    {
        (int exitCode, _, _) = await RunDockerAsync(
            new[] { "inspect", containerId },
            cancellationToken).ConfigureAwait(false);
        return exitCode == 0;
    }

    public static async Task RemoveForceAsync(string containerId, CancellationToken cancellationToken)
    {
        (int exitCode, _, string stdErr) = await RunDockerAsync(
            new[] { "rm", "-f", containerId },
            cancellationToken).ConfigureAwait(false);

        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"docker rm -f {containerId} failed with exit code {exitCode}: {stdErr.Trim()}");
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunDockerAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        string fileName = ResolveDockerExecutable();

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                $"Failed to start '{fileName}' for container resource inspection.");

        Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return (process.ExitCode, await stdOutTask, await stdErrTask);
    }

    private static string ResolveDockerExecutable()
    {
        string? dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (!string.IsNullOrEmpty(dockerHost))
        {
            return "docker";
        }

        if (OperatingSystem.IsWindows())
        {
            string[] windowsCandidates =
            [
                @"C:\Program Files\Docker\Docker\resources\bin\docker.exe",
                @"C:\Program Files\Docker\cli-resources\bin\docker.exe",
            ];
            foreach (string candidate in windowsCandidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return "docker";
    }
}
