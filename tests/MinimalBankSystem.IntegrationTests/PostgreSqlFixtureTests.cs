using Npgsql;

namespace MinimalBankSystem.IntegrationTests;

public sealed class PostgreSqlFixtureTests : PostgreSqlTestBase, IClassFixture<PostgreSqlFixture>
{
    public PostgreSqlFixtureTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    [Trait("Category", "PostgreSql")]
    public async Task FixtureUsesDigestPinnedPostgreSql18AndProvidesARealConnection()
    {
        Assert.Equal(PostgreSqlFixture.ImageReference, Fixture.Container.Image.FullName);

        await using NpgsqlConnection connection = await Database.OpenConnectionAsync();
        await using NpgsqlCommand command = new(
            "SELECT current_database(), current_setting('server_version');",
            connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal(Database.DatabaseName, reader.GetString(0));
        Assert.StartsWith("18.", reader.GetString(1), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "PostgreSql")]
    public async Task DisposingTheDatabaseHandleRemovesTheDatabase()
    {
        PostgreSqlTestDatabase database = await Fixture.CreateDatabaseAsync();
        string connectionString = database.ConnectionString;

        await database.DisposeAsync();

        await using NpgsqlConnection connection = new(connectionString);
        await Assert.ThrowsAnyAsync<Exception>(() => connection.OpenAsync());
    }

    [Fact]
    [Trait("Category", "PostgreSql")]
    public async Task DatabasesDoNotShareTestState()
    {
        List<PostgreSqlTestDatabase> databases = [];

        try
        {
            databases.Add(await Fixture.CreateDatabaseAsync());
            databases.Add(await Fixture.CreateDatabaseAsync());
            PostgreSqlTestDatabase first = databases[0];
            PostgreSqlTestDatabase second = databases[1];

            await using (NpgsqlConnection firstConnection = await first.OpenConnectionAsync())
            await using (NpgsqlCommand createTable = new(
                             "CREATE TABLE fixture_probe (value integer NOT NULL);",
                             firstConnection))
            {
                await createTable.ExecuteNonQueryAsync();
            }

            await using NpgsqlConnection secondConnection = await second.OpenConnectionAsync();
            await using NpgsqlCommand checkTable = new(
                "SELECT current_database(), to_regclass('public.fixture_probe')::text;",
                secondConnection);

            await using NpgsqlDataReader reader = await checkTable.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(second.DatabaseName, reader.GetString(0));
            Assert.True(reader.IsDBNull(1));
        }
        finally
        {
            await DisposeDatabasesAsync(databases.ToArray());
        }
    }

    [Fact]
    [Trait("Category", "PostgreSql")]
    public async Task IndependentDatabasesCanExecuteConcurrently()
    {
        List<PostgreSqlTestDatabase> databases = [];

        try
        {
            for (int index = 0; index < 4; index++)
            {
                databases.Add(await Fixture.CreateDatabaseAsync());
            }

            int[] rowCounts = await Task.WhenAll(databases.Select(async database =>
            {
                await using NpgsqlConnection connection = await database.OpenConnectionAsync();
                await using NpgsqlCommand command = new(
                    "CREATE TABLE fixture_parallel_probe (value integer NOT NULL); " +
                    "INSERT INTO fixture_parallel_probe (value) VALUES (1); " +
                    "SELECT count(*) FROM fixture_parallel_probe;",
                    connection);

                object? result = await command.ExecuteScalarAsync();
                return checked((int)Assert.IsType<long>(result));
            }));

            Assert.Equal([1, 1, 1, 1], rowCounts);
        }
        finally
        {
            await DisposeDatabasesAsync(databases.ToArray());
        }
    }

    [Fact]
    [Trait("Category", "PostgreSql")]
    public async Task ConnectionFailureIsReportedAsFixtureFailure()
    {
        NpgsqlConnectionStringBuilder connectionString =
            new(Fixture.Container.GetConnectionString())
            {
                Host = "127.0.0.1",
                Port = 1,
                Timeout = 1,
            };

        PostgreSqlFixtureException exception = await Assert.ThrowsAsync<PostgreSqlFixtureException>(
            () => PostgreSqlFixture.OpenConnectionAsync(connectionString.ConnectionString, "intentional failure test"));

        Assert.Contains("connection failed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("intentional failure test", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "PostgreSql")]
    public async Task CleanupFailureIsReportedRatherThanIgnored()
    {
        PostgreSqlFixtureException exception = await Assert.ThrowsAsync<PostgreSqlFixtureException>(
            () => Fixture.DropDatabaseAsync($"test_missing_{Guid.NewGuid():N}"));

        Assert.Contains("clean up", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task DisposeDatabasesAsync(params PostgreSqlTestDatabase[] databases)
    {
        List<Exception> failures = [];

        foreach (PostgreSqlTestDatabase database in databases)
        {
            try
            {
                await database.DisposeAsync();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("One or more PostgreSQL test database cleanups failed.", failures);
        }
    }
}
