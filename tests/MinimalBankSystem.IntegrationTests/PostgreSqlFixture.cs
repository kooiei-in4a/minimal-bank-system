using Npgsql;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    public const string ImageReference =
        "postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a";

    internal const string AdminDatabaseName = "postgres";

    private const string Username = "postgres";
    private const string Password = "postgres";

    private readonly PostgreSqlContainer container = new PostgreSqlBuilder(ImageReference)
        .WithDatabase(AdminDatabaseName)
        .WithUsername(Username)
        .WithPassword(Password)
        .Build();

    private bool started;

    public PostgreSqlContainer Container => container;

    public async Task InitializeAsync()
    {
        try
        {
            await container.StartAsync();
            started = true;

            await using NpgsqlConnection connection =
                await OpenConnectionAsync(container.GetConnectionString(), "fixture startup");
            await using NpgsqlCommand command = new(
                "SELECT current_database(), current_setting('server_version');",
                connection);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("PostgreSQL startup verification returned no row.");
            }

            string database = reader.GetString(0);
            string serverVersion = reader.GetString(1);
            if (!string.Equals(database, AdminDatabaseName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"PostgreSQL startup verification connected to '{database}' instead of '{AdminDatabaseName}'.");
            }

            if (!serverVersion.StartsWith("18.", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"PostgreSQL startup verification found server version '{serverVersion}', not PostgreSQL 18.");
            }
        }
        catch (Exception exception)
        {
            throw new PostgreSqlFixtureException(
                $"PostgreSQL Testcontainers fixture failed to start or connect using image '{ImageReference}'. " +
                "Docker must be available and the pinned PostgreSQL image must be runnable.",
                exception);
        }
    }

    public async Task<PostgreSqlTestDatabase> CreateDatabaseAsync()
    {
        EnsureStarted();

        string databaseName = $"test_{Guid.NewGuid():N}";

        try
        {
            await using NpgsqlConnection connection =
                await OpenConnectionAsync(container.GetConnectionString(), $"creating database '{databaseName}'");
            await using NpgsqlCommand command = new(
                $"CREATE DATABASE {QuoteIdentifier(databaseName)};",
                connection);
            await command.ExecuteNonQueryAsync();

            NpgsqlConnectionStringBuilder connectionString =
                new(container.GetConnectionString()) { Database = databaseName };
            return new PostgreSqlTestDatabase(this, databaseName, connectionString.ConnectionString);
        }
        catch (PostgreSqlFixtureException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PostgreSqlFixtureException(
                $"Failed to create isolated PostgreSQL test database '{databaseName}'.",
                exception);
        }
    }

    internal static async Task<NpgsqlConnection> OpenConnectionAsync(string connectionString, string operation)
    {
        NpgsqlConnection connection = new(connectionString);

        try
        {
            await connection.OpenAsync();
            return connection;
        }
        catch (Exception exception)
        {
            await connection.DisposeAsync();
            throw new PostgreSqlFixtureException(
                $"PostgreSQL test connection failed during {operation}.",
                exception);
        }
    }

    internal async Task DropDatabaseAsync(string databaseName)
    {
        EnsureStarted();

        if (!databaseName.StartsWith("test_", StringComparison.Ordinal))
        {
            throw new PostgreSqlFixtureException(
                $"Refusing to clean up database '{databaseName}'. Only fixture-owned test databases may be dropped.");
        }

        try
        {
            await using NpgsqlConnection connection =
                await OpenConnectionAsync(container.GetConnectionString(), $"cleaning up database '{databaseName}'");
            await using NpgsqlCommand command = new(
                $"DROP DATABASE {QuoteIdentifier(databaseName)} WITH (FORCE);",
                connection);
            await command.ExecuteNonQueryAsync();
        }
        catch (PostgreSqlFixtureException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PostgreSqlFixtureException(
                $"Failed to clean up isolated PostgreSQL test database '{databaseName}'.",
                exception);
        }
    }

    public async Task DisposeAsync()
    {
        List<Exception> failures = [];

        if (started)
        {
            try
            {
                await container.StopAsync();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        try
        {
            await container.DisposeAsync();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        if (failures.Count > 0)
        {
            throw new PostgreSqlFixtureException(
                "PostgreSQL Testcontainers fixture cleanup failed; container cleanup was not ignored.",
                new AggregateException(failures));
        }
    }

    private void EnsureStarted()
    {
        if (!started)
        {
            throw new PostgreSqlFixtureException(
                "PostgreSQL Testcontainers fixture is not started; database lifecycle cannot continue.");
        }
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}

public sealed class PostgreSqlTestDatabase : IAsyncDisposable
{
    private readonly PostgreSqlFixture fixture;
    private int disposed;

    internal PostgreSqlTestDatabase(PostgreSqlFixture fixture, string databaseName, string connectionString)
    {
        this.fixture = fixture;
        DatabaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string DatabaseName { get; }

    internal string ConnectionString { get; }

    public Task<NpgsqlConnection> OpenConnectionAsync()
    {
        return PostgreSqlFixture.OpenConnectionAsync(ConnectionString, $"opening database '{DatabaseName}'");
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        await fixture.DropDatabaseAsync(DatabaseName);
    }
}

public abstract class PostgreSqlTestBase : IAsyncLifetime
{
    private PostgreSqlTestDatabase? database;

    protected PostgreSqlTestBase(PostgreSqlFixture fixture)
    {
        Fixture = fixture;
    }

    protected PostgreSqlFixture Fixture { get; }

    protected PostgreSqlTestDatabase Database =>
        database ?? throw new InvalidOperationException("The PostgreSQL test database was not initialized.");

    public async Task InitializeAsync()
    {
        database = await Fixture.CreateDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        if (database is not null)
        {
            await database.DisposeAsync();
        }
    }
}

public sealed class PostgreSqlFixtureException : Exception
{
    public PostgreSqlFixtureException(string message)
        : base(message)
    {
    }

    public PostgreSqlFixtureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
