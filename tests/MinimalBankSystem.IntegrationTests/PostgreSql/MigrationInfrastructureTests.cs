using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MinimalBankSystem.Infrastructure.Persistence;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class MigrationInfrastructureTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task DedicatedMigratorAppliesOnlyTheEmptyFoundationBaselineToACleanDatabase()
    {
        MigratorProcessResult result = await RunMigratorAsync(Database.ConnectionString);

        Assert.Equal(0, result.ExitCode);

        string[] migrationIds = await ReadMigrationIdsAsync(Database.ConnectionString);
        Assert.Single(migrationIds);
        Assert.EndsWith("_InitialFoundation", migrationIds[0], StringComparison.Ordinal);

        string[] publicTables = await ReadPublicTablesAsync(Database.ConnectionString);
        Assert.Equal(["__EFMigrationsHistory"], publicTables);
    }

    [Fact]
    public async Task MigratorConnectionFailureReturnsANonZeroExitCode()
    {
        NpgsqlConnectionStringBuilder unreachable = new()
        {
            Host = "127.0.0.1",
            Port = 1,
            Database = "migration_failure_probe",
            Username = "postgres",
            Password = "test-only-unused",
            Pooling = false,
            Timeout = 2,
        };

        MigratorProcessResult result = await RunMigratorAsync(unreachable.ConnectionString);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Database migration failed", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MigrationExecutionHonorsCancellationWhilePostgreSqlMigrationHistoryIsLocked()
    {
        await using BankDbContext initialContext = BankDbContextFactory.Create(Database.ConnectionString);
        await MigrationExecution.MigrateAsync(initialContext, CancellationToken.None);

        await using NpgsqlConnection lockConnection = new(Database.ConnectionString);
        await lockConnection.OpenAsync();
        await using NpgsqlTransaction transaction = await lockConnection.BeginTransactionAsync();
        await using NpgsqlCommand lockCommand = new(
            "LOCK TABLE \"__EFMigrationsHistory\" IN ACCESS EXCLUSIVE MODE;",
            lockConnection,
            transaction);
        await lockCommand.ExecuteNonQueryAsync();

        await using BankDbContext blockedContext = BankDbContextFactory.Create(Database.ConnectionString);
        using CancellationTokenSource cancellationSource = new(TimeSpan.FromMilliseconds(250));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => MigrationExecution.MigrateAsync(blockedContext, cancellationSource.Token));
        Assert.Equal(60, MigrationExecution.CommandTimeoutSeconds);
        Assert.Equal(TimeSpan.FromSeconds(60), MigrationExecution.CancellationBudget);
        Assert.Equal(60, blockedContext.Database.GetCommandTimeout());
    }

    [Fact]
    public void BaselineSnapshotHasNoPendingEfModelChanges()
    {
        using BankDbContext context = BankDbContextFactory.Create(Database.ConnectionString);

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task NormalApiStartupDoesNotApplyMigrations()
    {
        Assert.Empty(await ReadMigrationIdsAsync(Database.ConnectionString));

        using NoAutoMigrationWebApplicationFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/not-found");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await ReadMigrationIdsAsync(Database.ConnectionString));
        Assert.Empty(await ReadPublicTablesAsync(Database.ConnectionString));
    }

    private static async Task<string[]> ReadMigrationIdsAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();

        await using NpgsqlCommand historyExistsCommand = new(
            "SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL;",
            connection);
        bool historyExists = (bool)(await historyExistsCommand.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("PostgreSQL did not report migration history state."));

        if (!historyExists)
        {
            return [];
        }

        await using NpgsqlCommand command = new(
            "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";",
            connection);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        List<string> migrationIds = [];

        while (await reader.ReadAsync())
        {
            migrationIds.Add(reader.GetString(0));
        }

        return migrationIds.ToArray();
    }

    private static async Task<string[]> ReadPublicTablesAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT table_name FROM information_schema.tables " +
            "WHERE table_schema = 'public' ORDER BY table_name;",
            connection);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        List<string> tables = [];

        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables.ToArray();
    }

    private static async Task<MigratorProcessResult> RunMigratorAsync(string connectionString)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            Arguments = "run --no-build --project src/MinimalBankSystem.Migrator/MinimalBankSystem.Migrator.csproj",
            WorkingDirectory = FindRepositoryRoot().FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment["ConnectionStrings__Database"] = connectionString;

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the dedicated migration process.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));

        await process.WaitForExitAsync(timeout.Token);

        return new MigratorProcessResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MinimalBankSystem.slnx")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new InvalidOperationException("Could not locate the repository root.");
    }

    private sealed class NoAutoMigrationWebApplicationFactory(string connectionString)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration(
                (_, configurationBuilder) => configurationBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Database"] = connectionString,
                    }));
        }
    }

    private sealed record MigratorProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
