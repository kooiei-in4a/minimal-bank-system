using Npgsql;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Per-test PostgreSQL database lifecycle.
/// Creates a uniquely named database on the shared container and drops it on dispose.
/// Cleanup failures are never swallowed.
/// </summary>
public sealed class PostgreSqlTestDatabase : IAsyncDisposable
{
    private readonly string adminConnectionString;
    private bool cleaned;

    private PostgreSqlTestDatabase(
        string adminConnectionString,
        string databaseName,
        string connectionString)
    {
        this.adminConnectionString = adminConnectionString;
        DatabaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string DatabaseName { get; }

    public string ConnectionString { get; }

    public static async Task<PostgreSqlTestDatabase> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        PostgreSqlContainer shared = await SharedPostgreSqlContainer
            .GetOrStartAsync(cancellationToken)
            .ConfigureAwait(false);

        string adminConnectionString = shared.GetConnectionString();
        string databaseName = "t_" + Guid.NewGuid().ToString("N");

        try
        {
            await ExecuteAdminAsync(
                    adminConnectionString,
                    $"CREATE DATABASE \"{databaseName}\"",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Failed to create PostgreSQL test database '{databaseName}'.",
                exception);
        }

        NpgsqlConnectionStringBuilder builder = new(adminConnectionString)
        {
            Database = databaseName,
        };

        return new PostgreSqlTestDatabase(
            adminConnectionString,
            databaseName,
            builder.ConnectionString);
    }

    /// <summary>
    /// Drops the owned database. When <paramref name="terminateBackends"/> is false,
    /// active sessions are left alone so callers can intentionally exercise cleanup failure.
    /// </summary>
    public async Task CleanupAsync(
        bool terminateBackends = true,
        CancellationToken cancellationToken = default)
    {
        if (cleaned)
        {
            return;
        }

        try
        {
            if (terminateBackends)
            {
                await ExecuteAdminAsync(
                        adminConnectionString,
                        $"""
                         SELECT pg_terminate_backend(pid)
                         FROM pg_stat_activity
                         WHERE datname = '{DatabaseName}'
                           AND pid <> pg_backend_pid();
                         """,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await ExecuteAdminAsync(
                    adminConnectionString,
                    $"DROP DATABASE \"{DatabaseName}\"",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Failed to clean up PostgreSQL test database '{DatabaseName}'.",
                exception);
        }

        cleaned = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (cleaned)
        {
            return;
        }

        await CleanupAsync(terminateBackends: true).ConfigureAwait(false);
    }

    /// <summary>
    /// Marks cleanup as complete without dropping again.
    /// Use only after a successful forced cleanup following a failure demonstration.
    /// </summary>
    public void MarkCleaned()
    {
        cleaned = true;
    }

    private static async Task ExecuteAdminAsync(
        string connectionString,
        string sql,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
