using System.Globalization;
using System.Security.Cryptography;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    public const string ImageReference =
        "postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a";

    private const string AdministrativeDatabaseName = "fixture_admin";
    private PostgreSqlContainer? container;

    public int ServerVersionNumber { get; private set; }

    public async Task InitializeAsync()
    {
        string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        PostgreSqlContainer candidate = new PostgreSqlBuilder(ImageReference)
            .WithDatabase(AdministrativeDatabaseName)
            .WithUsername("postgres")
            .WithPassword(password)
            .Build();

        container = candidate;

        try
        {
            await candidate.StartAsync();
            ServerVersionNumber = await ReadServerVersionNumberAsync(candidate.GetConnectionString());

            if (ServerVersionNumber != 180004)
            {
                throw new InvalidOperationException(
                    $"Expected PostgreSQL 18.4 (server_version_num 180004), but the container reported {ServerVersionNumber}.");
            }
        }
        catch (Exception startException)
        {
            Exception? cleanupException = null;

            try
            {
                await candidate.DisposeAsync();
            }
            catch (Exception exception)
            {
                cleanupException = exception;
            }

            container = null;
            Exception cause = cleanupException is null
                ? startException
                : new AggregateException(startException, cleanupException);

            throw new InvalidOperationException(
                $"Failed to start and connect to the PostgreSQL test container using '{ImageReference}'.",
                cause);
        }
    }

    public async Task DisposeAsync()
    {
        PostgreSqlContainer? candidate = Interlocked.Exchange(ref container, null);

        if (candidate is null)
        {
            return;
        }

        try
        {
            await candidate.DisposeAsync();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Failed to dispose the PostgreSQL test container using '{ImageReference}'.",
                exception);
        }
    }

    public async Task<PostgreSqlTestDatabase> CreateDatabaseAsync(
        CancellationToken cancellationToken = default)
    {
        string databaseName = $"test_{Guid.NewGuid():N}";

        try
        {
            await ExecuteAdministrativeNonQueryAsync(
                $"CREATE DATABASE {QuoteIdentifier(databaseName)};",
                cancellationToken);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Failed to create isolated PostgreSQL test database '{databaseName}'.",
                exception);
        }

        return new PostgreSqlTestDatabase(
            this,
            databaseName,
            BuildConnectionString(databaseName));
    }

    internal async Task<bool> DatabaseExistsAsync(
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = new(BuildConnectionString(AdministrativeDatabaseName));
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand command = new(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = $1);",
            connection);
        command.Parameters.AddWithValue(databaseName);

        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    internal async Task DropDatabaseAsync(
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ExecuteAdministrativeNonQueryAsync(
                $"DROP DATABASE {QuoteIdentifier(databaseName)} WITH (FORCE);",
                cancellationToken);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Failed to drop isolated PostgreSQL test database '{databaseName}'.",
                exception);
        }
    }

    private static async Task<int> ReadServerVersionNumberAsync(string connectionString)
    {
        NpgsqlConnectionStringBuilder builder = new(connectionString)
        {
            Pooling = false,
            Timeout = 10,
            CommandTimeout = 10,
        };

        await using NpgsqlConnection connection = new(builder.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT current_setting('server_version_num');",
            connection);

        object? result = await command.ExecuteScalarAsync();
        return int.Parse(
            Convert.ToString(result, CultureInfo.InvariantCulture)
                ?? throw new InvalidOperationException("PostgreSQL did not report server_version_num."),
            CultureInfo.InvariantCulture);
    }

    private async Task ExecuteAdministrativeNonQueryAsync(
        string commandText,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = new(BuildConnectionString(AdministrativeDatabaseName));
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand command = new(commandText, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private string BuildConnectionString(string databaseName)
    {
        PostgreSqlContainer candidate = container
            ?? throw new InvalidOperationException(
                "The PostgreSQL test container is not running. Fixture initialization must complete before a database is requested.");

        NpgsqlConnectionStringBuilder builder = new(candidate.GetConnectionString())
        {
            Database = databaseName,
            Pooling = false,
            Timeout = 10,
            CommandTimeout = 10,
        };

        return builder.ConnectionString;
    }

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}

public sealed class PostgreSqlTestDatabase : IAsyncDisposable
{
    private PostgreSqlContainerFixture? owner;

    internal PostgreSqlTestDatabase(
        PostgreSqlContainerFixture owner,
        string databaseName,
        string connectionString)
    {
        this.owner = owner;
        DatabaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string DatabaseName { get; }

    public string ConnectionString { get; }

    public async ValueTask DisposeAsync()
    {
        PostgreSqlContainerFixture? candidate = Interlocked.Exchange(ref owner, null);

        if (candidate is not null)
        {
            await candidate.DropDatabaseAsync(DatabaseName);
        }
    }
}
