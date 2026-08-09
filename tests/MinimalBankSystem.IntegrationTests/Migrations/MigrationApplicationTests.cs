extern alias Migrator;

using MinimalBankSystem.IntegrationTests.PostgreSql;
using MigrationExecutor = Migrator::MinimalBankSystem.Migrator.MigrationExecutor;

namespace MinimalBankSystem.IntegrationTests.Migrations;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class MigrationApplicationTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    internal const string InitialFoundationMigrationId = "20260809115813_InitialFoundation";

    [Fact]
    public async Task DedicatedMigratorAppliesBaselineToCleanDatabaseWithoutBusinessSchema()
    {
        Assert.Empty(await PostgreSqlProbe.GetAppliedMigrationIdsAsync(Database.ConnectionString));
        Assert.Empty(await PostgreSqlProbe.GetUserTablesAsync(Database.ConnectionString));

        MigrationProcessResult result = await MigrationProcessHarness.RunMigratorAsync(Database.ConnectionString);

        Assert.Equal(MigrationExecutor.ExitSuccess, result.ExitCode);
        Assert.Contains("migrated successfully", result.StandardOutput, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            [MigrationApplicationTests.InitialFoundationMigrationId],
            await PostgreSqlProbe.GetAppliedMigrationIdsAsync(Database.ConnectionString));
        Assert.Equal(
            ["__EFMigrationsHistory"],
            await PostgreSqlProbe.GetUserTablesAsync(Database.ConnectionString));
        Assert.Empty(await PostgreSqlProbe.GetUserSequencesAsync(Database.ConnectionString));
        Assert.Empty(await PostgreSqlProbe.GetUserTriggersAsync(Database.ConnectionString));
    }

    [Fact]
    public async Task MigratorRerunIsSafeAndDoesNotDuplicateHistory()
    {
        MigrationProcessResult first = await MigrationProcessHarness.RunMigratorAsync(Database.ConnectionString);
        Assert.Equal(MigrationExecutor.ExitSuccess, first.ExitCode);

        MigrationProcessResult rerun = await MigrationProcessHarness.RunMigratorAsync(Database.ConnectionString);

        Assert.Equal(MigrationExecutor.ExitSuccess, rerun.ExitCode);
        Assert.Equal(
            [MigrationApplicationTests.InitialFoundationMigrationId],
            await PostgreSqlProbe.GetAppliedMigrationIdsAsync(Database.ConnectionString));
        Assert.Equal(
            ["__EFMigrationsHistory"],
            await PostgreSqlProbe.GetUserTablesAsync(Database.ConnectionString));
    }
}
