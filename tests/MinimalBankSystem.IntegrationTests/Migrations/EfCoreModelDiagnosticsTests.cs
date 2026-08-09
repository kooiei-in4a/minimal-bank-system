using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.IntegrationTests.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.Migrations;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class EfCoreModelDiagnosticsTests(PostgreSqlContainerFixture fixture)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private readonly PostgreSqlContainerFixture fixture = fixture;

    [Fact]
    public async Task UnmodifiedBankDbContextHasNoPendingModelChanges()
    {
        await using PostgreSqlTestDatabase database = await fixture.CreateDatabaseAsync();
        await using BankDbContext dbContext = CreateContext(database.ConnectionString);

        Assert.False(dbContext.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task ModelDriftAgainstTheAppliedMigrationsIsDetected()
    {
        await using PostgreSqlTestDatabase database = await fixture.CreateDatabaseAsync();
        DbContextOptions options = new DbContextOptionsBuilder()
            .UseBankPostgreSql(database.ConnectionString)
            .Options;
        await using DriftedBankDbContext dbContext = new(options);

        Assert.True(dbContext.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task IdempotentScriptGenerationCoversTheInitialFoundationMigration()
    {
        await using PostgreSqlTestDatabase database = await fixture.CreateDatabaseAsync();
        await using BankDbContext dbContext = CreateContext(database.ConnectionString);

        IMigrator migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        string script = migrator.GenerateScript(null, null, MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains("InitialFoundation", script, StringComparison.Ordinal);
        Assert.Contains("__EFMigrationsHistory", script, StringComparison.Ordinal);
        Assert.Contains("IF NOT EXISTS", script, StringComparison.OrdinalIgnoreCase);
    }

    private static BankDbContext CreateContext(string connectionString)
    {
        DbContextOptions options = new DbContextOptionsBuilder()
            .UseBankPostgreSql(connectionString)
            .Options;
        return new BankDbContext(options);
    }

    private sealed class DriftedBankDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<DriftProbeEntity> DriftProbe => Set<DriftProbeEntity>();
    }

    private sealed class DriftProbeEntity
    {
        public int Id { get; set; }
    }
}
