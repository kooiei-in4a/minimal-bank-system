using Npgsql;

namespace MinimalBankSystem.IntegrationTests;

public sealed class PostgreSqlCleanupFailureTests(PostgreSqlTestFixture fixture)
    : IClassFixture<PostgreSqlTestFixture>
{
    [Fact]
    [Trait("Category", PostgreSqlTestFixture.Category)]
    public async Task CleanupFailureIsSurfacedInsteadOfBeingIgnored()
    {
        await using PostgreSqlDatabaseLease database = await fixture.CreateDatabaseAsync();

        await using NpgsqlConnection adminConnection = new(fixture.AdminConnectionString);
        await adminConnection.OpenAsync();
        await using NpgsqlCommand dropCommand = adminConnection.CreateCommand();
        dropCommand.CommandText = $"DROP DATABASE \"{database.DatabaseName}\" WITH (FORCE);";
        await dropCommand.ExecuteNonQueryAsync();

        PostgreSqlFixtureException exception = await Assert.ThrowsAsync<PostgreSqlFixtureException>(
            () => database.DisposeAsync().AsTask());

        Assert.Contains("Cleanup failed", exception.Message, StringComparison.Ordinal);
    }
}
