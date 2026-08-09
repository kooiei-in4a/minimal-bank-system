using System.Globalization;
using System.Security.Cryptography;
using Docker.DotNet;
using Docker.DotNet.Models;
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
    private readonly Func<PostgreSqlContainer, CancellationToken, Task>? startupValidation;
    private readonly Func<PostgreSqlContainer, ValueTask>? disposeContainer;
    private readonly Func<string, string?, IContainerCleanup> cleanupFactory;
    private PostgreSqlContainer? container;
    private IContainerCleanup? containerCleanup;
    private string? containerResourceId;

    public PostgreSqlContainerFixture()
    {
        cleanupFactory = static (resourceId, endpoint) =>
            new DockerContainerCleanup(resourceId, endpoint);
    }

    internal PostgreSqlContainerFixture(
        string? dockerEndpoint,
        Func<PostgreSqlContainer, CancellationToken, Task>? startupValidation = null,
        Func<PostgreSqlContainer, ValueTask>? disposeContainer = null,
        Func<string, string?, IContainerCleanup>? cleanupFactory = null)
    {
        this.dockerEndpoint = dockerEndpoint;
        this.startupValidation = startupValidation;
        this.disposeContainer = disposeContainer;
        this.cleanupFactory = cleanupFactory ?? CreateDefaultCleanup;
    }

    public int ServerVersionNumber { get; private set; }

    internal PostgreSqlContainer Container =>
        container ?? throw new InvalidOperationException(
            "The PostgreSQL test container is not running. Fixture initialization must succeed first.");

    internal string? PendingContainerResourceId => containerResourceId;

    internal bool HasPendingContainerCleanup => containerCleanup is not null;

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
            CaptureContainerResourceId(candidate);

            if (startupValidation is not null)
            {
                await startupValidation(candidate, cancellationToken);
            }
            else
            {
                string connectionString = BuildConnectionString(candidate, AdministrativeDatabaseName);
                ServerVersionNumber = await ReadServerVersionNumberAsync(connectionString, cancellationToken);
            }

            if (ServerVersionNumber != ExpectedServerVersionNumber)
            {
                throw new InvalidOperationException(
                    $"Expected PostgreSQL 18.4 (server_version_num {ExpectedServerVersionNumber}), " +
                    $"but the container reported {ServerVersionNumber}.");
            }
        }
        catch (Exception startupException)
        {
            Exception? cleanupException = await DisposeCandidateAndFallbackAsync(candidate);
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
        if (container is null && (containerCleanup is not null || containerResourceId is not null))
        {
            await DisposeIndependentContainerCleanupAsync();
            return;
        }

        PostgreSqlContainer? candidate = container;

        if (candidate is null)
        {
            return;
        }

        try
        {
            await DisposeTestcontainersContainerAsync(candidate);
            ClearContainerOwnership();
        }
        catch (Exception exception)
        {
            container = null;
            Exception? fallbackException = await TryDisposeIndependentContainerCleanupAsync();
            Exception cause = fallbackException is null
                ? exception
                : new AggregateException(exception, fallbackException);

            throw new InvalidOperationException(
                $"Failed to dispose the PostgreSQL test container using '{ImageReference}'. " +
                "The Testcontainers instance is not retried; cleanup ownership is retained by the independent Docker resource owner.",
                cause);
        }
    }

    private async Task<Exception?> DisposeCandidateAndFallbackAsync(PostgreSqlContainer candidate)
    {
        CaptureContainerResourceId(candidate);

        Exception? candidateException = null;

        try
        {
            await DisposeTestcontainersContainerAsync(candidate);
        }
        catch (Exception exception)
        {
            candidateException = exception;
        }

        if (candidateException is null)
        {
            ClearContainerOwnership();
            return null;
        }

        container = null;
        Exception? fallbackException = await TryDisposeIndependentContainerCleanupAsync();
        return fallbackException is null
            ? candidateException
            : new AggregateException(candidateException, fallbackException);
    }

    private async Task<Exception?> TryDisposeIndependentContainerCleanupAsync()
    {
        if (containerResourceId is null)
        {
            return null;
        }

        try
        {
            containerCleanup ??= cleanupFactory(containerResourceId, dockerEndpoint);
            await containerCleanup.RemoveAsync(CancellationToken.None);
            await containerCleanup.DisposeAsync();
            ClearContainerOwnership();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private async Task DisposeIndependentContainerCleanupAsync()
    {
        Exception? cleanupException = await TryDisposeIndependentContainerCleanupAsync();

        if (cleanupException is not null)
        {
            throw new InvalidOperationException(
                $"Failed to remove the PostgreSQL test container resource '{containerResourceId}'. " +
                "The independent cleanup owner is retained for a later retry.",
                cleanupException);
        }
    }

    private async ValueTask DisposeTestcontainersContainerAsync(PostgreSqlContainer candidate)
    {
        if (disposeContainer is not null)
        {
            await disposeContainer(candidate);
            return;
        }

        await candidate.DisposeAsync();
    }

    private void CaptureContainerResourceId(PostgreSqlContainer candidate)
    {
        if (containerResourceId is not null)
        {
            return;
        }

        try
        {
            containerResourceId = candidate.Id;
        }
        catch (InvalidOperationException)
        {
            // StartAsync may fail before Docker creates a container.
        }
    }

    private void ClearContainerOwnership()
    {
        container = null;
        containerCleanup = null;
        containerResourceId = null;
    }

    private static DockerContainerCleanup CreateDefaultCleanup(string resourceId, string? endpoint) =>
        new DockerContainerCleanup(resourceId, endpoint);

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

internal interface IContainerCleanup : IAsyncDisposable
{
    string ResourceId { get; }

    Task RemoveAsync(CancellationToken cancellationToken);
}

internal sealed class DockerContainerCleanup : IContainerCleanup
{
    private readonly DockerClient client;
    private bool removed;

    internal DockerContainerCleanup(string resourceId, string? dockerEndpoint)
    {
        ResourceId = resourceId;

        DockerClientBuilder builder = new();
        if (dockerEndpoint is not null)
        {
            builder = builder.WithEndpoint(new Uri(dockerEndpoint));
        }

        client = builder.Build();
    }

    public string ResourceId { get; }

    public async Task RemoveAsync(CancellationToken cancellationToken)
    {
        if (removed)
        {
            return;
        }

        try
        {
            await client.Containers.RemoveContainerAsync(
                ResourceId,
                new ContainerRemoveParameters { Force = true },
                cancellationToken);
        }
        catch (DockerContainerNotFoundException)
        {
            // An earlier remove may have succeeded while its response was lost.
        }

        removed = true;
    }

    internal static async Task<bool> ExistsAsync(
        string resourceId,
        string? dockerEndpoint,
        CancellationToken cancellationToken = default)
    {
        DockerClientBuilder builder = new();
        if (dockerEndpoint is not null)
        {
            builder = builder.WithEndpoint(new Uri(dockerEndpoint));
        }

        using DockerClient inspectClient = builder.Build();

        try
        {
            await inspectClient.Containers.InspectContainerAsync(resourceId, cancellationToken);
            return true;
        }
        catch (DockerContainerNotFoundException)
        {
            return false;
        }
    }

    public ValueTask DisposeAsync()
    {
        client.Dispose();
        return ValueTask.CompletedTask;
    }
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
