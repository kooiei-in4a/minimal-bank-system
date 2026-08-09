using System.Globalization;
using System.Security.Cryptography;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    public const string ImageReference =
        "postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a";

    public const int ExpectedServerVersionNumber = 180004;

    internal const string DatabaseNamePrefix = "mbs_test_";

    private const string AdministrativeDatabaseName = "fixture_admin";
    private readonly string? dockerEndpoint;
    private readonly IDockerContainerReclaimer containerReclaimer;
    private readonly Exception? startupFaultAfterContainerStart;
    private PostgreSqlContainer? container;
    private string? ownedContainerId;

    public PostgreSqlContainerFixture()
        : this(dockerEndpoint: null, containerReclaimer: null, startupFaultAfterContainerStart: null)
    {
    }

    internal PostgreSqlContainerFixture(string dockerEndpoint)
        : this(dockerEndpoint, containerReclaimer: null, startupFaultAfterContainerStart: null)
    {
    }

    internal PostgreSqlContainerFixture(
        string? dockerEndpoint,
        IDockerContainerReclaimer? containerReclaimer,
        Exception? startupFaultAfterContainerStart = null)
    {
        this.dockerEndpoint = dockerEndpoint;
        this.containerReclaimer = containerReclaimer ?? new CliDockerContainerReclaimer();
        this.startupFaultAfterContainerStart = startupFaultAfterContainerStart;
    }

    public int ServerVersionNumber { get; private set; }

    /// <summary>
    /// Docker container identity retained for independent reclaim after a poisoned
    /// Testcontainers dispose latch. Cleared only after the daemon resource is gone.
    /// </summary>
    internal string? OwnedContainerId => ownedContainerId;

    internal PostgreSqlContainer Container =>
        container ?? throw new InvalidOperationException(
            "The PostgreSQL test container is not running. Fixture initialization must succeed first.");

    public Task InitializeAsync() => InitializeAsync(CancellationToken.None);

    internal async Task InitializeAsync(CancellationToken cancellationToken)
    {
        string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        PostgreSqlBuilder builder = new PostgreSqlBuilder(ImageReference)
            .WithDatabase(AdministrativeDatabaseName)
            .WithUsername("postgres")
            .WithPassword(password);

        if (dockerEndpoint is not null)
        {
            builder = builder.WithDockerEndpoint(dockerEndpoint);
        }

        PostgreSqlContainer candidate = builder.Build();
        container = candidate;

        try
        {
            await candidate.StartAsync(cancellationToken);
            CaptureOwnedContainerId(candidate);
            string connectionString = BuildConnectionString(candidate, AdministrativeDatabaseName);
            ServerVersionNumber = await ReadServerVersionNumberAsync(connectionString, cancellationToken);

            if (ServerVersionNumber != ExpectedServerVersionNumber)
            {
                throw new InvalidOperationException(
                    $"Expected PostgreSQL 18.4 (server_version_num {ExpectedServerVersionNumber}), " +
                    $"but the container reported {ServerVersionNumber}.");
            }

            if (startupFaultAfterContainerStart is not null)
            {
                throw startupFaultAfterContainerStart;
            }
        }
        catch (Exception startupException)
        {
            CaptureOwnedContainerId(candidate);

            Exception? cleanupException = null;

            try
            {
                await ReclaimOwnedContainerAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                cleanupException = exception;
            }

            Exception cause = cleanupException is null
                ? startupException
                : new AggregateException(startupException, cleanupException);

            throw new InvalidOperationException(
                $"Failed to start and connect to the PostgreSQL test container using '{ImageReference}'. " +
                "PostgreSQL integration tests require Docker and never fall back to another provider.",
                cause);
        }
    }

    public async Task DisposeAsync()
    {
        if (ownedContainerId is null && container is null)
        {
            return;
        }

        try
        {
            await ReclaimOwnedContainerAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Failed to dispose the PostgreSQL test container using '{ImageReference}'. " +
                "Docker container identity remains owned for an independent reclaim path; " +
                "retrying DisposeAsync on a poisoned Testcontainers instance is not treated as cleanup success.",
                exception);
        }
    }

    public async Task<PostgreSqlTestDatabase> CreateDatabaseAsync(
        CancellationToken cancellationToken = default)
    {
        string databaseName = $"{DatabaseNamePrefix}{Guid.NewGuid():N}";
        string connectionString = BuildConnectionString(Container, databaseName);

        try
        {
            await ExecuteAdministrativeNonQueryAsync(
                $"CREATE DATABASE {QuoteIdentifier(databaseName)} TEMPLATE template0;",
                cancellationToken);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Failed to create isolated PostgreSQL test database '{databaseName}'.",
                exception);
        }

        return new PostgreSqlTestDatabase(this, databaseName, connectionString);
    }

    internal async Task<bool> DatabaseExistsAsync(
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = await OpenConnectionAsync(
            BuildConnectionString(Container, AdministrativeDatabaseName),
            $"checking database '{databaseName}'",
            cancellationToken);
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
        if (!databaseName.StartsWith(DatabaseNamePrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing to drop database '{databaseName}'. Only fixture-owned databases may be removed.");
        }

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

    internal static async Task<NpgsqlConnection> OpenConnectionAsync(
        string connectionString,
        string operation,
        CancellationToken cancellationToken = default)
    {
        NpgsqlConnection connection = new(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch (Exception connectionException)
        {
            Exception? cleanupException = null;

            try
            {
                await connection.DisposeAsync();
            }
            catch (Exception exception)
            {
                cleanupException = exception;
            }

            Exception cause = cleanupException is null
                ? connectionException
                : new AggregateException(connectionException, cleanupException);

            throw new InvalidOperationException(
                $"PostgreSQL connection failed while {operation}.",
                cause);
        }
    }

    private void CaptureOwnedContainerId(PostgreSqlContainer candidate)
    {
        if (ownedContainerId is not null)
        {
            return;
        }

        try
        {
            ownedContainerId = candidate.Id;
        }
        catch (InvalidOperationException)
        {
            // Container was never created on the daemon.
        }
    }

    private async Task ReclaimOwnedContainerAsync(CancellationToken cancellationToken)
    {
        string? id = ownedContainerId;
        PostgreSqlContainer? candidate = container;

        if (id is not null)
        {
            if (await containerReclaimer.ExistsAsync(id, cancellationToken))
            {
                await containerReclaimer.RemoveForceAsync(id, cancellationToken);

                if (await containerReclaimer.ExistsAsync(id, cancellationToken))
                {
                    throw new InvalidOperationException(
                        $"Docker container '{id}' still exists after forced removal.");
                }
            }

            ownedContainerId = null;
        }

        container = null;
        await DisposeManagedContainerBestEffortAsync(candidate);
    }

    private static async Task DisposeManagedContainerBestEffortAsync(PostgreSqlContainer? candidate)
    {
        if (candidate is null)
        {
            return;
        }

        try
        {
            await candidate.DisposeAsync();
        }
        catch (Exception)
        {
            // Managed dispose is best-effort after authoritative Docker reclaim.
            // A poisoned Testcontainers instance may no-op or throw; neither releases ownership.
        }
    }

    private static async Task<int> ReadServerVersionNumberAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await OpenConnectionAsync(
            connectionString,
            "verifying the container server version",
            cancellationToken);
        await using NpgsqlCommand command = new("SHOW server_version_num;", connection);

        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return int.Parse(
            Convert.ToString(result, CultureInfo.InvariantCulture)
                ?? throw new InvalidOperationException("PostgreSQL did not report server_version_num."),
            CultureInfo.InvariantCulture);
    }

    private async Task ExecuteAdministrativeNonQueryAsync(
        string commandText,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await OpenConnectionAsync(
            BuildConnectionString(Container, AdministrativeDatabaseName),
            "executing a test database lifecycle operation",
            cancellationToken);
        await using NpgsqlCommand command = new(commandText, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string BuildConnectionString(
        PostgreSqlContainer candidate,
        string databaseName)
    {
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
    private readonly PostgreSqlContainerFixture owner;
    private readonly SemaphoreSlim cleanupGate = new(1, 1);
    private bool disposed;

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

    public ValueTask DisposeAsync() => DisposeAsync(CancellationToken.None);

    internal async ValueTask DisposeAsync(CancellationToken cancellationToken)
    {
        await cleanupGate.WaitAsync(CancellationToken.None);

        try
        {
            if (disposed)
            {
                return;
            }

            await owner.DropDatabaseAsync(DatabaseName, cancellationToken);
            disposed = true;
        }
        finally
        {
            cleanupGate.Release();
        }
    }
}
