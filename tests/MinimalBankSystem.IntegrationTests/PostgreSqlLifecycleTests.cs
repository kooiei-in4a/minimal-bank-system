namespace MinimalBankSystem.IntegrationTests;

[Collection("PostgreSQL")]
[Trait("Category", "PostgreSQL")]
public sealed class PostgreSqlLifecycleTests
{
    private readonly PostgreSqlFixture _fixture;

    public PostgreSqlLifecycleTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ContainerStartsAndAcceptsSqlConnection()
    {
        var result = await _fixture.ExecuteSqlAsync(
            PostgreSqlFixture.FixedDatabase,
            "SELECT 1 AS one;");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("1", result.Stdout);
    }

    [Fact]
    public void ImageIsFixedToSpecifiedDigest()
    {
        Assert.Equal(PostgreSqlFixture.FixedImage, _fixture.ImageFullName);
    }

    [Fact]
    public void ConnectionStringIsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(_fixture.ConnectionString));
    }
}
