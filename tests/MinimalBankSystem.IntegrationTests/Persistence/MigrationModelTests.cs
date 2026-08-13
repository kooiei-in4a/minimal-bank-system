using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.IntegrationTests.Persistence;

/// <summary>Model and generated-SQL checks; real PostgreSQL behavior is verified separately.</summary>
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

        Assert.Equal(ActiveProvider, context.Database.ProviderName);
        Assert.Equal(typeof(BankDbContext).Assembly, context.GetService<IMigrationsAssembly>().Assembly);
    }

    [Fact]
    public void FoundationAndOperatorMigrationsAreCommitted()
    {
        using BankDbContext context = CreateDesignTimeContext();

        string[] migrations = [.. context.GetService<IMigrationsAssembly>().Migrations.Keys];

        Assert.Equal(2, migrations.Length);
        Assert.EndsWith("_InitialFoundation", migrations[0], StringComparison.Ordinal);
        Assert.EndsWith("_OperatorIdentityPersistence", migrations[1], StringComparison.Ordinal);
    }

    [Fact]
    public void TheModelDeclaresOnlyTheOperatorIdentityEntity()
    {
        using BankDbContext context = CreateDesignTimeContext();

        Assert.Equal(
            ["MinimalBankSystem.Domain.Operator"],
            context.Model.GetEntityTypes().Select(entity => entity.Name).ToArray());
    }

    [Fact]
    public void TheFoundationMigrationDeclaresNoSchemaOperationAtAll()
    {
        using BankDbContext context = CreateDesignTimeContext();
        IMigrationsAssembly migrations = context.GetService<IMigrationsAssembly>();

        Migration migration = migrations.CreateMigration(
            migrations.Migrations.Values.First(),
            ActiveProvider);

        Assert.Empty(migration.UpOperations);
        Assert.Empty(migration.DownOperations);
    }

    [Fact]
    public void TheGeneratedSqlCreatesTheMigrationHistoryAndOperatorTables()
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

        Assert.Contains(QualifiedHistoryTable, createdTables);
        Assert.Contains("\"Operators\"", createdTables);
        Assert.Equal(2, createdTables.Length);
        Assert.DoesNotContain("CREATE SEQUENCE", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TRIGGER", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE INDEX", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheModelHasNoPendingChangeAgainstTheCommittedSnapshot()
    {
        using BankDbContext context = CreateDesignTimeContext();

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
        Assert.Contains("_OperatorIdentityPersistence", script, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationHistoryUsesThePostgreSqlDefaultLocation()
    {
        using BankDbContext context = CreateDesignTimeContext();

        Assert.Contains(QualifiedHistoryTable, context.GetService<IMigrator>().GenerateScript(), StringComparison.Ordinal);
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

    private static string StripComments(string source) =>
        Regex.Replace(source, @"/\*.*?\*/|//[^\r\n]*", string.Empty, RegexOptions.Singleline);
}
