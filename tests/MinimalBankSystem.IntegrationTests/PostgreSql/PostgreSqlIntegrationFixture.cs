namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[CollectionDefinition(Name)]
public sealed class PostgreSqlIntegrationFixture : ICollectionFixture<SharedPostgreSqlContainer>
{
    public const string Name = "PostgreSqlIntegration";
}
