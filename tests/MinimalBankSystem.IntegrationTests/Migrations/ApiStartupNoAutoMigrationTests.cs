using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.IntegrationTests.PostgreSql;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.Migrations;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class ApiStartupNoAutoMigrationTests(PostgreSqlContainerFixture fixture)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private readonly PostgreSqlContainerFixture fixture = fixture;

    [Fact]
    public async Task NormalApiStartupDoesNotCreateMigrationHistoryOrApplicationSchema()
    {
        await using PostgreSqlTestDatabase database = await fixture.CreateDatabaseAsync();

        Assert.False(await MigrationHistoryTableExistsAsync(database.ConnectionString));
        Assert.Empty(await QueryUserTablesAsync(database.ConnectionString));

        using ApiWithRealConnectionFactory factory = new(database.ConnectionString);

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            BankDbContext dbContext = scope.ServiceProvider.GetRequiredService<BankDbContext>();
            bool canConnect = await dbContext.Database.CanConnectAsync();
            Assert.True(canConnect);
        }

        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/__no-such-endpoint");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);

        Assert.False(await MigrationHistoryTableExistsAsync(database.ConnectionString));
        Assert.Empty(await QueryUserTablesAsync(database.ConnectionString));
    }

    private static async Task<bool> MigrationHistoryTableExistsAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL;",
            connection);
        object? result = await command.ExecuteScalarAsync();
        return result is true;
    }

    private static async Task<string[]> QueryUserTablesAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name;",
            connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        List<string> tableNames = [];

        while (await reader.ReadAsync())
        {
            tableNames.Add(reader.GetString(0));
        }

        return [.. tableNames];
    }
}

internal sealed class ApiWithRealConnectionFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("ConnectionStrings:Database", connectionString),
            ]);
        });
    }
}
