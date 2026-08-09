using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Real PostgreSQL evidence for the FND-04 migration machinery, reusing the FND-03 digest-pinned
/// container fixture. Nothing here falls back to SQLite or InMemory.
/// </summary>
[Trait("Category", "PostgreSqlIntegration")]
public sealed class MigrationBaselineTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly TimeSpan NormalRunBudget = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan BlockedRunBudget = TimeSpan.FromSeconds(180);

    [Fact]
    public async Task ExplicitMigratorAppliesTheBaselineToACleanDatabase()
    {
        Assert.Empty(await ReadPublicTablesAsync());
        Assert.False(await MigrationHistoryExistsAsync());

        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, NormalRunBudget);

        Assert.True(
            run.ExitCode == MigratorExitCode.Success,
            $"Expected a successful migration, got exit code {run.ExitCode}. Output:\n{run.Output}");

        string appliedMigration = Assert.Single(await ReadMigrationHistoryAsync());
        Assert.EndsWith("_InitialFoundation", appliedMigration, StringComparison.Ordinal);

        // The baseline establishes machinery only: the migration history table is the sole table.
        Assert.Equal([BankPersistence.MigrationsHistoryTableName], await ReadPublicTablesAsync());
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
            $"A no-op migration must still succeed, got exit code {second.ExitCode}. Output:\n{second.Output}");
        Assert.Equal(afterFirst, await ReadMigrationHistoryAsync());
        Assert.Equal([BankPersistence.MigrationsHistoryTableName], await ReadPublicTablesAsync());
    }

    [Fact]
    public async Task MigratorExitsNonZeroWhenNoConnectionStringIsConfigured()
    {
        MigratorRun run = await MigratorProcess.RunAsync(connectionString: null, NormalRunBudget);

        Assert.True(
            run.ExitCode != MigratorExitCode.Success,
            $"Missing configuration must not be reported as success. Output:\n{run.Output}");
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

        Assert.True(
            run.ExitCode != MigratorExitCode.Success,
            $"A connection failure must not be reported as success. Output:\n{run.Output}");
        Assert.Empty(await ReadPublicTablesAsync());
    }

    [Fact]
    public async Task MigratorExitsNonZeroWhenCredentialsAreRejected()
    {
        NpgsqlConnectionStringBuilder rejected = new(Database.ConnectionString)
        {
            Password = "not-the-fixture-password",
        };

        MigratorRun run = await MigratorProcess.RunAsync(rejected.ConnectionString, NormalRunBudget);

        Assert.True(
            run.ExitCode != MigratorExitCode.Success,
            $"An authentication failure must not be reported as success. Output:\n{run.Output}");
        Assert.Empty(await ReadPublicTablesAsync());
    }

    [Fact]
    public async Task MigrationExecutionStopsAtTheFixedBudgetInsteadOfHanging()
    {
        // Hold an uncommitted CREATE TABLE on the migration history relation. The migrator's own
        // CREATE TABLE then blocks on that lock, which is what an unbounded migration would do
        // forever during a deployment.
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

        Assert.True(
            run.ExitCode != MigratorExitCode.Success,
            $"A migration that never completed must not be reported as success. Output:\n{run.Output}");
        Assert.Equal(MigratorExitCode.Timeout, run.ExitCode);
        Assert.InRange(
            run.Duration,
            BankPersistence.MigrationTimeout - TimeSpan.FromSeconds(10),
            BankPersistence.MigrationTimeout + TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task NormalApiStartupNeverCreatesOrChangesSchema()
    {
        // Before: an untouched database.
        Assert.Empty(await ReadPublicTablesAsync());

        await StartApiAndIssueRequestAsync();

        // The API started, served traffic and resolved a real DbContext, yet created nothing.
        Assert.Empty(await ReadPublicTablesAsync());
        Assert.False(await MigrationHistoryExistsAsync());

        MigratorRun migration = await MigratorProcess.RunAsync(Database.ConnectionString, NormalRunBudget);
        Assert.Equal(MigratorExitCode.Success, migration.ExitCode);

        string[] historyAfterMigration = await ReadMigrationHistoryAsync();
        string[] tablesAfterMigration = await ReadPublicTablesAsync();
        Assert.Single(historyAfterMigration);

        await StartApiAndIssueRequestAsync();

        // After: restarting the API against an already-migrated database changes nothing either.
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
            Assert.Equal(
                typeof(BankDbContext).Assembly,
                context.GetService<IMigrationsAssembly>().Assembly);
            Assert.Equal(
                Database.ConnectionString,
                context.Database.GetConnectionString());
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

    private async Task<bool> MigrationHistoryExistsAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"SELECT to_regclass('{BankPersistence.MigrationsHistorySchema}.\"{BankPersistence.MigrationsHistoryTableName}\"') IS NOT NULL;",
            connection);

        return Assert.IsType<bool>(await command.ExecuteScalarAsync());
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
