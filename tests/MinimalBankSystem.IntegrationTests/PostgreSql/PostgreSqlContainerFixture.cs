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
    private readonly Func<CancellationToken, Task>? postStartFaultInjector;
    private PostgreSqlContainer? container;
    private string? containerId;
    private bool containerDisposeAttempted;

    public PostgreSqlContainerFixture()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlContainerFixture" /> class for
    /// lifecycle tests.
    /// </summary>
    /// <param name="dockerEndpoint">The Docker endpoint the container is created through.</param>
    /// <param name="postStartFaultInjector">
    /// Test-only hook invoked once the container is running and before the server version is
    /// verified. It exists so a startup failure can be raised while a real container exists.
    /// </param>
    internal PostgreSqlContainerFixture(
        string dockerEndpoint,
        Func<CancellationToken, Task>? postStartFaultInjector = null)
    {
        this.dockerEndpoint = dockerEndpoint;
        this.postStartFaultInjector = postStartFaultInjector;
    }

    public int ServerVersionNumber { get; private set; }

    /// <summary>
    /// Gets the Docker container id this fixture is still responsible for removing, or
    /// <see langword="null" /> once the Docker daemon has confirmed the container is gone.
    /// </summary>
    /// <remarks>
    /// This id, not the Testcontainers instance, is the durable cleanup ownership token. A
    /// container instance whose <c>DisposeAsync</c> failed cannot remove its container again, so
    /// the id is retained until an independent removal succeeds.
    /// </remarks>
    internal string? UnreclaimedContainerId => containerId;

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

            // Take the Docker resource identity as soon as it exists. It outlives the container
            // instance and is the only thing that can still remove a leftover container.
            containerId = candidate.Id;

            if (postStartFaultInjector is not null)
            {
                await postStartFaultInjector(cancellationToken);
            }

            string connectionString = BuildConnectionString(candidate, AdministrativeDatabaseName);
            ServerVersionNumber = await ReadServerVersionNumberAsync(connectionString, cancellationToken);

            if (ServerVersionNumber != ExpectedServerVersionNumber)
            {
                throw new InvalidOperationException(
                    $"Expected PostgreSQL 18.4 (server_version_num {ExpectedServerVersionNumber}), " +
                    $"but the container reported {ServerVersionNumber}.");
            }
        }
        catch (Exception startupException)
        {
            // A container may have been created before startup failed. Recover its id so the
            // partial resource keeps an owner even when the cleanup below also fails.
            containerId ??= TryReadContainerId(candidate);

            Exception? cleanupException = null;

            try
            {
                await ReclaimContainerAsync(cancellationToken);
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

    public Task DisposeAsync() => DisposeAsync(CancellationToken.None);

    internal async Task DisposeAsync(CancellationToken cancellationToken)
    {
        if (container is null && containerId is null)
        {
            return;
        }

        try
        {
            await ReclaimContainerAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Failed to remove the PostgreSQL test container using '{ImageReference}'. " +
                $"Container id '{containerId ?? "unknown"}' stays owned by this fixture so cleanup " +
                "can be retried without reusing the already disposed container instance.",
                exception);
        }
    }

    /// <summary>
    /// Removes the container and only releases ownership once the resource is really gone.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the container has been reclaimed.</returns>
    /// <remarks>
    /// Testcontainers evaluates its disposed guard before it deletes the container, and that
    /// evaluation is a test-and-set. The first <c>DisposeAsync</c> therefore latches the instance
    /// as disposed even when the Docker removal that follows fails, and every later
    /// <c>DisposeAsync</c> on that instance returns without contacting Docker at all. Treating that
    /// silence as success would drop the last owner of a container that is still running, so the
    /// instance is used for exactly one removal attempt and the Docker container id takes over
    /// afterwards.
    /// </remarks>
    private async Task ReclaimContainerAsync(CancellationToken cancellationToken)
    {
        PostgreSqlContainer? candidate = container;

        if (candidate is not null && !containerDisposeAttempted)
        {
            containerDisposeAttempted = true;

            // The guard inside Testcontainers has not been tripped yet, so this call really does
            // reach Docker and really does report a removal failure.
            await candidate.DisposeAsync();

            container = null;
            containerId = null;
            return;
        }

        string? reclaimableContainerId = containerId;

        if (reclaimableContainerId is null)
        {
            // No container was ever created, so there is no resource left to own.
            container = null;
            return;
        }

        await DockerEngineEndpoint.RemoveContainerAsync(
            DockerEngineEndpoint.Resolve(dockerEndpoint),
            reclaimableContainerId,
            cancellationToken);

        container = null;
        containerId = null;
    }

    private static string? TryReadContainerId(PostgreSqlContainer candidate)
    {
        try
        {
            return candidate.Id;
        }
        catch (InvalidOperationException)
        {
            // Testcontainers throws when no container was created, which means nothing to reclaim.
            return null;
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
