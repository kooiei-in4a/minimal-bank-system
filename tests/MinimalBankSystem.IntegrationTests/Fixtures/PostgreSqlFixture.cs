using Npgsql;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.Fixtures;

/// <summary>
/// PostgreSQL test fixture that manages container lifecycle and database isolation.
/// Each test instance gets its own isolated database to prevent cross-test contamination.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private readonly string _databaseName;
    private bool _disposed;

    /// <summary>
    /// The PostgreSQL image reference with pinned digest.
    /// </summary>
    public const string PostgresImage =
        "postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a";

    public PostgreSqlFixture()
    {
        _databaseName = $"test_{Guid.NewGuid():N}";
        _container = new PostgreSqlBuilder(PostgresImage)
            .WithDatabase(_databaseName)
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();
    }

    /// <summary>
    /// The connection string to the isolated test database.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>
    /// The host of the PostgreSQL container.
    /// </summary>
    public string Host => _container.Hostname;

    /// <summary>
    /// The port of the PostgreSQL container.
    /// </summary>
    public int Port => _container.GetMappedPublicPort(5432);

    /// <summary>
    /// The name of the isolated test database.
    /// </summary>
    public string DatabaseName => _databaseName;

    public async Task InitializeAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            await _container.StopAsync();
        }
        catch
        {
            // Container cleanup failure should not mask test failures
        }

        await _container.DisposeAsync();
    }

    /// <summary>
    /// Creates a new connection to the test database.
    /// </summary>
    public async Task<NpgsqlConnection> CreateConnectionAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>
    /// Executes a SQL command against the test database.
    /// </summary>
    public async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = await CreateConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Drops all tables in the test database for cleanup.
    /// </summary>
    public async Task DropAllTablesAsync()
    {
        const string sql = @"
            DO $$ DECLARE
                r RECORD;
            BEGIN
                FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public') LOOP
                    EXECUTE 'DROP TABLE IF EXISTS ' || quote_ident(r.tablename) || ' CASCADE';
                END LOOP;
            END $$;";

        await ExecuteSqlAsync(sql);
    }
}
