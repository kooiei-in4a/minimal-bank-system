using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection(PostgreSqlTestCollections.Name)]
[Trait(PostgreSqlTestCollections.CategoryTraitName, PostgreSqlTestCollections.CategoryTraitValue)]
public sealed class PostgreSqlStartupFailureTests
{
    [Fact]
    public async Task ContainerStartupFailureIsReportedAsATestFailure()
    {
        const string bogusDigestReference =
            "postgres:18.4@sha256:0000000000000000000000000000000000000000000000000000000000000000";

        PostgreSqlContainer container = PostgreSqlContainerFixture.CreateContainer(bogusDigestReference);
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => container.StartAsync());
        }
        finally
        {
            await container.DisposeAsync();
        }
    }
}
