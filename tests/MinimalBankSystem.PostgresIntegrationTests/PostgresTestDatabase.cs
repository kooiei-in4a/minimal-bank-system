namespace MinimalBankSystem.PostgresIntegrationTests;

/// <summary>
/// One isolated PostgreSQL database owned by a single test. Disposal always attempts a
/// forced drop so a test cannot leak a database into the shared container; cleanup
/// failures propagate from <see cref="DisposeAsync"/> instead of being swallowed.
/// </summary>
public sealed class PostgresTestDatabase : IAsyncDisposable
{
    private readonly PostgresContainerFixture fixture;
    private bool disposed;

    internal PostgresTestDatabase(PostgresContainerFixture fixture, string name, string connectionString)
    {
        this.fixture = fixture;
        Name = name;
        ConnectionString = connectionString;
    }

    public string Name { get; }

    public string ConnectionString { get; }

    public Task DropAsync(bool force, CancellationToken cancellationToken = default) =>
        fixture.DropDatabaseAsync(Name, force, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await DropAsync(force: true);
    }
}
