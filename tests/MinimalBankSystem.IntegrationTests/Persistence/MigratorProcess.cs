using System.Diagnostics;
using System.Text;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.IntegrationTests.Persistence;

internal static class MigratorProcess
{
    private const string MigratorAssemblyName = "MinimalBankSystem.Migrator";
    private const string ApiRuntimeRoleEnvironmentVariable = "MBS_API_RUNTIME_ROLE";

    public static async Task<MigratorRun> RunAsync(
        string? connectionString,
        TimeSpan waitBudget,
        string? apiRuntimeRoleName = null)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = ResolveDotnetHost(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = RepositoryLayout.RepositoryRoot.FullName,
        };

        startInfo.ArgumentList.Add(ResolveMigratorAssemblyPath());

        if (connectionString is null)
        {
            startInfo.Environment.Remove(BankPersistence.ConnectionStringEnvironmentVariable);
        }
        else
        {
            startInfo.Environment[BankPersistence.ConnectionStringEnvironmentVariable] = connectionString;
        }

        if (string.IsNullOrWhiteSpace(apiRuntimeRoleName))
        {
            startInfo.Environment.Remove(ApiRuntimeRoleEnvironmentVariable);
        }
        else
        {
            startInfo.Environment[ApiRuntimeRoleEnvironmentVariable] = apiRuntimeRoleName;
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the migrator process.");

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();

        long startedAt = Stopwatch.GetTimestamp();
        using CancellationTokenSource wait = new(waitBudget);

        try
        {
            await process.WaitForExitAsync(wait.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException(
                $"The migrator did not exit within {waitBudget}. A bounded migration must terminate on its own.");
        }

        return new MigratorRun(
            process.ExitCode,
            await standardOutput,
            await standardError,
            Stopwatch.GetElapsedTime(startedAt));
    }

    internal static string ResolveDotnetHost() =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } hostPath
            ? hostPath
            : "dotnet";

    private static string ResolveMigratorAssemblyPath()
    {
        string path = Path.Combine(
            RepositoryLayout.RepositoryRoot.FullName,
            "src",
            MigratorAssemblyName,
            "bin",
            RepositoryLayout.BuildConfiguration,
            RepositoryLayout.TargetFramework,
            $"{MigratorAssemblyName}.dll");

        return File.Exists(path)
            ? path
            : throw new InvalidOperationException(
                $"The migrator was not built at '{path}'. Build the solution before running these tests.");
    }
}

internal sealed record MigratorRun(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration)
{
    public string Output => new StringBuilder(StandardOutput).Append(StandardError).ToString();
}

internal static class RepositoryLayout
{
    public const string TargetFramework = "net10.0";

    public static DirectoryInfo RepositoryRoot { get; } = FindRepositoryRoot();

    public static string BuildConfiguration { get; } = FindBuildConfiguration();

    private static DirectoryInfo FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "MinimalBankSystem.slnx")))
        {
            directory = directory.Parent;
        }

        return directory
            ?? throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static string FindBuildConfiguration()
    {
        DirectoryInfo testOutput = new(AppContext.BaseDirectory);
        DirectoryInfo? configuration = testOutput.Name == TargetFramework
            ? testOutput.Parent
            : testOutput;

        return configuration?.Name
            ?? throw new InvalidOperationException(
                $"Could not determine the build configuration from '{AppContext.BaseDirectory}'.");
    }
}
