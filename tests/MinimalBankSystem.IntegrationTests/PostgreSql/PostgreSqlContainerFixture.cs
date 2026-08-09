using System.Globalization;
using System.Security.Cryptography;
using Docker.DotNet;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Configurations;
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
    private PostgreSqlContainer? container;
    private string? containerId;
    private bool instanceDisposalAttempted;
    private bool containerRemovalConfirmed;

    public PostgreSqlContainerFixture()
        : this(TestcontainersSettings.OS.DockerEndpointAuthConfig?.Endpoint.ToString())
    {
    }

    internal PostgreSqlContainerFixture(string? dockerEndpoint)
    {
        this.dockerEndpoint = dockerEndpoint;
    }

    internal PostgreSqlContainerFixture(PostgreSqlContainer container, string dockerEndpoint)
    {
        this.container = container;
        this.dockerEndpoint = dockerEndpoint;
        containerId = TryGetContainerId(container);
    }

    public int ServerVersionNumber { get; private set; }

    internal PostgreSqlContainer Container =>
        container ?? throw new InvalidOperationException(
            "The PostgreSQL test container is not running. Fixture initialization must succeed first.");

    /// <summary>
    /// Gets the Docker container identity whose removal has not yet been confirmed by the daemon,
    /// or null when no removal is outstanding. This is the deterministic cleanup owner that is
    /// kept while a real Docker container may still exist.
    /// </summary>
    internal string? PendingContainerId => containerRemovalConfirmed ? null : containerId;

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
            containerId = TryGetContainerId(candidate);
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
            Exception? cleanupException = null;

            try
            {
                await candidate.DisposeAsync();
                container = null;
                containerRemovalConfirmed = true;
            }
            catch (Exception exception)
            {
                // Testcontainers latches its internal disposed state before the Docker removal
                // call, so this instance must never be disposed again.
                cleanupException = exception;
                instanceDisposalAttempted = true;
                containerId ??= TryGetContainerId(candidate);
            }

            Exception cause = cleanupException is null
                ? startupException
                : new AggregateException(startupException, cleanupException);

            throw new InvalidOperationException(
                $"Failed to start and connect to the PostgreSQL test container using '{ImageReference}'. " +
                "PostgreSQL integration tests require Docker and never fall back to another provider." +
                (cleanupException is null
                    ? string.Empty
                    : " Partial cleanup also failed; the fixture keeps the Docker container identity and the " +
                      "next DisposeAsync call removes the container through the direct Docker API cleanup path."),
                cause);
        }
    }

    public async Task DisposeAsync()
    {
        PostgreSqlContainer? candidate = container;

        if (candidate is null)
        {
            return;
        }

        if (!instanceDisposalAttempted)
        {
            try
            {
                await candidate.DisposeAsync();
                container = null;
                containerRemovalConfirmed = true;
                return;
            }
            catch (Exception exception)
            {
                // Testcontainers latches its internal disposed state before the Docker removal
                // call. Retrying DisposeAsync on this same instance would silently no-op while
                // the Docker container may still exist, so the instance is never used again.
                instanceDisposalAttempted = true;
                throw new InvalidOperationException(
                    $"Failed to dispose the PostgreSQL test container using '{ImageReference}'. " +
                    "The Testcontainers instance latched its internal disposed state before the Docker " +
                    "removal call, so disposing it again would silently no-op while the Docker container " +
                    "may still exist. The fixture keeps the container identity and the next DisposeAsync " +
                    "call removes the container through the direct Docker API cleanup path.",
                    exception);
            }
        }

        await RemoveContainerThroughDockerApiAsync();
    }

    private async Task RemoveContainerThroughDockerApiAsync()
    {
        if (containerRemovalConfirmed)
        {
            return;
        }

        string? resourceId = containerId;

        if (resourceId is null)
        {
            throw new InvalidOperationException(
                $"Cannot finalize the cleanup of the PostgreSQL test container using '{ImageReference}': " +
                "no Docker container identity was captured. The fixture keeps ownership of the container " +
                "instance, but the removal cannot be retried without a resource identity.");
        }

        try
        {
            using DockerClient client = dockerEndpoint is null
                ? new DockerClientBuilder().Build()
                : new DockerClientBuilder().WithEndpoint(new Uri(dockerEndpoint)).Build();

            try
            {
                await client.Containers.RemoveContainerAsync(
                    resourceId,
                    new ContainerRemoveParameters { Force = true, RemoveVolumes = true });
            }
            catch (DockerContainerNotFoundException)
            {
                // The daemon confirms that the container is absent, so the resource is gone.
            }

            container = null;
            containerRemovalConfirmed = true;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Failed to remove the PostgreSQL test container '{resourceId}' using '{ImageReference}' " +
                "through the direct Docker API cleanup path. The fixture keeps the container identity; " +
                "call DisposeAsync again to retry the removal.",
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

    private static string? TryGetContainerId(PostgreSqlContainer candidate)
    {
        try
        {
            string id = candidate.Id;
            return string.IsNullOrEmpty(id) ? null : id;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
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
