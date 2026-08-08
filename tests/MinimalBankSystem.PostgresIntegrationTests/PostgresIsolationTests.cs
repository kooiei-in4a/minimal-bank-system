using MinimalBankSystem.PostgresIntegrationTests.Fixtures;
using Npgsql;

namespace MinimalBankSystem.PostgresIntegrationTests;

/// <summary>
/// Proves that two tests never observe each other's state.
/// </summary>
/// <remarks>
/// The two probe tests deliberately use the same table name and the same advisory lock key. If the
/// fixture ever handed them one database, the second <c>CREATE TABLE</c> would fail and the
/// advisory lock would be contended, so these tests fail loudly instead of silently sharing state.
/// The probe table is created inside the test and disappears with the database; it is not a
/// business table and no migration defines it.
/// </remarks>
[Trait(PostgresTestCategories.Category, PostgresTestCategories.PostgresIntegration)]
public sealed class PostgresIsolationTests : PostgresIntegrationTest
{
    private const long SharedAdvisoryLockKey = 41_03;

    [Fact]
    public Task FirstTestOnlySeesItsOwnRows() => AssertProbeTableIsPrivateAsync("first");

    [Fact]
    public Task SecondTestOnlySeesItsOwnRows() => AssertProbeTableIsPrivateAsync("second");

    [Fact]
    public async Task AdvisoryLocksAreScopedToTheDatabaseOwnedByTheTest()
    {
        await using NpgsqlConnection holder = await Database.OpenConnectionAsync();
        Assert.True(await TryAdvisoryLockAsync(holder, SharedAdvisoryLockKey));

        await using NpgsqlConnection sameDatabase = await Database.OpenConnectionAsync();
        Assert.False(await TryAdvisoryLockAsync(sameDatabase, SharedAdvisoryLockKey));

        await using PostgresTestDatabase otherDatabase = await Server.CreateDatabaseAsync("advisory_probe");
        await using NpgsqlConnection otherConnection = await otherDatabase.OpenConnectionAsync();

        Assert.NotEqual(Database.Name, otherDatabase.Name);
        Assert.True(await TryAdvisoryLockAsync(otherConnection, SharedAdvisoryLockKey));
    }

    private static async Task<bool> TryAdvisoryLockAsync(NpgsqlConnection connection, long key)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock($1)";
        command.Parameters.AddWithValue(key);

        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private async Task AssertProbeTableIsPrivateAsync(string marker)
    {
        await Database.ExecuteAsync("CREATE TABLE isolation_probe (marker text PRIMARY KEY)");

        await using (NpgsqlConnection connection = await Database.OpenConnectionAsync())
        await using (NpgsqlCommand insert = connection.CreateCommand())
        {
            insert.CommandText = "INSERT INTO isolation_probe (marker) VALUES ($1)";
            insert.Parameters.AddWithValue(marker);
            await insert.ExecuteNonQueryAsync();
        }

        Assert.Equal(1L, await Database.ExecuteScalarAsync<long>("SELECT count(*) FROM isolation_probe"));
        Assert.Equal(marker, await Database.ExecuteScalarAsync<string>("SELECT marker FROM isolation_probe"));
    }
}
