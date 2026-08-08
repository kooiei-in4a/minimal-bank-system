namespace MinimalBankSystem.PostgresIntegrationTests.Fixtures;

/// <summary>
/// Base class for a test that needs a real, isolated PostgreSQL database.
/// </summary>
/// <remarks>
/// <para>
/// xUnit creates a new instance of a test class for every test method, so
/// <see cref="InitializeAsync"/> and <see cref="DisposeAsync"/> run once per test. That makes the
/// database lifecycle automatic: a test starts with an empty database and ends with that database
/// dropped, without any per-test bookkeeping.
/// </para>
/// <para>
/// Parallel policy: derived classes may run in parallel with each other, because each test only
/// touches its own database. Tests that mutate cluster-wide state (roles, <c>ALTER SYSTEM</c>,
/// other backends) must instead join <see cref="PostgresClusterCollection"/> so they are
/// serialized against each other.
/// </para>
/// </remarks>
public abstract class PostgresIntegrationTest : IAsyncLifetime
{
    private PostgresTestServer? server;
    private PostgresTestDatabase? database;

    /// <summary>The assembly-wide PostgreSQL server.</summary>
    protected PostgresTestServer Server =>
        server ?? throw new InvalidOperationException(
            "The PostgreSQL test server is only available between InitializeAsync and DisposeAsync.");

    /// <summary>The database owned by the currently running test.</summary>
    protected PostgresTestDatabase Database =>
        database ?? throw new InvalidOperationException(
            "The isolated test database is only available between InitializeAsync and DisposeAsync.");

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        server = await PostgresTestServer.SharedAsync();
        database = await server.CreateDatabaseAsync(GetType().Name);
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        PostgresTestDatabase? owned = database;
        database = null;
        server = null;

        if (owned is not null)
        {
            await owned.DisposeAsync();
        }
    }
}
