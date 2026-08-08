using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class PostgreSqlFixtureTests(
    PostgreSqlContainerFixture fixture) : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task StartsPinnedPostgreSql18AndOwnsTheDatabaseLifecycle()
    {
        Assert.Equal(
            "postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a",
            PostgreSqlContainerFixture.ImageReference);
        Assert.Equal(180004, fixture.ServerVersionNumber);

        string databaseName;

        await using (PostgreSqlTestDatabase database = await fixture.CreateDatabaseAsync())
        {
            databaseName = database.DatabaseName;
            string currentDatabase = await ExecuteScalarAsync<string>(
                database.ConnectionString,
                "SELECT current_database();");

            Assert.Equal(databaseName, currentDatabase);
            Assert.True(await fixture.DatabaseExistsAsync(databaseName));
        }

        Assert.False(await fixture.DatabaseExistsAsync(databaseName));
    }

    [Fact]
    public async Task SeparateDatabasesDoNotShareProbeState()
    {
        await using PostgreSqlTestDatabase left = await fixture.CreateDatabaseAsync();
        await using PostgreSqlTestDatabase right = await fixture.CreateDatabaseAsync();

        Assert.NotEqual(left.DatabaseName, right.DatabaseName);

        await ExecuteNonQueryAsync(
            left.ConnectionString,
            "CREATE TABLE isolation_probe (value integer NOT NULL); INSERT INTO isolation_probe VALUES (41);");

        bool leftHasProbe = await ExecuteScalarAsync<bool>(
            left.ConnectionString,
            "SELECT to_regclass('public.isolation_probe') IS NOT NULL;");
        bool rightHasProbe = await ExecuteScalarAsync<bool>(
            right.ConnectionString,
            "SELECT to_regclass('public.isolation_probe') IS NOT NULL;");

        Assert.True(leftHasProbe);
        Assert.False(rightHasProbe);
    }

    [Fact]
    public async Task CleanupFailureIsReportedInsteadOfIgnored()
    {
        PostgreSqlTestDatabase database = await fixture.CreateDatabaseAsync();
        await fixture.DropDatabaseAsync(database.DatabaseName);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => database.DisposeAsync().AsTask());

        Assert.Contains("Failed to drop isolated PostgreSQL test database", exception.Message);
        Assert.Contains(database.DatabaseName, exception.Message);
    }

    private static async Task<T> ExecuteScalarAsync<T>(
        string connectionString,
        string commandText)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        object? result = await command.ExecuteScalarAsync();
        return Assert.IsType<T>(result);
    }

    private static async Task ExecuteNonQueryAsync(
        string connectionString,
        string commandText)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        await command.ExecuteNonQueryAsync();
    }
}
