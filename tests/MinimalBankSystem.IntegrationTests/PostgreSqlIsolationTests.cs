namespace MinimalBankSystem.IntegrationTests;

[Collection("PostgreSQL")]
[Trait("Category", "PostgreSQL")]
public sealed class PostgreSqlIsolationTestA : PostgreSqlIsolatedTestBase
{
    public PostgreSqlIsolationTestA(PostgreSqlFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task CreatesTableAndInsertsDataInOwnDatabase()
    {
        var createResult = await Fixture.ExecuteSqlAsync(DatabaseName,
            "CREATE TABLE isolation_test_a (id INT PRIMARY KEY, value TEXT);");
        Assert.Equal(0, createResult.ExitCode);

        var insertResult = await Fixture.ExecuteSqlAsync(DatabaseName,
            "INSERT INTO isolation_test_a (id, value) VALUES (1, 'data-from-a');");
        Assert.Equal(0, insertResult.ExitCode);

        var selectResult = await Fixture.ExecuteSqlAsync(DatabaseName,
            "SELECT value FROM isolation_test_a WHERE id = 1;");
        Assert.Equal(0, selectResult.ExitCode);
        Assert.Contains("data-from-a", selectResult.Stdout);
    }

    [Fact]
    public async Task UsesUniqueDatabaseName()
    {
        Assert.Contains(GetType().Name, DatabaseName);
        Assert.DoesNotContain(nameof(PostgreSqlIsolationTestB), DatabaseName);
    }
}

[Collection("PostgreSQL")]
[Trait("Category", "PostgreSQL")]
public sealed class PostgreSqlIsolationTestB : PostgreSqlIsolatedTestBase
{
    public PostgreSqlIsolationTestB(PostgreSqlFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task CreatesTableAndInsertsDataInOwnDatabase()
    {
        var createResult = await Fixture.ExecuteSqlAsync(DatabaseName,
            "CREATE TABLE isolation_test_b (id INT PRIMARY KEY, value TEXT);");
        Assert.Equal(0, createResult.ExitCode);

        var insertResult = await Fixture.ExecuteSqlAsync(DatabaseName,
            "INSERT INTO isolation_test_b (id, value) VALUES (1, 'data-from-b');");
        Assert.Equal(0, insertResult.ExitCode);

        var selectResult = await Fixture.ExecuteSqlAsync(DatabaseName,
            "SELECT value FROM isolation_test_b WHERE id = 1;");
        Assert.Equal(0, selectResult.ExitCode);
        Assert.Contains("data-from-b", selectResult.Stdout);
    }

    [Fact]
    public async Task UsesUniqueDatabaseName()
    {
        Assert.Contains(GetType().Name, DatabaseName);
        Assert.DoesNotContain(nameof(PostgreSqlIsolationTestA), DatabaseName);
    }
}
