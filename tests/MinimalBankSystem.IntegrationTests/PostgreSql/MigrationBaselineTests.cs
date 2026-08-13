using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Infrastructure.Identity;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>Real PostgreSQL evidence for the FND-04 migration machinery.</summary>
[Trait("Category", "PostgreSqlIntegration")]
public sealed class MigrationBaselineTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string SecretSentinel = "FND04_REJECTED_PASSWORD_SENTINEL_8C9314F2";

    private static readonly TimeSpan NormalRunBudget = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan BlockedRunBudget = TimeSpan.FromSeconds(180);

    private static readonly string[] ExpectedTablesAtLatest =
        [BankPersistence.MigrationsHistoryTableName, OperatorConfiguration.TableName];

    [Fact]
    public async Task ExplicitMigratorAppliesTheBaselineToACleanDatabase()
    {
        Assert.Empty(await ReadPublicTablesAsync());
        Assert.False(await MigrationHistoryExistsAsync());

        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, NormalRunBudget);

        Assert.True(
            run.ExitCode == MigratorExitCode.Success,
            $"Expected success, got exit code {run.ExitCode}. Output:\n{run.Output}");

        string[] appliedMigrations = await ReadMigrationHistoryAsync();
        Assert.Equal(2, appliedMigrations.Length);
        Assert.Contains(appliedMigrations, id => id.EndsWith("_InitialFoundation", StringComparison.Ordinal));
        Assert.Contains(appliedMigrations, id => id.EndsWith("_AddOperatorIdentity", StringComparison.Ordinal));
        await AssertPublicTablesAsync(ExpectedTablesAtLatest);
    }

    [Fact]
    public async Task RunningTheMigratorTwiceLeavesTheHistoryUnchanged()
    {
        MigratorRun first = await MigratorProcess.RunAsync(Database.ConnectionString, NormalRunBudget);
        Assert.Equal(MigratorExitCode.Success, first.ExitCode);
        string[] afterFirst = await ReadMigrationHistoryAsync();

        MigratorRun second = await MigratorProcess.RunAsync(Database.ConnectionString, NormalRunBudget);

        Assert.True(
            second.ExitCode == MigratorExitCode.Success,
            $"A no-op migration must succeed. Output:\n{second.Output}");
        Assert.Equal(afterFirst, await ReadMigrationHistoryAsync());
        await AssertPublicTablesAsync(ExpectedTablesAtLatest);
    }

    /// <summary>
    /// ADR-0009's "immediately previous migration to latest, with representative existing rows"
    /// forward-validation requirement. The previous state (<c>InitialFoundation</c> only) is
    /// staged directly through <see cref="IMigrator"/> rather than the compiled Migrator, because
    /// the Migrator always targets latest; the upgrade step itself still goes through the real
    /// Migrator process.
    /// </summary>
    [Fact]
    public async Task UpgradingFromInitialFoundationAppliesOnlyTheOperatorIdentityMigration()
    {
        string initialFoundationMigrationId = await MigrateToInitialFoundationOnlyAsync();

        Assert.Equal([initialFoundationMigrationId], await ReadMigrationHistoryAsync());
        Assert.Equal([BankPersistence.MigrationsHistoryTableName], await ReadPublicTablesAsync());

        MigratorRun upgrade = await MigratorProcess.RunAsync(Database.ConnectionString, NormalRunBudget);

        Assert.True(
            upgrade.ExitCode == MigratorExitCode.Success,
            $"Expected the upgrade to latest to succeed. Output:\n{upgrade.Output}");

        string[] historyAfterUpgrade = await ReadMigrationHistoryAsync();
        Assert.Equal(2, historyAfterUpgrade.Length);
        Assert.Contains(initialFoundationMigrationId, historyAfterUpgrade);
        Assert.Contains(historyAfterUpgrade, id => id.EndsWith("_AddOperatorIdentity", StringComparison.Ordinal));
        await AssertPublicTablesAsync(ExpectedTablesAtLatest);
    }

    private async Task<string> MigrateToInitialFoundationOnlyAsync()
    {
        DbContextOptionsBuilder<BankDbContext> builder = new();
        builder.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        await using BankDbContext context = new(builder.Options);
        IMigrationsAssembly migrationsAssembly = context.GetService<IMigrationsAssembly>();
        string initialFoundationMigrationId = migrationsAssembly.Migrations.Keys
            .Single(id => id.EndsWith("_InitialFoundation", StringComparison.Ordinal));

        await context.GetService<IMigrator>().MigrateAsync(initialFoundationMigrationId);

        return initialFoundationMigrationId;
    }

    private async Task AssertPublicTablesAsync(string[] expectedTables)
    {
        string[] actualTables = await ReadPublicTablesAsync();

        Assert.Equal(
            expectedTables.OrderBy(name => name, StringComparer.Ordinal),
            actualTables.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task MigratorExitsNonZeroWhenNoConnectionStringIsConfigured()
    {
        MigratorRun run = await MigratorProcess.RunAsync(connectionString: null, NormalRunBudget);

        Assert.NotEqual(MigratorExitCode.Success, run.ExitCode);
        Assert.Empty(await ReadPublicTablesAsync());
    }

    [Fact]
    public async Task MigratorExitsNonZeroWhenTheServerIsUnreachable()
    {
        NpgsqlConnectionStringBuilder unreachable = new(Database.ConnectionString)
        {
            Port = 1,
            Timeout = 5,
        };

        MigratorRun run = await MigratorProcess.RunAsync(unreachable.ConnectionString, NormalRunBudget);

        Assert.NotEqual(MigratorExitCode.Success, run.ExitCode);
        Assert.Empty(await ReadPublicTablesAsync());
    }

    [Fact]
    public async Task MigratorExitsNonZeroWhenCredentialsAreRejectedWithoutDisclosingThePassword()
    {
        NpgsqlConnectionStringBuilder rejected = new(Database.ConnectionString)
        {
            Password = SecretSentinel,
        };

        MigratorRun run = await MigratorProcess.RunAsync(rejected.ConnectionString, NormalRunBudget);

        Assert.NotEqual(MigratorExitCode.Success, run.ExitCode);
        Assert.DoesNotContain(SecretSentinel, run.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretSentinel, run.StandardError, StringComparison.Ordinal);
        Assert.Empty(await ReadPublicTablesAsync());
    }

    [Fact]
    public async Task MigratorExitsNonZeroWhenMigrationHistoryIsMalformed()
    {
        await ExecuteNonQueryAsync(
            $"""
             CREATE TABLE {BankPersistence.MigrationsHistorySchema}."{BankPersistence.MigrationsHistoryTableName}" (
                 "MigrationId" character varying(150) NOT NULL,
                 CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
             );
             """);

        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, NormalRunBudget);

        Assert.NotEqual(MigratorExitCode.Success, run.ExitCode);
        Assert.Empty(await ReadMigrationHistoryIdsFromMalformedTableAsync());
    }

    [Fact]
    public async Task MigrationExecutionStopsAtTheFixedBudgetInsteadOfHanging()
    {
        await using NpgsqlConnection blocker = await PostgreSqlContainerFixture.OpenConnectionAsync(
            Database.ConnectionString,
            "holding the migration history lock");
        await using NpgsqlTransaction blocking = await blocker.BeginTransactionAsync();
        await using (NpgsqlCommand create = new(
            $"""
             CREATE TABLE {BankPersistence.MigrationsHistorySchema}."{BankPersistence.MigrationsHistoryTableName}" (
                 "MigrationId" character varying(150) NOT NULL,
                 "ProductVersion" character varying(32) NOT NULL,
                 CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
             );
             """,
            blocker,
            blocking))
        {
            await create.ExecuteNonQueryAsync();
        }

        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, BlockedRunBudget);

        await blocking.RollbackAsync();

        Assert.Equal(MigratorExitCode.Timeout, run.ExitCode);
        Assert.InRange(
            run.Duration,
            BankPersistence.MigrationTimeout - TimeSpan.FromSeconds(10),
            BankPersistence.MigrationTimeout + TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task NormalApiStartupNeverCreatesOrChangesSchema()
    {
        Assert.Empty(await ReadPublicTablesAsync());

        await StartApiAndIssueRequestAsync();

        Assert.Empty(await ReadPublicTablesAsync());
        Assert.False(await MigrationHistoryExistsAsync());

        MigratorRun migration = await MigratorProcess.RunAsync(Database.ConnectionString, NormalRunBudget);
        Assert.Equal(MigratorExitCode.Success, migration.ExitCode);

        string[] historyAfterMigration = await ReadMigrationHistoryAsync();
        string[] tablesAfterMigration = await ReadPublicTablesAsync();
        Assert.Equal(2, historyAfterMigration.Length);

        await StartApiAndIssueRequestAsync();

        Assert.Equal(historyAfterMigration, await ReadMigrationHistoryAsync());
        Assert.Equal(tablesAfterMigration, await ReadPublicTablesAsync());
    }

    [Fact]
    public async Task ApiResolvesTheSamePostgreSqlContextAsTheMigratorWithoutTouchingTheSchema()
    {
        await using MigrationApiFactory factory = new(Database.ConnectionString);
        using (HttpClient client = factory.CreateClient())
        {
            using HttpResponseMessage response = await client.GetAsync(new Uri("/", UriKind.Relative));
            Assert.NotNull(response);
        }

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            BankDbContext context = scope.ServiceProvider.GetRequiredService<BankDbContext>();

            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
            Assert.Equal(typeof(BankDbContext).Assembly, context.GetService<IMigrationsAssembly>().Assembly);
            Assert.Equal(Database.ConnectionString, context.Database.GetConnectionString());
        }

        Assert.Empty(await ReadPublicTablesAsync());
    }

    private async Task StartApiAndIssueRequestAsync()
    {
        await using MigrationApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/", UriKind.Relative));
        Assert.NotNull(response);
    }

    private Task<string[]> ReadPublicTablesAsync() =>
        ReadStringsAsync(
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
            ORDER BY table_name;
            """);

    private Task<string[]> ReadMigrationHistoryAsync() =>
        ReadStringsAsync(
            $"""
             SELECT "MigrationId"
             FROM {BankPersistence.MigrationsHistorySchema}."{BankPersistence.MigrationsHistoryTableName}"
             ORDER BY "MigrationId";
             """);

    private Task<string[]> ReadMigrationHistoryIdsFromMalformedTableAsync() =>
        ReadMigrationHistoryAsync();

    private async Task<bool> MigrationHistoryExistsAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"SELECT to_regclass('{BankPersistence.MigrationsHistorySchema}.\"{BankPersistence.MigrationsHistoryTableName}\"') IS NOT NULL;",
            connection);

        return Assert.IsType<bool>(await command.ExecuteScalarAsync());
    }

    private async Task ExecuteNonQueryAsync(string commandText)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<string[]> ReadStringsAsync(string commandText)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        List<string> values = [];

        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return [.. values];
    }

    private sealed class MigrationApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting(
                $"ConnectionStrings:{BankPersistence.ConnectionStringName}",
                connectionString);
        }
    }
}
