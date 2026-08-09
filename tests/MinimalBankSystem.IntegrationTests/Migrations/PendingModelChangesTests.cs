using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Infrastructure;
using MinimalBankSystem.IntegrationTests.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.Migrations;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class PendingModelChangesTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public void NoPendingModelChangesExistAfterTheBaselineMigration()
    {
        using BankDbContext context = new(BankDbContextOptions.Create(Database.ConnectionString));

        Assert.False(context.Database.HasPendingModelChanges());
    }
}
