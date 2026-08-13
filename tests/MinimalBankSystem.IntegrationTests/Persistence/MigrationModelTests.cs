using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using MinimalBankSystem.Infrastructure.Identity;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.IntegrationTests.Persistence;

/// <summary>
/// Model and generated-SQL checks; real PostgreSQL behavior is verified separately.
/// </summary>
/// <remarks>
/// FND-04 originally fixed this baseline to an intentionally empty <c>InitialFoundation</c>
/// migration with zero entity types. WP2-ID-01 is the first schema-owning leaf: it re-fixes this
/// baseline to the new intended shape (<c>InitialFoundation</c> unchanged and still declaring no
/// schema operation of its own, plus a committed <c>AddOperatorIdentity</c> migration) instead of
/// deleting or broadly weakening these assertions.
/// </remarks>
public sealed class MigrationModelTests
{
    private const string ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";
    private const string InitialFoundationSuffix = "_InitialFoundation";
    private const string AddOperatorIdentitySuffix = "_AddOperatorIdentity";

    private static readonly string QualifiedHistoryTable =
        $"{BankPersistence.MigrationsHistorySchema}.\"{BankPersistence.MigrationsHistoryTableName}\"";

    private const string OperatorsTable = "\"" + OperatorConfiguration.TableName + "\"";

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
    public void ExactlyTwoMigrationsAreCommittedInASingleOrderedHistory()
    {
        using BankDbContext context = CreateDesignTimeContext();

        string[] migrationIds =
            [.. context.GetService<IMigrationsAssembly>().Migrations.Keys.OrderBy(id => id, StringComparer.Ordinal)];

        Assert.Equal(2, migrationIds.Length);
        Assert.EndsWith(InitialFoundationSuffix, migrationIds[0], StringComparison.Ordinal);
        Assert.EndsWith(AddOperatorIdentitySuffix, migrationIds[1], StringComparison.Ordinal);
    }

    [Fact]
    public void TheModelDeclaresExactlyTheOperatorEntityType()
    {
        using BankDbContext context = CreateDesignTimeContext();

        IEntityType entityType = Assert.Single(context.Model.GetEntityTypes());
        Assert.Equal(typeof(Operator), entityType.ClrType);
    }

    [Fact]
    public void TheInitialFoundationMigrationStillDeclaresNoSchemaOperation()
    {
        using BankDbContext context = CreateDesignTimeContext();

        Migration migration = CreateCommittedMigration(context, InitialFoundationSuffix);

        Assert.Empty(migration.UpOperations);
        Assert.Empty(migration.DownOperations);
    }

    [Fact]
    public void TheOperatorIdentityMigrationCreatesExactlyTheOperatorsTableAndItsUniqueIndex()
    {
        using BankDbContext context = CreateDesignTimeContext();

        Migration migration = CreateCommittedMigration(context, AddOperatorIdentitySuffix);

        CreateTableOperation createTable =
            Assert.IsType<CreateTableOperation>(Assert.Single(migration.UpOperations.OfType<CreateTableOperation>()));
        Assert.Equal(OperatorConfiguration.TableName, createTable.Name);
        Assert.Null(createTable.Schema);
        Assert.Equal(
            [
                "Id", "UserName", "NormalizedUserName", "PasswordHash", "SecurityStamp",
                "Role", "State", "AuthorizationStateVersion", "CreatedAt", "UpdatedAt",
            ],
            createTable.Columns.Select(column => column.Name));
        Assert.All(createTable.Columns, column => Assert.False(column.IsNullable));
        Assert.Equal("timestamptz", createTable.Columns.Single(c => c.Name == "CreatedAt").ColumnType);
        Assert.Equal("timestamptz", createTable.Columns.Single(c => c.Name == "UpdatedAt").ColumnType);
        Assert.Equal(
            [OperatorConfiguration.RoleCheckConstraintName, OperatorConfiguration.StateCheckConstraintName],
            createTable.CheckConstraints.Select(c => c.Name).OrderBy(name => name, StringComparer.Ordinal));

        CreateIndexOperation createIndex =
            Assert.IsType<CreateIndexOperation>(Assert.Single(migration.UpOperations.OfType<CreateIndexOperation>()));
        Assert.Equal(OperatorConfiguration.NormalizedUserNameIndexName, createIndex.Name);
        Assert.Equal(OperatorConfiguration.TableName, createIndex.Table);
        Assert.Equal(["NormalizedUserName"], createIndex.Columns);
        Assert.True(createIndex.IsUnique);

        Assert.Equal(2, migration.UpOperations.Count);

        DropTableOperation dropTable = Assert.IsType<DropTableOperation>(Assert.Single(migration.DownOperations));
        Assert.Equal(OperatorConfiguration.TableName, dropTable.Name);
    }

    [Fact]
    public void TheGeneratedScriptCreatesOnlyTheMigrationHistoryAndOperatorsTables()
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

        Assert.Equal([QualifiedHistoryTable, OperatorsTable], createdTables);
        Assert.DoesNotContain("CREATE SEQUENCE", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TRIGGER", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE", script, StringComparison.OrdinalIgnoreCase);

        MatchCollection createdIndexes = Regex.Matches(script, @"CREATE\s+(?:UNIQUE\s+)?INDEX", RegexOptions.IgnoreCase);
        Assert.Single(createdIndexes);
        Assert.Contains(
            $"CREATE UNIQUE INDEX \"{OperatorConfiguration.NormalizedUserNameIndexName}\" ON {OperatorsTable} (\"NormalizedUserName\");",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheModelHasNoPendingChangeAgainstTheCommittedSnapshot()
    {
        using BankDbContext context = CreateDesignTimeContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void IdempotentGenerationGuardsBothCommittedMigrations()
    {
        using BankDbContext context = CreateDesignTimeContext();

        string script = context.GetService<IMigrator>().GenerateScript(
            options: MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains("CREATE TABLE IF NOT EXISTS", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DO $EF$", script, StringComparison.Ordinal);
        Assert.Contains("IF NOT EXISTS(SELECT 1 FROM", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(InitialFoundationSuffix, script, StringComparison.Ordinal);
        Assert.Contains(AddOperatorIdentitySuffix, script, StringComparison.Ordinal);
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

    private static Migration CreateCommittedMigration(BankDbContext context, string idSuffix)
    {
        IMigrationsAssembly migrations = context.GetService<IMigrationsAssembly>();
        KeyValuePair<string, TypeInfo> entry = migrations.Migrations.Single(
            candidate => candidate.Key.EndsWith(idSuffix, StringComparison.Ordinal));

        return migrations.CreateMigration(entry.Value, ActiveProvider);
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
