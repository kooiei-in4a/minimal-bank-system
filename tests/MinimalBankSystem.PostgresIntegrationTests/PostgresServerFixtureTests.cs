using System.Globalization;
using MinimalBankSystem.PostgresIntegrationTests.Fixtures;

namespace MinimalBankSystem.PostgresIntegrationTests;

/// <summary>
/// Proves that the fixture really runs the pinned PostgreSQL 18 image and hands each test an
/// empty database of its own.
/// </summary>
[Trait(PostgresTestCategories.Category, PostgresTestCategories.PostgresIntegration)]
public sealed class PostgresServerFixtureTests : PostgresIntegrationTest
{
    [Fact]
    public void TheRunningContainerUsesThePinnedDigest()
    {
        Assert.Equal(PostgresTestImage.Digest, Server.Image.Digest);
        Assert.Equal("18.4", Server.Image.Tag);
        Assert.Contains(PostgresTestImage.Digest, Server.Image.FullName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheServerIsRealPostgres18()
    {
        string version = await Database.ExecuteScalarAsync<string>("SELECT version()");
        string serverVersionNumber =
            await Database.ExecuteScalarAsync<string>("SELECT current_setting('server_version_num')");

        Assert.StartsWith(PostgresTestImage.ExpectedVersionPrefix, version, StringComparison.Ordinal);
        Assert.True(
            int.Parse(serverVersionNumber, CultureInfo.InvariantCulture) >=
                PostgresTestImage.MinimumServerVersionNumber,
            $"Expected at least server_version_num {PostgresTestImage.MinimumServerVersionNumber}, " +
            $"but the server reported {serverVersionNumber}.");
    }

    [Fact]
    public async Task TheTestRunsInsideItsOwnEmptyDatabase()
    {
        string currentDatabase = await Database.ExecuteScalarAsync<string>("SELECT current_database()");
        long userTables = await Database.ExecuteScalarAsync<long>(
            """
            SELECT count(*)
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r'
              AND n.nspname NOT IN ('pg_catalog', 'information_schema')
            """);

        Assert.StartsWith(PostgresTestServer.DatabaseNamePrefix, Database.Name, StringComparison.Ordinal);
        Assert.Equal(Database.Name, currentDatabase);
        Assert.Equal(0L, userTables);
    }
}
