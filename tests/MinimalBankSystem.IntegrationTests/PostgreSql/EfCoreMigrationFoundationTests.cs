using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection(TestExecutionCollections.ConsoleSensitive)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class EfCoreMigrationFoundationTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string MigrationHistoryTable = "public.\"__EFMigrationsHistory\"";

    [Fact]
    public async Task ExplicitMigratorAppliesInitialFoundationToCleanPostgreSql()
    {
        SchemaSnapshot before = await CaptureSchemaSnapshotAsync(Database.ConnectionString);
        Assert.False(before.MigrationHistoryExists);
        Assert.Empty(before.MigrationIds);
        Assert.Empty(before.UserRelations);

        int exitCode = await RunMigratorAsync(Database.ConnectionString);

        Assert.Equal(0, exitCode);

        SchemaSnapshot after = await CaptureSchemaSnapshotAsync(Database.ConnectionString);
        Assert.True(after.MigrationHistoryExists);
        Assert.Single(after.MigrationIds);
        Assert.EndsWith("_InitialFoundation", after.MigrationIds[0], StringComparison.Ordinal);
        Assert.Empty(after.UserRelations);
        AssertNoBusinessArtifacts(after);
    }

    [Fact]
    public async Task ExplicitMigratorRerunOnAppliedDatabaseRemainsSuccessfulAndStable()
    {
        Assert.Equal(0, await RunMigratorAsync(Database.ConnectionString));

        SchemaSnapshot afterFirst = await CaptureSchemaSnapshotAsync(Database.ConnectionString);

        Assert.Equal(0, await RunMigratorAsync(Database.ConnectionString));

        SchemaSnapshot afterSecond = await CaptureSchemaSnapshotAsync(Database.ConnectionString);
        AssertSnapshotsEqual(afterFirst, afterSecond);
        Assert.Single(afterSecond.MigrationIds);
        Assert.EndsWith("_InitialFoundation", afterSecond.MigrationIds[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplicitMigratorFailsForUnreachablePostgreSql()
    {
        const string unreachable =
            "Host=127.0.0.1;Port=1;Database=mbs_unreachable;Username=postgres;Password=invalid;Timeout=1;Command Timeout=1";

        int exitCode = await RunMigratorAsync(unreachable);

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task ExplicitMigratorPropagatesCancellationAsFailure()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        int exitCode = await RunMigratorAsync(Database.ConnectionString, cancellation.Token);

        Assert.NotEqual(0, exitCode);

        SchemaSnapshot snapshot = await CaptureSchemaSnapshotAsync(Database.ConnectionString);
        Assert.False(snapshot.MigrationHistoryExists);
    }

    [Fact]
    public async Task NormalApiStartupDoesNotApplyMigrationsOrMutateSchema()
    {
        SchemaSnapshot before = await CaptureSchemaSnapshotAsync(Database.ConnectionString);
        Assert.False(before.MigrationHistoryExists);

        await using PersistenceApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/__contract/does-not-exist");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);

        SchemaSnapshot after = await CaptureSchemaSnapshotAsync(Database.ConnectionString);
        AssertSnapshotsEqual(before, after);
        Assert.False(after.MigrationHistoryExists);
        Assert.Empty(after.UserRelations);
    }

    [Fact]
    public async Task BankDbContextReportsNoPendingModelChangesAgainstBaseline()
    {
        Assert.Equal(0, await RunMigratorAsync(Database.ConnectionString));

        await using BankDbContext dbContext = CreateBankDbContext(Database.ConnectionString);
        Assert.False(dbContext.Database.HasPendingModelChanges());
    }

    [Fact]
    public void TemporaryModelOnlyDriftIsDetectedByHasPendingModelChanges()
    {
        DbContextOptionsBuilder<BankDbContext> optionsBuilder = new();
        BankPersistence.ConfigureNpgsql(optionsBuilder, Database.ConnectionString);

        using PendingModelDriftProbeContext probe = new(optionsBuilder.Options);
        Assert.True(probe.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task DotnetMigratorExecutableAppliesBaseline()
    {
        await using PostgreSqlTestDatabase database = await Fixture.CreateDatabaseAsync();
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string migratorDll = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "MinimalBankSystem.Migrator",
            "bin",
            "Debug",
            "net10.0",
            "MinimalBankSystem.Migrator.dll");

        Assert.True(File.Exists(migratorDll), $"Migrator assembly was not built at '{migratorDll}'.");

        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            Arguments = $"\"{migratorDll}\"",
            WorkingDirectory = repositoryRoot.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment[BankPersistence.ConnectionStringEnvironmentVariable] =
            database.ConnectionString;

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the migrator process.");
        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, $"stdout:\n{stdout}\nstderr:\n{stderr}");

        SchemaSnapshot snapshot = await CaptureSchemaSnapshotAsync(database.ConnectionString);
        Assert.Single(snapshot.MigrationIds);
        Assert.EndsWith("_InitialFoundation", snapshot.MigrationIds[0], StringComparison.Ordinal);
        Assert.Empty(snapshot.UserRelations);
    }

    private static async Task<int> RunMigratorAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        // Prefer in-memory configuration in-process so connection strings that contain ';'
        // are never parsed as command-line fragments, and process-wide env mutation is avoided.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"ConnectionStrings:{BankPersistence.ConnectionStringName}"] = connectionString,
                })
            .Build();

        return await MigrationEntryPoint.RunAsync(configuration, cancellationToken);
    }

    private static void AssertSnapshotsEqual(SchemaSnapshot left, SchemaSnapshot right)
    {
        Assert.Equal(left.MigrationHistoryExists, right.MigrationHistoryExists);
        Assert.Equal(left.MigrationIds, right.MigrationIds);
        Assert.Equal(left.UserRelations, right.UserRelations);
    }

    private static BankDbContext CreateBankDbContext(string connectionString)
    {
        DbContextOptionsBuilder<BankDbContext> optionsBuilder = new();
        BankPersistence.ConfigureNpgsql(optionsBuilder, connectionString);
        return new BankDbContext(optionsBuilder.Options);
    }

    private static async Task<SchemaSnapshot> CaptureSchemaSnapshotAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();

        bool historyExists = await ExecuteScalarAsync<bool>(
            connection,
            """
            SELECT EXISTS (
                SELECT 1
                FROM pg_catalog.pg_class AS class
                INNER JOIN pg_catalog.pg_namespace AS namespace
                    ON namespace.oid = class.relnamespace
                WHERE namespace.nspname = 'public'
                  AND class.relname = '__EFMigrationsHistory'
                  AND class.relkind IN ('r', 'p'));
            """);

        List<string> migrationIds = [];
        if (historyExists)
        {
            await using NpgsqlCommand historyCommand = new(
                $"SELECT \"MigrationId\" FROM {MigrationHistoryTable} ORDER BY \"MigrationId\";",
                connection);
            await using NpgsqlDataReader reader = await historyCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                migrationIds.Add(reader.GetString(0));
            }
        }

        List<string> userRelations = [];
        await using (NpgsqlCommand relationsCommand = new(
            """
            SELECT n.nspname || '.' || c.relname
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public'
              AND c.relkind IN ('r', 'p', 'v', 'm', 'S')
              AND c.relname <> '__EFMigrationsHistory'
            ORDER BY 1;
            """,
            connection))
        await using (NpgsqlDataReader reader = await relationsCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                userRelations.Add(reader.GetString(0));
            }
        }

        return new SchemaSnapshot(historyExists, migrationIds, userRelations);
    }

    private static void AssertNoBusinessArtifacts(SchemaSnapshot snapshot)
    {
        string[] forbidden =
        [
            "customer",
            "account",
            "operator",
            "identity",
            "audit",
            "transaction",
            "idempotency",
        ];

        Assert.All(
            snapshot.UserRelations,
            relation =>
            {
                foreach (string token in forbidden)
                {
                    Assert.DoesNotContain(token, relation, StringComparison.OrdinalIgnoreCase);
                }
            });
    }

    private static async Task<T> ExecuteScalarAsync<T>(
        NpgsqlConnection connection,
        string commandText)
    {
        await using NpgsqlCommand command = new(commandText, connection);
        object? result = await command.ExecuteScalarAsync();
        return Assert.IsType<T>(result);
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

    private sealed record SchemaSnapshot(
        bool MigrationHistoryExists,
        IReadOnlyList<string> MigrationIds,
        IReadOnlyList<string> UserRelations);

    /// <summary>
    /// Test-only model drift probe. Not part of the product model and must not be committed
    /// as a production entity.
    /// </summary>
    private sealed class PendingModelDriftProbeContext : BankDbContext
    {
        public PendingModelDriftProbeContext(DbContextOptions<BankDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity(
                "_fnd04_pending_model_drift_probe",
                entity =>
                {
                    entity.Property<int>("Id");
                    entity.HasKey("Id");
                });
        }
    }
}

internal sealed class PersistenceApiFactory : WebApplicationFactory<Program>
{
    private readonly string connectionString;

    public PersistenceApiFactory(string connectionString)
    {
        this.connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"ConnectionStrings:{BankPersistence.ConnectionStringName}"] = connectionString,
                });
        });
    }
}
