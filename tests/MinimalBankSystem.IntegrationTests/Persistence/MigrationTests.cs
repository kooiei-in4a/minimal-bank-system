using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;
using MinimalBankSystem.IntegrationTests.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.Persistence;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class MigrationTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task CleanDatabaseMigrationUsesEmptyFoundationAndRecordsHistory()
    {
        ConfigurationManager configuration = new();
        configuration["ConnectionStrings:Database"] = Database.ConnectionString;

        int exitCode = await MigratorApplication.RunAsync(
            configuration,
            TextWriter.Null);

        Assert.Equal(0, exitCode);
        Assert.True(await RelationExistsAsync("__EFMigrationsHistory"));
        Assert.Equal(
            ["20260809112429_InitialFoundation"],
            await ReadMigrationIdsAsync());
        Assert.Equal(0, await CountApplicationTablesAsync());

        DbContextOptionsBuilder<BankDbContext> options = new();
        BankDbContextConfiguration.Configure(options, Database.ConnectionString);
        await using BankDbContext dbContext = new(options.Options);

        Assert.False(dbContext.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task MigratorConnectionFailureReturnsNonZero()
    {
        NpgsqlConnectionStringBuilder unreachable = new(Database.ConnectionString)
        {
            Host = "127.0.0.1",
            Port = 1,
            Timeout = 1,
            CommandTimeout = 1,
        };
        ConfigurationManager configuration = new();
        configuration["ConnectionStrings:Database"] = unreachable.ConnectionString;
        using StringWriter errors = new();

        int exitCode = await MigratorApplication.RunAsync(configuration, errors);

        Assert.Equal(1, exitCode);
        Assert.Contains("Database migration failed", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalApiStartupDoesNotCreateMigrationHistoryOrApplicationTables()
    {
        Assert.False(await RelationExistsAsync("__EFMigrationsHistory"));
        Assert.Equal(0, await CountApplicationTablesAsync());

        using NoAutoMigrationWebApplicationFactory factory =
            new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(await RelationExistsAsync("__EFMigrationsHistory"));
        Assert.Equal(0, await CountApplicationTablesAsync());
    }

    private async Task<string[]> ReadMigrationIdsAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";",
            connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        List<string> migrationIds = [];

        while (await reader.ReadAsync())
        {
            migrationIds.Add(reader.GetString(0));
        }

        return [.. migrationIds];
    }

    private async Task<bool> RelationExistsAsync(string relationName)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT to_regclass($1) IS NOT NULL;",
            connection);
        command.Parameters.AddWithValue($"public.\"{relationName}\"");
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private async Task<int> CountApplicationTablesAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT COUNT(*) FROM pg_class " +
            "WHERE relnamespace = 'public'::regnamespace " +
            "AND relkind IN ('r', 'p') " +
            "AND relname <> '__EFMigrationsHistory';",
            connection);
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class NoAutoMigrationWebApplicationFactory(string connectionString)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Database", connectionString);
        }
    }
}
