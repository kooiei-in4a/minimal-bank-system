namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection(PostgreSqlTestCollections.Name)]
[Trait(PostgreSqlTestCollections.CategoryTraitName, PostgreSqlTestCollections.CategoryTraitValue)]
public sealed class PostgreSqlContainerLifecycleTests(PostgreSqlContainerFixture container)
{
    [Fact]
    public async Task ContainerUsesThePinnedPostgreSql18ImageDigest()
    {
        await container.EnsureStartedAsync();

        Assert.Equal("postgres", container.ImageRepository);
        Assert.Equal("18.4", container.ImageTag);
        Assert.Equal(
            "sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a",
            container.ImageDigest);
    }

    [Fact]
    public async Task RealPostgreSql18ServerAcceptsConnections()
    {
        await container.EnsureStartedAsync();

        object? serverVersionNumber = await PostgreSqlTestSql.ExecuteScalarAsync(
            container.AdminConnectionString,
            "SHOW server_version_num");

        Assert.Equal("180004", serverVersionNumber);
    }
}
