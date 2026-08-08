using Npgsql;

namespace MinimalBankSystem.PostgresIntegrationTests.Fixtures;

/// <summary>
/// An isolated PostgreSQL database owned by exactly one test.
/// </summary>
/// <remarks>
/// <para>
/// Ownership: one database per test. The owner creates it through
/// <see cref="PostgresTestServer.CreateDatabaseAsync"/> and is responsible for disposing it, which
/// drops the database. No database outlives the test that created it, so no test can observe
/// another test's data, locks, sequences or schema.
/// </para>
/// <para>
/// Cleanup never fails silently: a failed drop throws <see cref="PostgresTestCleanupException"/>,
/// which xUnit reports as a failure of the owning test.
/// </para>
/// </remarks>
public sealed class PostgresTestDatabase : IAsyncDisposable
{
    private readonly PostgresTestServer server;
    private readonly NpgsqlDataSource dataSource;
    private int disposed;

    internal PostgresTestDatabase(
        PostgresTestServer server,
        string name,
        NpgsqlDataSource dataSource)
    {
        this.server = server;
        this.dataSource = dataSource;
        Name = name;
    }

    /// <summary>
    /// The generated database name. It only contains lowercase ASCII letters, digits and
    /// underscores, so it is safe to embed in a quoted identifier.
    /// </summary>
    public string Name { get; }

    /// <summary>The connection string for this database only.</summary>
    public string ConnectionString => dataSource.ConnectionString;

    /// <summary>Opens a connection to this database.</summary>
    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

        return await dataSource.OpenConnectionAsync(cancellationToken);
    }

    /// <summary>Executes a statement against this database.</summary>
    public async Task ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        await using NpgsqlConnection connection = await OpenConnectionAsync(cancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Reads a single value from this database.</summary>
    public async Task<T> ExecuteScalarAsync<T>(string sql, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        await using NpgsqlConnection connection = await OpenConnectionAsync(cancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object? value = await command.ExecuteScalarAsync(cancellationToken);

        return (T)value!;
    }

    /// <summary>Closes every connection to this database and drops it.</summary>
    /// <exception cref="PostgresTestCleanupException">The database could not be dropped.</exception>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await dataSource.DisposeAsync();
        }
        catch (Exception exception)
        {
            throw new PostgresTestCleanupException(
                Name,
                $"Could not close the connections owned by the test database '{Name}'.",
                exception);
        }

        try
        {
            // No IF EXISTS: a database that already disappeared is a cleanup defect, not a no-op.
            // WITH (FORCE) terminates backends the test left behind instead of hanging.
            await server.ExecuteClusterStatementAsync($"DROP DATABASE \"{Name}\" WITH (FORCE)");
        }
        catch (Exception exception)
        {
            throw new PostgresTestCleanupException(
                Name,
                $"Could not drop the test database '{Name}'. The PostgreSQL test server may still " +
                "hold state that would leak into other tests.",
                exception);
        }
    }
}
