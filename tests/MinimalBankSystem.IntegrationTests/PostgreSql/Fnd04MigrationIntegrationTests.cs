using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection(TestExecutionCollections.ConsoleSensitive)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class Fnd04MigrationIntegrationTests(
    PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task CleanDatabaseAppliesEmptyFoundationAndRecordsMigrationHistory()
    {
        await using BankDbContext before = CreateContext();

        Assert.Empty(await before.Database.GetAppliedMigrationsAsync());
        Assert.False(await TableExistsAsync("__EFMigrationsHistory"));

        await MigrationExecutor.ApplyAsync(Database.ConnectionString);

        await using BankDbContext after = CreateContext();
        string[] appliedMigrations = (await after.Database.GetAppliedMigrationsAsync()).ToArray();

        string appliedMigration = Assert.Single(appliedMigrations);
        Assert.EndsWith("_InitialFoundation", appliedMigration, StringComparison.Ordinal);
        Assert.False(after.Database.HasPendingModelChanges());
        Assert.True(await TableExistsAsync("__EFMigrationsHistory"));
        Assert.Equal(0, await CountNonHistoryTablesAsync());
    }

    [Fact]
    public async Task DedicatedMigratorAppliesCleanPostgreSqlDatabase()
    {
        ProcessResult result = await RunMigratorAsync(Database.ConnectionString);

        Assert.Equal(0, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError));

        await using BankDbContext context = CreateContext();
        Assert.Single(await context.Database.GetAppliedMigrationsAsync());
        Assert.True(await TableExistsAsync("__EFMigrationsHistory"));
        Assert.Equal(0, await CountNonHistoryTablesAsync());
    }

    [Fact]
    public async Task TemporaryModelOnlyDriftIsDetectedByEfPendingModelMechanism()
    {
        await MigrationExecutor.ApplyAsync(Database.ConnectionString);

        await using BankDbContext current = CreateContext();
        Assert.False(current.Database.HasPendingModelChanges());

        await using DriftProbeDbContext drift = new(CreateOptions());
        Assert.True(drift.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task NormalApiStartupDoesNotChangePostgreSqlSchemaOrMigrationHistory()
    {
        SchemaState before = await ReadSchemaStateAsync();

        using ApiWithoutMigrationFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);

        SchemaState after = await ReadSchemaStateAsync();
        Assert.Equal(before, after);
        Assert.False(await TableExistsAsync("__EFMigrationsHistory"));
    }

    [Fact]
    public async Task MigratorConnectionFailureReturnsNonZeroExitCode()
    {
        NpgsqlConnectionStringBuilder unreachable = new()
        {
            Host = "127.0.0.1",
            Port = 1,
            Database = "unreachable",
            Username = "test-only-unused",
            Password = "test-only-unused",
            Pooling = false,
            Timeout = 1,
            CommandTimeout = 1,
        };

        int exitCode = await MigrationCommand.RunAsync(
            unreachable.ConnectionString,
            TimeSpan.FromSeconds(2));

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task MigratorTimeoutReturnsNonZeroExitCode()
    {
        int exitCode = await MigrationCommand.RunAsync(
            "Host=not-used;Database=not-used;Username=not-used;Password=not-used",
            TimeSpan.FromMilliseconds(50),
            static (_, cancellationToken) =>
                Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));

        Assert.NotEqual(0, exitCode);
    }

    private BankDbContext CreateContext() => new(CreateOptions());

    private DbContextOptions<BankDbContext> CreateOptions()
    {
        DbContextOptionsBuilder<BankDbContext> options = new();
        BankDbContextOptions.Configure(
            options,
            Database.ConnectionString,
            useMigrationTimeout: true);
        return options.Options;
    }

    private async Task<bool> TableExistsAsync(string tableName)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_name = $1);",
            connection);
        command.Parameters.AddWithValue(tableName);

        return Assert.IsType<bool>(await command.ExecuteScalarAsync());
    }

    private async Task<int> CountNonHistoryTablesAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT COUNT(*) FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_name <> '__EFMigrationsHistory';",
            connection);

        long count = Assert.IsType<long>(await command.ExecuteScalarAsync());
        return checked((int)count);
    }

    private async Task<SchemaState> ReadSchemaStateAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT " +
            "COALESCE((SELECT string_agg(table_name, ',' ORDER BY table_name) " +
            "FROM information_schema.tables WHERE table_schema = 'public'), '');",
            connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        string tables = reader.GetString(0);
        await reader.DisposeAsync();

        string migrationHistory = string.Empty;
        if (await TableExistsAsync("__EFMigrationsHistory"))
        {
            await using NpgsqlCommand historyCommand = new(
                "SELECT COALESCE(string_agg(migration_id, ',' ORDER BY migration_id), '') " +
                "FROM public.\"__EFMigrationsHistory\";",
                connection);
            migrationHistory = Assert.IsType<string>(await historyCommand.ExecuteScalarAsync());
        }

        return new SchemaState(tables, migrationHistory);
    }

    private static async Task<ProcessResult> RunMigratorAsync(string connectionString)
    {
        string repositoryRoot = FindRepositoryRoot();
        string migratorAssembly = Path.Combine(
            repositoryRoot,
            "src",
            "MinimalBankSystem.Migrator",
            "bin",
            "Debug",
            "net10.0",
            "MinimalBankSystem.Migrator.dll");

        Assert.True(File.Exists(migratorAssembly), $"Migrator assembly was not found: {migratorAssembly}");

        ProcessStartInfo startInfo = new("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(migratorAssembly);
        startInfo.Environment["ConnectionStrings__Database"] = connectionString;

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The dedicated migrator process could not be started.");
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(90));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            throw;
        }

        string standardOutput = await process.StandardOutput.ReadToEndAsync();
        string standardError = await process.StandardError.ReadToEndAsync();
        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "MinimalBankSystem.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("The repository root could not be found.");
    }

    private sealed class DriftProbeDbContext(DbContextOptions<BankDbContext> options)
        : BankDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TemporaryDriftProbe>(entity =>
            {
                entity.HasNoKey();
                entity.Property(probe => probe.Value);
            });
        }
    }

    private sealed class TemporaryDriftProbe
    {
        public int Value { get; set; }
    }

    private sealed record SchemaState(string Tables, string MigrationHistory);

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class ApiWithoutMigrationFactory(string connectionString)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Database", connectionString);
        }
    }
}
