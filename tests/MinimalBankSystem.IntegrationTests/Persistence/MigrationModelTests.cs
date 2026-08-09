using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.IntegrationTests.Persistence;

/// <summary>
/// Model and generated-SQL level checks. These do not touch a database, so they never stand in for
/// the real PostgreSQL evidence in <see cref="PostgreSql.MigrationBaselineTests"/>; they pin down
/// what the committed migration actually contains.
/// </summary>
public sealed class MigrationModelTests
{
    private const string ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";

    private static readonly string QualifiedHistoryTable =
        $"{BankPersistence.MigrationsHistorySchema}.\"{BankPersistence.MigrationsHistoryTableName}\"";

    private static BankDbContext CreateDesignTimeContext() =>
        new BankDbContextFactory().CreateDbContext([]);

    [Fact]
    public void DesignTimeContextUsesPostgreSqlAndTheInfrastructureMigrationsAssembly()
    {
        using BankDbContext context = CreateDesignTimeContext();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        Assert.Equal(
            typeof(BankDbContext).Assembly,
            context.GetService<IMigrationsAssembly>().Assembly);
    }

    [Fact]
    public void OnlyTheInitialFoundationMigrationIsCommitted()
    {
        using BankDbContext context = CreateDesignTimeContext();

        string migration = Assert.Single(context.GetService<IMigrationsAssembly>().Migrations.Keys);

        Assert.EndsWith("_InitialFoundation", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBaselineModelDeclaresNoEntityType()
    {
        using BankDbContext context = CreateDesignTimeContext();

        // FND-04 owns migration machinery only. A business entity appearing here would mean the
        // baseline migration is no longer empty.
        Assert.Empty(context.Model.GetEntityTypes());
    }

    [Fact]
    public void TheBaselineMigrationDeclaresNoSchemaOperationAtAll()
    {
        using BankDbContext context = CreateDesignTimeContext();
        IMigrationsAssembly migrations = context.GetService<IMigrationsAssembly>();

        Migration migration = migrations.CreateMigration(
            migrations.Migrations.Values.Single(),
            ActiveProvider);

        // Strongest available statement that the baseline holds no business DDL: EF built zero
        // migration operations in either direction, so there is no table, sequence, trigger or
        // constraint to inspect for business content.
        Assert.Empty(migration.UpOperations);
        Assert.Empty(migration.DownOperations);
    }

    [Fact]
    public void TheGeneratedBaselineSqlCreatesOnlyTheMigrationHistoryTable()
    {
        using BankDbContext context = CreateDesignTimeContext();

        string script = context.GetService<IMigrator>().GenerateScript();

        string[] createdTables =
        [
            .. Regex
                .Matches(
                    script,
                    @"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<table>[^\s(]+)",
                    RegexOptions.IgnoreCase)
                .Select(match => match.Groups["table"].Value),
        ];

        Assert.Equal([QualifiedHistoryTable], createdTables);

        Assert.DoesNotContain("CREATE SEQUENCE", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TRIGGER", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE INDEX", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheModelHasNoPendingChangeAgainstTheCommittedSnapshot()
    {
        using BankDbContext context = CreateDesignTimeContext();

        // Uses the real EF pending-model mechanism, not a constant or a migration listing.
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void IdempotentGenerationGuardsTheBaselineMigration()
    {
        using BankDbContext context = CreateDesignTimeContext();

        string script = context.GetService<IMigrator>().GenerateScript(
            options: MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains("CREATE TABLE IF NOT EXISTS", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DO $EF$", script, StringComparison.Ordinal);
        Assert.Contains("IF NOT EXISTS(SELECT 1 FROM", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("_InitialFoundation", script, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationHistoryUsesThePostgreSqlDefaultLocation()
    {
        using BankDbContext context = CreateDesignTimeContext();

        string script = context.GetService<IMigrator>().GenerateScript();

        Assert.Contains(QualifiedHistoryTable, script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMigrationBudgetIsSixtySeconds()
    {
        Assert.Equal(60, BankPersistence.MigrationTimeoutSeconds);
        Assert.Equal(TimeSpan.FromSeconds(60), BankPersistence.MigrationTimeout);
    }

    [Fact]
    public void TheApiSourceContainsNoSchemaMutationCall()
    {
        // Defence in depth only. The authoritative evidence that normal API startup leaves the
        // schema alone is the before/after check against a real PostgreSQL database in
        // MigrationBaselineTests.NormalApiStartupNeverCreatesOrChangesSchema.
        string[] forbidden = ["EnsureCreated", "EnsureCreatedAsync", ".Migrate(", "MigrateAsync"];
        DirectoryInfo apiSource = new(Path.Combine(
            RepositoryLayout.RepositoryRoot.FullName,
            "src",
            "MinimalBankSystem.Api"));

        string[] offenders =
        [
            .. apiSource
                .EnumerateFiles("*.cs", SearchOption.AllDirectories)
                .Where(file => !IsBuildOutput(file))
                .Where(file => forbidden.Any(token =>
                    StripComments(File.ReadAllText(file.FullName)).Contains(token, StringComparison.Ordinal)))
                .Select(file => file.Name),
        ];

        Assert.Empty(offenders);
    }

    private static bool IsBuildOutput(FileInfo file)
    {
        char separator = Path.DirectorySeparatorChar;

        return file.FullName.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
            || file.FullName.Contains($"{separator}bin{separator}", StringComparison.Ordinal);
    }

    /// <summary>
    /// Removes comments so the scan reacts to calls rather than to prose. Documenting that startup
    /// must not call <c>Migrate</c> is not itself a violation.
    /// </summary>
    /// <param name="source">C# source text.</param>
    private static string StripComments(string source) =>
        Regex.Replace(source, @"/\*.*?\*/|//[^\r\n]*", string.Empty, RegexOptions.Singleline);
}
