namespace MinimalBankSystem.IntegrationTests;

[CollectionDefinition("PostgreSQL")]
public sealed class PostgreSqlCollectionDefinition : ICollectionFixture<PostgreSqlFixture>
{
}
