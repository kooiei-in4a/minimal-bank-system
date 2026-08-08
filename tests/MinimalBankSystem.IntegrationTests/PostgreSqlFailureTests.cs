namespace MinimalBankSystem.IntegrationTests;

[Collection("PostgreSQL")]
[Trait("Category", "PostgreSQL")]
public sealed class PostgreSqlFailureTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;

    public PostgreSqlFailureTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task InvalidSqlReturnsNonZeroExitCode()
    {
        var result = await _fixture.ExecuteSqlAsync(
            PostgreSqlFixture.FixedDatabase,
            "INVALID SQL STATEMENT;");

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Stderr));
    }

    [Fact]
    public async Task ConnectionToNonExistentDatabaseFails()
    {
        var result = await _fixture.ExecuteSqlAsync(
            "nonexistent_database_xyz",
            "SELECT 1;");

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task DroppingNonExistentDatabaseWithIfExistsDoesNotFail()
    {
        var result = await _fixture.ExecuteSqlAsync(
            PostgreSqlFixture.FixedDatabase,
            "DROP DATABASE IF EXISTS nonexistent_db_xyz WITH (FORCE);");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task CannotConnectToDroppedDatabase()
    {
        var dbName = $"test_drop_{Guid.NewGuid():N}";

        var createResult = await _fixture.ExecuteSqlAsync(
            PostgreSqlFixture.FixedDatabase,
            $"CREATE DATABASE \"{dbName}\";");
        Assert.Equal(0, createResult.ExitCode);

        var selectResult = await _fixture.ExecuteSqlAsync(dbName, "SELECT 1;");
        Assert.Equal(0, selectResult.ExitCode);

        var dropResult = await _fixture.ExecuteSqlAsync(
            PostgreSqlFixture.FixedDatabase,
            $"DROP DATABASE \"{dbName}\" WITH (FORCE);");
        Assert.Equal(0, dropResult.ExitCode);

        var connectResult = await _fixture.ExecuteSqlAsync(dbName, "SELECT 1;");
        Assert.NotEqual(0, connectResult.ExitCode);
    }

    [Fact]
    public async Task CreatingDuplicateDatabaseFails()
    {
        var dbName = $"test_dup_{Guid.NewGuid():N}";

        var createResult = await _fixture.ExecuteSqlAsync(
            PostgreSqlFixture.FixedDatabase,
            $"CREATE DATABASE \"{dbName}\";");
        Assert.Equal(0, createResult.ExitCode);

        try
        {
            var dupResult = await _fixture.ExecuteSqlAsync(
                PostgreSqlFixture.FixedDatabase,
                $"CREATE DATABASE \"{dbName}\";");
            Assert.NotEqual(0, dupResult.ExitCode);
        }
        finally
        {
            await _fixture.ExecuteSqlAsync(
                PostgreSqlFixture.FixedDatabase,
                $"DROP DATABASE IF EXISTS \"{dbName}\" WITH (FORCE);");
        }
    }
}
