namespace MinimalBankSystem.PostgresIntegrationTests.Fixtures;

/// <summary>
/// The one test collection allowed to touch cluster-wide PostgreSQL state.
/// </summary>
/// <remarks>
/// <para>
/// Tests in a single xUnit collection never run concurrently with each other, so joining this
/// collection is how a test declares "I am not safe to run beside another cluster-scoped test".
/// Use it for tests that start additional containers, drop databases out of band, terminate
/// backends, create roles or run <c>ALTER SYSTEM</c>.
/// </para>
/// <para>
/// Members must still tolerate unrelated per-test databases existing on the server, because
/// database-scoped tests keep running in parallel with this collection.
/// </para>
/// </remarks>
[CollectionDefinition(Name)]
public sealed class PostgresClusterScope
{
    /// <summary>The collection name.</summary>
    public const string Name = "PostgreSQL cluster-scoped";
}
