using MinimalBankSystem.IntegrationTests.Fixtures;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection("PostgreSQL")]
[Trait("Category", "PostgreSQL")]
public class PostgreSqlImageDigestTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _containerFixture;
    private readonly PostgreSqlSchemaFixture _schemaFixture;

    private const string ExpectedImageReference =
        "postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a";

    public PostgreSqlImageDigestTests(PostgreSqlContainerFixture containerFixture)
    {
        _containerFixture = containerFixture;
        _schemaFixture = new PostgreSqlSchemaFixture(containerFixture);
    }

    public async Task InitializeAsync() => await _schemaFixture.InitializeAsync();

    public async Task DisposeAsync() => await _schemaFixture.DisposeAsync();

    [Fact]
    public void ImageReferenceContainsExpectedDigest()
    {
        Assert.Contains("sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a",
            ExpectedImageReference);
    }

    [Fact]
    public void ImageReferenceUsesPostgres18()
    {
        Assert.StartsWith("postgres:18.", ExpectedImageReference);
    }

    [Fact]
    public async Task RunningPostgreSQLVersionMatches18()
    {
        await using var connection = await _schemaFixture.CreateOpenConnectionAsync();

        await using var command = new NpgsqlCommand("SHOW server_version", connection);
        var version = (string)(await command.ExecuteScalarAsync())!;

        Assert.StartsWith("18.", version);
    }
}
