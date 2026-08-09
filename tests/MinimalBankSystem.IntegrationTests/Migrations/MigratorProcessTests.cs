using System.Diagnostics;
using MinimalBankSystem.IntegrationTests.PostgreSql;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.Migrations;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class MigratorProcessTests(PostgreSqlContainerFixture fixture)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private readonly PostgreSqlContainerFixture fixture = fixture;

    [Fact]
    public async Task CleanApplyCreatesHistoryTableWithInitialFoundationAndNoBusinessSchema()
    {
        await using PostgreSqlTestDatabase database = await fixture.CreateDatabaseAsync();

        MigratorProcessResult result = await MigratorProcess.RunAsync(
            database.ConnectionString,
            TimeSpan.FromSeconds(90));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("completed successfully", result.StandardOutput, StringComparison.OrdinalIgnoreCase);

        string[] historyRows = await QueryMigrationHistoryAsync(database.ConnectionString);
        string migrationId = Assert.Single(historyRows);
        Assert.EndsWith("_InitialFoundation", migrationId, StringComparison.Ordinal);

        string[] userTables = await QueryUserTablesAsync(database.ConnectionString);
        Assert.Equal(["__EFMigrationsHistory"], userTables);
    }

    [Fact]
    public async Task RerunOnAnAlreadyMigratedDatabaseSucceedsWithoutDuplicatingHistory()
    {
        await using PostgreSqlTestDatabase database = await fixture.CreateDatabaseAsync();

        MigratorProcessResult first = await MigratorProcess.RunAsync(
            database.ConnectionString,
            TimeSpan.FromSeconds(90));
        Assert.Equal(0, first.ExitCode);

        MigratorProcessResult second = await MigratorProcess.RunAsync(
            database.ConnectionString,
            TimeSpan.FromSeconds(90));

        Assert.Equal(0, second.ExitCode);
        Assert.Contains("completed successfully", second.StandardOutput, StringComparison.OrdinalIgnoreCase);

        string[] historyRows = await QueryMigrationHistoryAsync(database.ConnectionString);
        string migrationId = Assert.Single(historyRows);
        Assert.EndsWith("_InitialFoundation", migrationId, StringComparison.Ordinal);

        string[] userTables = await QueryUserTablesAsync(database.ConnectionString);
        Assert.Equal(["__EFMigrationsHistory"], userTables);
    }

    [Fact]
    public async Task MissingConnectionStringFailsFastWithNonZeroExit()
    {
        MigratorProcessResult result = await MigratorProcess.RunAsync(
            connectionString: null,
            TimeSpan.FromSeconds(30));

        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain("completed successfully", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ConnectionStrings__Database", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnreachableConnectionFailsWithNonZeroExitAndDoesNotReportSuccess()
    {
        NpgsqlConnectionStringBuilder unreachable = new()
        {
            Host = "127.0.0.1",
            Port = 1,
            Database = "unreachable",
            Username = "postgres",
            Password = "test-only-unused",
            Timeout = 5,
        };

        MigratorProcessResult result = await MigratorProcess.RunAsync(
            unreachable.ConnectionString,
            TimeSpan.FromSeconds(30));

        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain("completed successfully", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Migration failed", result.StandardError, StringComparison.Ordinal);
    }

    private static async Task<string[]> QueryMigrationHistoryAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT \"MigrationId\" FROM public.\"__EFMigrationsHistory\" ORDER BY \"MigrationId\";",
            connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        List<string> migrationIds = [];

        while (await reader.ReadAsync())
        {
            migrationIds.Add(reader.GetString(0));
        }

        return [.. migrationIds];
    }

    private static async Task<string[]> QueryUserTablesAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name;",
            connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        List<string> tableNames = [];

        while (await reader.ReadAsync())
        {
            tableNames.Add(reader.GetString(0));
        }

        return [.. tableNames];
    }
}

internal readonly record struct MigratorProcessResult(int ExitCode, string StandardOutput, string StandardError);

internal static class MigratorProcess
{
    public static async Task<MigratorProcessResult> RunAsync(string? connectionString, TimeSpan timeout)
    {
        string dllPath = ResolveMigratorDllPath();

        ProcessStartInfo startInfo = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(dllPath);

        if (connectionString is null)
        {
            startInfo.Environment.Remove("ConnectionStrings__Database");
        }
        else
        {
            startInfo.Environment["ConnectionStrings__Database"] = connectionString;
        }

        using Process process = new() { StartInfo = startInfo };
        process.Start();

        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();

        using CancellationTokenSource timeoutCancellation = new(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"The Migrator process did not exit within the {timeout} test timeout.");
        }

        string standardOutput = await standardOutputTask;
        string standardError = await standardErrorTask;

        return new MigratorProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static string ResolveMigratorDllPath()
    {
        DirectoryInfo testOutputDirectory = new(AppContext.BaseDirectory);
        string targetFrameworkMoniker = testOutputDirectory.Name;
        string configuration = testOutputDirectory.Parent?.Name
            ?? throw new InvalidOperationException(
                "Could not determine the build configuration from the test output directory.");

        DirectoryInfo? repositoryRoot = testOutputDirectory;

        while (repositoryRoot is not null &&
            !File.Exists(Path.Combine(repositoryRoot.FullName, "MinimalBankSystem.slnx")))
        {
            repositoryRoot = repositoryRoot.Parent;
        }

        if (repositoryRoot is null)
        {
            throw new InvalidOperationException(
                "Could not locate the repository root from the test output directory.");
        }

        string dllPath = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "MinimalBankSystem.Migrator",
            "bin",
            configuration,
            targetFrameworkMoniker,
            "MinimalBankSystem.Migrator.dll");

        if (!File.Exists(dllPath))
        {
            throw new InvalidOperationException(
                $"The Migrator build output was not found at '{dllPath}'. Build the solution before " +
                "running this test.");
        }

        return dllPath;
    }
}
