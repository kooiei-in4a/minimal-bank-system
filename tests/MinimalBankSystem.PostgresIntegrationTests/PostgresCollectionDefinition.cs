namespace MinimalBankSystem.PostgresIntegrationTests;

/// <summary>
/// Groups every test that reuses the one shared <see cref="PostgresContainerFixture"/>
/// container. xUnit runs test classes within the same collection sequentially, so tests
/// here are serialized against each other; they stay independent through per-test
/// databases rather than through xUnit-level parallelism. <see cref="ContainerFailureTests"/>
/// deliberately stays outside this collection because it starts and stops its own
/// container and must not disturb the shared one; it therefore runs in xUnit's default
/// parallel scheduling alongside this collection.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "PostgreSQL container collection";
}
