using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Auditing;
using MinimalBankSystem.Infrastructure.Persistence.Identity;

namespace MinimalBankSystem.IntegrationTests.Persistence;

/// <summary>Model and generated-SQL checks; real PostgreSQL behavior is verified separately.</summary>
public sealed class MigrationModelTests
{
    private const string ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";
    private const string FoundationMigration = "20260809113338_InitialFoundation";

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
    public void CommittedMigrationsAreFoundationOperatorIdentityThenProductAudit()
    {
        using BankDbContext context = CreateDesignTimeContext();
        string[] migrations = [.. context.GetService<IMigrationsAssembly>().Migrations.Keys];

        Assert.Equal(
            [FoundationMigration, OperatorPersistence.IdentityMigrationId, AuditPersistence.AuditMigrationId],
            migrations);
    }

    [Fact]
    public void TheModelDeclaresOnlyOperatorAndProductAuditWithoutALiveActorForeignKey()
    {
        using BankDbContext context = CreateDesignTimeContext();
        IEntityType[] entityTypes = [.. context.Model.GetEntityTypes()];

        Assert.Equal(2, entityTypes.Length);

        IEntityType operatorType = Assert.Single(entityTypes, type => type.ClrType == typeof(Operator));
        Assert.Equal(typeof(Operator), operatorType.ClrType);
        Assert.Equal(OperatorPersistence.TableName, operatorType.GetTableName());
        Assert.Null(operatorType.FindNavigation("Roles"));
        Assert.Empty(operatorType.GetReferencingForeignKeys());
        Assert.Contains(
            operatorType.GetIndexes(),
            index => index.IsUnique
                && index.Properties.Count == 1
                && index.Properties[0].Name == nameof(Operator.NormalizedUserName));
        Assert.DoesNotContain(
            operatorType.GetIndexes(),
            index => index.Properties.Any(property => property.Name == nameof(Operator.UserName)));
        Assert.DoesNotContain(
            entityTypes,
            entity => entity.ClrType.Name.Contains("IdentityRole", StringComparison.Ordinal)
                || entity.GetTableName() is "AspNetRoles" or "AspNetUserRoles" or "AspNetUsers");

        IEntityType auditType = Assert.Single(entityTypes, type => type.ClrType == typeof(AuditRecord));
        Assert.Equal(AuditPersistence.TableName, auditType.GetTableName());
        Assert.Empty(auditType.GetForeignKeys());
        Assert.Empty(auditType.GetNavigations());
        Assert.Equal(
            "timestamp with time zone",
            auditType.FindProperty(nameof(AuditRecord.AuditTime))!.GetColumnType());
    }

    [Fact]
    public void TheFoundationMigrationStillDeclaresNoSchemaOperation()
    {
        using BankDbContext context = CreateDesignTimeContext();
        IMigrationsAssembly migrations = context.GetService<IMigrationsAssembly>();

        Migration migration = migrations.CreateMigration(
            migrations.Migrations[FoundationMigration],
            ActiveProvider);

        Assert.Empty(migration.UpOperations);
        Assert.Empty(migration.DownOperations);
    }

    [Fact]
    public void TheOperatorIdentityMigrationCreatesTheOperatorTableAndConstraints()
    {
        using BankDbContext context = CreateDesignTimeContext();
        IMigrationsAssembly migrations = context.GetService<IMigrationsAssembly>();

        Migration migration = migrations.CreateMigration(
            migrations.Migrations[OperatorPersistence.IdentityMigrationId],
            ActiveProvider);

        CreateTableOperation createTable = Assert.Single(migration.UpOperations.OfType<CreateTableOperation>());
        Assert.Equal(OperatorPersistence.TableName, createTable.Name);
        Assert.Contains(createTable.CheckConstraints, constraint => constraint.Name == OperatorPersistence.StateCheckConstraint);
        Assert.Contains(createTable.CheckConstraints, constraint => constraint.Name == OperatorPersistence.RoleCheckConstraint);
        Assert.Contains(createTable.Columns, column => column.Name == OperatorPersistence.FixedRoleColumn && !column.IsNullable);
        Assert.Contains(createTable.Columns, column => column.Name == OperatorPersistence.AuthorizationStateVersionColumn);
        Assert.Contains(createTable.Columns, column => column.Name == OperatorPersistence.SecurityStampColumn);

        DropTableOperation dropTable = Assert.Single(migration.DownOperations.OfType<DropTableOperation>());
        Assert.Equal(OperatorPersistence.TableName, dropTable.Name);
    }

    [Fact]
    public void TheAuditMigrationCreatesTheApprovedTableTriggerAndSafeRollbackBoundary()
    {
        using BankDbContext context = CreateDesignTimeContext();
        IMigrationsAssembly migrations = context.GetService<IMigrationsAssembly>();

        Migration migration = migrations.CreateMigration(
            migrations.Migrations[AuditPersistence.AuditMigrationId],
            ActiveProvider);

        CreateTableOperation createTable = Assert.Single(migration.UpOperations.OfType<CreateTableOperation>());
        Assert.Equal(AuditPersistence.TableName, createTable.Name);
        Assert.Contains(createTable.Columns, column => column.Name == AuditPersistence.IdColumn && !column.IsNullable);
        Assert.Contains(createTable.Columns, column => column.Name == AuditPersistence.ActorIdentifierColumn && !column.IsNullable);
        Assert.Contains(createTable.Columns, column => column.Name == AuditPersistence.FailureBusinessErrorCodeColumn && column.IsNullable);
        Assert.Contains(createTable.CheckConstraints, constraint => constraint.Name == AuditPersistence.ActorRoleCheckConstraint);
        Assert.Contains(createTable.CheckConstraints, constraint => constraint.Name == AuditPersistence.ResultCheckConstraint);

        SqlOperation triggerSql = Assert.Single(migration.UpOperations.OfType<SqlOperation>());
        Assert.Contains($"CREATE FUNCTION public.{AuditPersistence.AppendOnlyFunction}", triggerSql.Sql, StringComparison.Ordinal);
        Assert.Contains($"CREATE TRIGGER {AuditPersistence.AppendOnlyTrigger}", triggerSql.Sql, StringComparison.Ordinal);
        Assert.Contains("BEFORE UPDATE OR DELETE", triggerSql.Sql, StringComparison.Ordinal);
        Assert.Contains(AuditPersistence.AppendOnlyErrorMarker, triggerSql.Sql, StringComparison.Ordinal);

        DropTableOperation dropTable = Assert.Single(migration.DownOperations.OfType<DropTableOperation>());
        Assert.Equal(AuditPersistence.TableName, dropTable.Name);
        SqlOperation[] downSql = [.. migration.DownOperations.OfType<SqlOperation>()];
        Assert.Equal(2, downSql.Length);
        Assert.Contains(downSql, operation => operation.Sql.Contains(
            AuditPersistence.RollbackRequiresRestoreMarker,
            StringComparison.Ordinal));
        Assert.Contains(downSql, operation => operation.Sql.Contains(
            $"DROP FUNCTION public.{AuditPersistence.AppendOnlyFunction}",
            StringComparison.Ordinal));
    }

    [Fact]
    public void TheGeneratedSqlCreatesTheIntendedAuditTriggerWithoutUnrelatedSchemaDrift()
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

        Assert.Equal(
            [QualifiedHistoryTable, OperatorPersistence.TableName, AuditPersistence.TableName],
            createdTables);
        Assert.Contains("ck_operators_state", script, StringComparison.Ordinal);
        Assert.Contains("ck_operators_fixed_role", script, StringComparison.Ordinal);
        Assert.Contains("timestamp with time zone", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AspNetRoles", script, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetUserRoles", script, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetUsers", script, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE SEQUENCE", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"CREATE FUNCTION public.{AuditPersistence.AppendOnlyFunction}", script, StringComparison.Ordinal);
        Assert.Contains($"CREATE TRIGGER {AuditPersistence.AppendOnlyTrigger}", script, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(script, @"CREATE\s+TRIGGER", RegexOptions.IgnoreCase));
    }

    [Fact]
    public void TheModelHasNoPendingChangeAgainstTheCommittedSnapshot()
    {
        using BankDbContext context = CreateDesignTimeContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void IdempotentGenerationGuardsFoundationOperatorIdentityAndAuditMigrations()
    {
        using BankDbContext context = CreateDesignTimeContext();

        string script = context.GetService<IMigrator>().GenerateScript(
            options: MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains("CREATE TABLE IF NOT EXISTS", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DO $EF$", script, StringComparison.Ordinal);
        Assert.Contains("IF NOT EXISTS(SELECT 1 FROM", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("_InitialFoundation", script, StringComparison.Ordinal);
        Assert.Contains("_AddOperatorIdentity", script, StringComparison.Ordinal);
        Assert.Contains("_AddProductAudit", script, StringComparison.Ordinal);
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

    [Fact]
    public void TheApiDoesNotInstallIdentityRoleOrSecondIdentityPersistenceModel()
    {
        string apiProgram = File.ReadAllText(Path.Combine(
            RepositoryLayout.RepositoryRoot.FullName,
            "src",
            "MinimalBankSystem.Api",
            "Program.cs"));

        Assert.DoesNotContain("IPasswordHasher<Operator>", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddIdentityCore", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddIdentity<", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("IdentityDbContext", apiProgram, StringComparison.Ordinal);
        Assert.Contains("AddAuthentication", apiProgram, StringComparison.Ordinal);
        Assert.Contains("UseAuthentication", apiProgram, StringComparison.Ordinal);
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
