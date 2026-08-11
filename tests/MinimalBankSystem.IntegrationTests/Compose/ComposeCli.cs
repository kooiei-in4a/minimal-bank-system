using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MinimalBankSystem.IntegrationTests.Persistence;

namespace MinimalBankSystem.IntegrationTests.Compose;

internal sealed class ComposeCli
{
    private readonly string projectName;
    private readonly string workingDirectory;
    private readonly IReadOnlyDictionary<string, string> environment;

    public ComposeCli(
        string projectName,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment)
    {
        this.projectName = projectName;
        this.workingDirectory = workingDirectory;
        this.environment = environment;
    }

    public string ProjectName => projectName;

    public async Task<ComposeCommandResult> ConfigQuietAsync(CancellationToken cancellationToken = default) =>
        await RunAsync(["config", "--quiet"], cancellationToken: cancellationToken);

    public async Task<string> ConfigJsonAsync(CancellationToken cancellationToken = default)
    {
        ComposeCommandResult result = await RunAsync(
            ["config", "--format", "json"],
            cancellationToken: cancellationToken);
        Assert.True(
            result.ExitCode == 0,
            $"compose config --format json failed ({result.ExitCode}):\n{result.Output}");
        return result.StandardOutput;
    }

    public async Task<ComposeCommandResult> UpBuildDetachAsync(CancellationToken cancellationToken = default) =>
        await RunAsync(
            ["up", "--build", "--detach", "--remove-orphans"],
            cancellationToken: cancellationToken);

    public async Task<ComposeCommandResult> DownRetainDataAsync(CancellationToken cancellationToken = default) =>
        await RunAsync(["down", "--remove-orphans"], cancellationToken: cancellationToken);

    public async Task<ComposeCommandResult> DownCleanResetAsync(CancellationToken cancellationToken = default) =>
        await RunAsync(["down", "--volumes", "--remove-orphans"], cancellationToken: cancellationToken);

    public async Task<string> PsJsonAsync(CancellationToken cancellationToken = default)
    {
        ComposeCommandResult result = await RunAsync(
            ["ps", "-a", "--format", "json"],
            cancellationToken: cancellationToken);
        Assert.True(
            result.ExitCode == 0,
            $"compose ps failed ({result.ExitCode}):\n{result.Output}");
        return result.StandardOutput;
    }

    public async Task<string> LogsAsync(string service, CancellationToken cancellationToken = default)
    {
        ComposeCommandResult result = await RunAsync(
            ["logs", "--no-color", "--timestamps", service],
            cancellationToken: cancellationToken);
        // Logs may return non-zero when the service never started; still return captured text.
        return result.Output;
    }

    public async Task<ComposeCommandResult> RunAsync(
        IReadOnlyList<string> composeArguments,
        IReadOnlyDictionary<string, string>? extraEnvironment = null,
        IReadOnlyList<string>? extraComposeFiles = null,
        CancellationToken cancellationToken = default)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "docker",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("compose");
        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add(projectName);
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add(ComposeContracts.ComposeFileName);

        if (extraComposeFiles is not null)
        {
            foreach (string file in extraComposeFiles)
            {
                startInfo.ArgumentList.Add("-f");
                startInfo.ArgumentList.Add(file);
            }
        }

        foreach (string argument in composeArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach ((string key, string value) in environment)
        {
            startInfo.Environment[key] = value;
        }

        if (extraEnvironment is not null)
        {
            foreach ((string key, string value) in extraEnvironment)
            {
                startInfo.Environment[key] = value;
            }
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start docker compose.");

        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new ComposeCommandResult(
            process.ExitCode,
            await stdout,
            await stderr);
    }
}

internal sealed record ComposeCommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    public string Output => new StringBuilder(StandardOutput).Append(StandardError).ToString();
}

internal static class DockerCli
{
    public static async Task<ComposeCommandResult> RunRawAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "docker",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = RepositoryLayout.RepositoryRoot.FullName,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start docker.");

        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new ComposeCommandResult(process.ExitCode, await stdout, await stderr);
    }

    public static async Task<JsonDocument> InspectContainerAsync(
        string containerId,
        CancellationToken cancellationToken = default)
    {
        ComposeCommandResult result = await RunRawAsync(
            ["inspect", containerId],
            cancellationToken);
        Assert.True(result.ExitCode == 0, $"docker inspect failed:\n{result.Output}");
        return JsonDocument.Parse(result.StandardOutput);
    }

    public static async Task<ComposeCommandResult> VolumeInspectAsync(
        string volumeName,
        CancellationToken cancellationToken = default) =>
        await RunRawAsync(["volume", "inspect", volumeName], cancellationToken);

    public static async Task<ComposeCommandResult> VolumeLsAsync(
        CancellationToken cancellationToken = default) =>
        await RunRawAsync(["volume", "ls", "--format", "{{.Name}}"], cancellationToken);

    public static async Task<string> ContainerArgsAsync(
        string containerId,
        CancellationToken cancellationToken = default)
    {
        ComposeCommandResult result = await RunRawAsync(
            ["inspect", "--format", "{{json .Args}}", containerId],
            cancellationToken);
        Assert.True(result.ExitCode == 0, $"docker inspect Args failed:\n{result.Output}");
        return result.StandardOutput.Trim();
    }

    public static async Task<string> ContainerCmdAsync(
        string containerId,
        CancellationToken cancellationToken = default)
    {
        ComposeCommandResult result = await RunRawAsync(
            ["inspect", "--format", "{{json .Config.Cmd}} {{json .Config.Entrypoint}}", containerId],
            cancellationToken);
        Assert.True(result.ExitCode == 0, $"docker inspect Cmd failed:\n{result.Output}");
        return result.StandardOutput.Trim();
    }
}
