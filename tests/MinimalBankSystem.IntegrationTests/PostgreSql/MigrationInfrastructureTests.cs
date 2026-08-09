using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Infrastructure.Persistence;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class MigrationInfrastructureTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string InitialFoundationMigration = "20260809120000_InitialFoundation";

    [Fact]
    public async Task ExplicitMigratorAppliesTheEmptyFoundationExactlyOnce()
    {
        MigratorResult firstRun = await RunMigratorAsync(Database.ConnectionString);

        Assert.True(firstRun.ExitCode == 0, firstRun.CombinedOutput);
        Assert.Equal(
            [InitialFoundationMigration],
            await ReadMigrationHistoryAsync(Database.ConnectionString));
        Assert.Equal(
            "10.0.10",
            await ExecuteScalarAsync<string>(
                Database.ConnectionString,
                "SELECT \"ProductVersion\" FROM public.\"__EFMigrationsHistory\";"));
        Assert.Equal(0, await CountBusinessTablesAsync(Database.ConnectionString));

        MigratorResult secondRun = await RunMigratorAsync(Database.ConnectionString);

        Assert.True(secondRun.ExitCode == 0, secondRun.CombinedOutput);
        Assert.Equal(
            [InitialFoundationMigration],
            await ReadMigrationHistoryAsync(Database.ConnectionString));
    }

    [Fact]
    public async Task ExplicitMigratorReturnsNonZeroWhenTheConnectionFails()
    {
        NpgsqlConnectionStringBuilder invalidConnection = new(Database.ConnectionString)
        {
            Host = "127.0.0.1",
            Port = 1,
            Timeout = 1,
            CommandTimeout = 1,
            Pooling = false,
        };

        MigratorResult result = await RunMigratorAsync(
            invalidConnection.ConnectionString,
            TimeSpan.FromSeconds(30));
        string password = invalidConnection.Password
            ?? throw new InvalidOperationException("The fixture connection string has no password.");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Database migration failed", result.CombinedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(password, result.CombinedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplicitMigratorReturnsNonZeroAtTheSixtySecondBudget()
    {
        MigratorResult initialRun = await RunMigratorAsync(Database.ConnectionString);
        Assert.True(initialRun.ExitCode == 0, initialRun.CombinedOutput);

        await using NpgsqlConnection lockConnection = new(Database.ConnectionString);
        await lockConnection.OpenAsync();
        await using NpgsqlTransaction lockTransaction = await lockConnection.BeginTransactionAsync();
        await using (NpgsqlCommand lockCommand = new(
            "LOCK TABLE public.\"__EFMigrationsHistory\" IN ACCESS EXCLUSIVE MODE;",
            lockConnection,
            lockTransaction))
        {
            await lockCommand.ExecuteNonQueryAsync();
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        MigratorResult blockedRun = await RunMigratorAsync(
            Database.ConnectionString,
            TimeSpan.FromSeconds(90));
        stopwatch.Stop();

        Assert.NotEqual(0, blockedRun.ExitCode);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(55), TimeSpan.FromSeconds(85));
        Assert.DoesNotContain(
            "Database migrations applied successfully",
            blockedRun.CombinedOutput,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalApiStartupDoesNotCreateMigrationHistoryOrSchema()
    {
        Assert.False(await MigrationHistoryExistsAsync(Database.ConnectionString));
        Assert.Equal(0, await CountBusinessTablesAsync(Database.ConnectionString));

        using NoMigrationApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/__migration-startup-probe");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);

        using IServiceScope scope = factory.Services.CreateScope();
        BankDbContext dbContext = scope.ServiceProvider.GetRequiredService<BankDbContext>();
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);

        Assert.False(await MigrationHistoryExistsAsync(Database.ConnectionString));
        Assert.Equal(0, await CountBusinessTablesAsync(Database.ConnectionString));
    }

    [Fact]
    public void ActualEfModelDriftMechanismReportsNoPendingChanges()
    {
        using BankDbContext dbContext = new(
            BankDbContextConfiguration.CreateOptions(Database.ConnectionString));

        Assert.False(dbContext.Database.HasPendingModelChanges());
    }

    private static async Task<MigratorResult> RunMigratorAsync(
        string connectionString,
        TimeSpan? processBudget = null)
    {
        string repositoryRoot = FindRepositoryRoot();
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not determine the test build configuration.");

        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add(configuration);
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add("src/MinimalBankSystem.Migrator");
        startInfo.Environment[BankDbContextConfiguration.EnvironmentVariable] = connectionString;
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the explicit migrator process.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(
            processBudget ?? TimeSpan.FromSeconds(30));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException exception) when (timeout.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync();
            throw new TimeoutException("The migrator process did not exit within the test budget.", exception);
        }

        return new MigratorResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MinimalBankSystem.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static async Task<bool> MigrationHistoryExistsAsync(string connectionString) =>
        await ExecuteScalarAsync<bool>(
            connectionString,
            "SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL;");

    private static async Task<long> CountBusinessTablesAsync(string connectionString) =>
        await ExecuteScalarAsync<long>(
            connectionString,
            """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name NOT LIKE '\_\_%' ESCAPE '\';
            """);

    private static async Task<string[]> ReadMigrationHistoryAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT \"MigrationId\" FROM public.\"__EFMigrationsHistory\" ORDER BY \"MigrationId\";",
            connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        List<string> migrations = [];

        while (await reader.ReadAsync())
        {
            migrations.Add(reader.GetString(0));
        }

        return [.. migrations];
    }

    private static async Task<T> ExecuteScalarAsync<T>(
        string connectionString,
        string commandText)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        object? result = await command.ExecuteScalarAsync();
        return Assert.IsType<T>(result);
    }

    private sealed record MigratorResult(
        int ExitCode,
        string StandardOutput,
        string StandardError)
    {
        public string CombinedOutput => $"{StandardOutput}{Environment.NewLine}{StandardError}";
    }

    private sealed class NoMigrationApiFactory(string connectionString)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting(BankDbContextConfiguration.ConfigurationKey, connectionString);
        }
    }
}
