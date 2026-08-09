using System.Diagnostics;

namespace MinimalBankSystem.IntegrationTests.Migrations;

internal static class MigrationProcessHarness
{
    public static async Task<MigrationProcessResult> RunMigratorAsync(
        string? connectionString,
        TimeSpan? timeout = null)
    {
        string migratorDll = RepositoryLayout.ResolveProjectBinary("MinimalBankSystem.Migrator");
        string migratorDirectory = Path.GetDirectoryName(migratorDll)!;

        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            ArgumentList = { migratorDll },
            WorkingDirectory = migratorDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // A parent test process must never leak a connection string into the migrator.
        startInfo.Environment.Remove("ConnectionStrings__Database");

        if (connectionString is not null)
        {
            startInfo.Environment["ConnectionStrings__Database"] = connectionString;
        }

        using CancellationTokenSource executionTimeout = new(timeout ?? TimeSpan.FromSeconds(60));

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the migrator process.");

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(executionTimeout.Token);
        Task<string> standardError = process.StandardError.ReadToEndAsync(executionTimeout.Token);

        try
        {
            await process.WaitForExitAsync(executionTimeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException("The migrator process did not exit within the expected budget.");
        }

        return new MigrationProcessResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }
}

internal sealed record MigrationProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
