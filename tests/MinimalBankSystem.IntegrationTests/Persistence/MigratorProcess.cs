using System.Diagnostics;
using System.Text;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.IntegrationTests.Persistence;

/// <summary>
/// Runs the real <c>MinimalBankSystem.Migrator</c> process so exit codes are observed the way a
/// deployment step observes them, instead of being simulated in-process.
/// </summary>
internal static class MigratorProcess
{
    private const string MigratorAssemblyName = "MinimalBankSystem.Migrator";

    /// <summary>
    /// Runs the migrator once and waits for it to exit.
    /// </summary>
    /// <param name="connectionString">
    /// Value for <see cref="BankPersistence.ConnectionStringEnvironmentVariable"/>. When
    /// <see langword="null"/> the variable is removed, so the migrator sees no configuration at all.
    /// </param>
    /// <param name="waitBudget">Upper bound the test is willing to wait for the process.</param>
    public static async Task<MigratorRun> RunAsync(
        string? connectionString,
        TimeSpan waitBudget)
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

        // The ambient environment of the test runner must not leak into the migrator, otherwise a
        // "no connection string" case could silently pick one up.
        if (connectionString is null)
        {
            startInfo.Environment.Remove(BankPersistence.ConnectionStringEnvironmentVariable);
        }
        else
        {
            startInfo.Environment[BankPersistence.ConnectionStringEnvironmentVariable] = connectionString;
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
                $"The migrator did not exit within {waitBudget}. A bounded migration must always " +
                "terminate on its own.");
        }

        TimeSpan duration = Stopwatch.GetElapsedTime(startedAt);

        return new MigratorRun(
            process.ExitCode,
            await standardOutput,
            await standardError,
            duration);
    }

    private static string ResolveDotnetHost() =>
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

/// <summary>Observed result of one migrator process run.</summary>
/// <param name="ExitCode">Process exit code; only zero means success.</param>
/// <param name="StandardOutput">Captured standard output.</param>
/// <param name="StandardError">Captured standard error.</param>
/// <param name="Duration">Wall-clock duration of the process.</param>
internal sealed record MigratorRun(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration)
{
    /// <summary>Combined output, used in assertion messages.</summary>
    public string Output => new StringBuilder(StandardOutput).Append(StandardError).ToString();
}

/// <summary>Locates repository-relative build output from the running test assembly.</summary>
internal static class RepositoryLayout
{
    /// <summary>Target framework folder shared by every project.</summary>
    public const string TargetFramework = "net10.0";

    /// <summary>Repository root, found by walking up to the solution file.</summary>
    public static DirectoryInfo RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>
    /// Build configuration of the currently running tests, taken from the test output path so
    /// Debug and Release runs both find the matching migrator build.
    /// </summary>
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
        // .../tests/MinimalBankSystem.IntegrationTests/bin/<Configuration>/net10.0/
        DirectoryInfo testOutput = new(AppContext.BaseDirectory);
        DirectoryInfo? configuration = testOutput.Name == TargetFramework
            ? testOutput.Parent
            : testOutput;

        return configuration?.Name
            ?? throw new InvalidOperationException(
                $"Could not determine the build configuration from '{AppContext.BaseDirectory}'.");
    }
}
