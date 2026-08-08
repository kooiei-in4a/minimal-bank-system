using Xunit;

namespace MinimalBankSystem.IntegrationTests.Fixtures;

/// <summary>
/// Collection definition for PostgreSQL integration tests.
/// Tests in this collection run sequentially within the collection,
/// but different collections can run in parallel.
/// </summary>
[CollectionDefinition("PostgreSqlCollection")]
public sealed class PostgreSqlCollectionDefinition : ICollectionFixture<PostgreSqlFixture>
{
}
