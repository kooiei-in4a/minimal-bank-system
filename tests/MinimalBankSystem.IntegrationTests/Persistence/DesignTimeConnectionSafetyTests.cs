using System.Diagnostics;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.IntegrationTests.Persistence;

/// <summary>C8-M01 regression: a missing design-time connection must fail closed.</summary>
public sealed class DesignTimeConnectionSafetyTests
{
    private static readonly TimeSpan ProcessBudget = TimeSpan.FromSeconds(120);

    [Fact]
    public async Task ConnectionRequiredEfCommandWithoutConfigurationFailsWithoutAFabricatedDestination()
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = MigratorProcess.ResolveDotnetHost(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = RepositoryLayout.RepositoryRoot.FullName,
        };

        foreach (string argument in new[]
        {
            "tool", "run", "dotnet-ef", "database", "update", "--no-build",
            "--project", "src/MinimalBankSystem.Infrastructure",
            "--startup-project", "src/MinimalBankSystem.Migrator",
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Remove only from the child. The test process environment remains untouched and parallel-safe.
        startInfo.Environment.Remove(BankPersistence.ConnectionStringEnvironmentVariable);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the design-time EF command.");

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource wait = new(ProcessBudget);

        try
        {
            await process.WaitForExitAsync(wait.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException(
                $"The connection-required EF command did not fail within {ProcessBudget}.");
        }

        string output = $"{await standardOutput}{Environment.NewLine}{await standardError}";

        Assert.True(
            process.ExitCode != 0,
            $"A connection-required design-time operation without configuration must fail closed. Output:\n{output}");

        string[] forbiddenDestinations =
        [
            "127.0.0.1",
            "localhost",
            "design_time",
            "Data Source=",
            "Sqlite",
            "InMemory",
        ];

        Assert.All(
            forbiddenDestinations,
            destination => Assert.DoesNotContain(destination, output, StringComparison.OrdinalIgnoreCase));
    }
}
