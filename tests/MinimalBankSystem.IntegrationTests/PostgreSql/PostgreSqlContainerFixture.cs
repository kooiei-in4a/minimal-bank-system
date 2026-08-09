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
    private readonly ITestContainerHandle? injectedContainer;
    private readonly IContainerResourceCleanup containerResourceCleanup;
    private PostgreSqlContainer? container;
    private ContainerResourceOwner? containerOwner;

    public PostgreSqlContainerFixture()
    {
        containerResourceCleanup = new DockerContainerResourceCleanup(null);
    }

    internal PostgreSqlContainerFixture(string dockerEndpoint)
    {
        this.dockerEndpoint = dockerEndpoint;
        containerResourceCleanup = new DockerContainerResourceCleanup(dockerEndpoint);
    }

    internal PostgreSqlContainerFixture(
        ITestContainerHandle injectedContainer,
        IContainerResourceCleanup containerResourceCleanup)
    {
        this.injectedContainer = injectedContainer;
        this.containerResourceCleanup = containerResourceCleanup;
    }

    public int ServerVersionNumber { get; private set; }

    internal PostgreSqlContainer Container =>
        container ?? throw new InvalidOperationException(
            "The PostgreSQL test container is not running. Fixture initialization must succeed first.");

    public Task InitializeAsync() => InitializeAsync(CancellationToken.None);

    internal async Task InitializeAsync(CancellationToken cancellationToken)
    {
        ITestContainerHandle candidateHandle;

        if (injectedContainer is not null)
        {
            candidateHandle = injectedContainer;
        }
        else
        {
            string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            string containerName = $"mbs_test_container_{Guid.NewGuid():N}";
            PostgreSqlBuilder builder = new PostgreSqlBuilder(ImageReference)
                .WithDatabase(AdministrativeDatabaseName)
                .WithUsername("postgres")
                .WithPassword(password)
                .WithName(containerName);

            if (dockerEndpoint is not null)
            {
                builder = builder.WithDockerEndpoint(dockerEndpoint);
            }

            PostgreSqlContainer candidate = builder.Build();
            container = candidate;
            candidateHandle = new PostgreSqlContainerHandle(candidate, containerName);
        }

        ContainerResourceOwner owner = new(candidateHandle);
        containerOwner = owner;

        try
        {
            await candidateHandle.StartAsync(cancellationToken);
            owner.CaptureResourceId();
            string connectionString = BuildConnectionString(owner.Handle, AdministrativeDatabaseName);
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
            owner.CaptureResourceId();
            Exception? cleanupException = await owner.CleanupAsync(
                containerResourceCleanup,
                cancellationToken);

            if (owner.IsFinalized)
            {
                containerOwner = null;
                container = null;
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
        ContainerResourceOwner? owner = containerOwner;

        if (owner is null)
        {
            return;
        }

        owner.CaptureResourceId();
        Exception? cleanupException = await owner.CleanupAsync(
            containerResourceCleanup,
            CancellationToken.None);

        if (owner.IsFinalized)
        {
            containerOwner = null;
            container = null;
        }

        if (cleanupException is not null)
        {
            throw new InvalidOperationException(
                $"Failed to clean up the PostgreSQL test container using '{ImageReference}'. " +
                (owner.IsFinalized
                    ? "The independent Docker cleanup completed after the Testcontainers failure."
                    : "The independent cleanup owner was retained for a later retry."),
                cleanupException);
        }
    }

    public async Task<PostgreSqlTestDatabase> CreateDatabaseAsync(
        CancellationToken cancellationToken = default)
    {
        string databaseName = $"{DatabaseNamePrefix}{Guid.NewGuid():N}";
        string connectionString = BuildConnectionString(
            containerOwner?.Handle
                ?? throw new InvalidOperationException("The PostgreSQL test container is not owned by this fixture."),
            databaseName);

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
            BuildConnectionString(
                containerOwner?.Handle
                    ?? throw new InvalidOperationException("The PostgreSQL test container is not owned by this fixture."),
                AdministrativeDatabaseName),
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
            BuildConnectionString(
                containerOwner?.Handle
                    ?? throw new InvalidOperationException("The PostgreSQL test container is not owned by this fixture."),
                AdministrativeDatabaseName),
            "executing a test database lifecycle operation",
            cancellationToken);
        await using NpgsqlCommand command = new(commandText, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string BuildConnectionString(
        ITestContainerHandle candidate,
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

internal interface ITestContainerHandle
{
    string ResourceIdentity { get; }

    string? ResourceId { get; }

    Task StartAsync(CancellationToken cancellationToken);

    string GetConnectionString();

    ValueTask DisposeAsync();
}

internal sealed class PostgreSqlContainerHandle(
    PostgreSqlContainer container,
    string resourceIdentity) : ITestContainerHandle
{
    public string ResourceIdentity { get; } = resourceIdentity;

    public string? ResourceId
    {
        get
        {
            try
            {
                return container.Id;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) =>
        container.StartAsync(cancellationToken);

    public string GetConnectionString() => container.GetConnectionString();

    public ValueTask DisposeAsync() => container.DisposeAsync();
}

internal sealed class ContainerResourceOwner(ITestContainerHandle handle)
{
    private string? resourceId;
    private bool testcontainersDisposeAttempted;

    public ITestContainerHandle Handle { get; } = handle;

    public bool IsFinalized { get; private set; }

    internal string ResourceReference => resourceId ?? Handle.ResourceIdentity;

    internal void CaptureResourceId()
    {
        resourceId ??= Handle.ResourceId;
    }

    internal async Task<Exception?> CleanupAsync(
        IContainerResourceCleanup independentCleanup,
        CancellationToken cancellationToken)
    {
        if (IsFinalized)
        {
            return null;
        }

        Exception? testcontainersException = null;

        if (!testcontainersDisposeAttempted)
        {
            testcontainersDisposeAttempted = true;

            try
            {
                await Handle.DisposeAsync();
                IsFinalized = true;
                return null;
            }
            catch (Exception exception)
            {
                testcontainersException = exception;
            }
        }

        try
        {
            await independentCleanup.RemoveAndVerifyAsync(ResourceReference, cancellationToken);
            IsFinalized = true;

            return testcontainersException is null
                ? null
                : new InvalidOperationException(
                    "Testcontainers cleanup failed, but independent Docker cleanup verified the resource is absent.",
                    testcontainersException);
        }
        catch (Exception independentException)
        {
            return testcontainersException is null
                ? independentException
                : new AggregateException(testcontainersException, independentException);
        }
    }
}

internal interface IContainerResourceCleanup
{
    Task RemoveAndVerifyAsync(string resourceReference, CancellationToken cancellationToken);
}

internal sealed class DockerContainerResourceCleanup(string? dockerEndpoint) : IContainerResourceCleanup
{
    public async Task RemoveAndVerifyAsync(
        string resourceReference,
        CancellationToken cancellationToken)
    {
        using DockerClient client = CreateClient();
        Exception? removeException = null;

        try
        {
            await client.Containers.RemoveContainerAsync(
                resourceReference,
                new ContainerRemoveParameters { Force = true },
                cancellationToken);
        }
        catch (DockerContainerNotFoundException)
        {
        }
        catch (Exception exception)
        {
            removeException = exception;
        }

        bool exists;

        try
        {
            await client.Containers.InspectContainerAsync(resourceReference, cancellationToken);
            exists = true;
        }
        catch (DockerContainerNotFoundException)
        {
            exists = false;
        }

        if (exists)
        {
            InvalidOperationException verificationException = new(
                $"Independent Docker cleanup did not remove container '{resourceReference}'.");

            throw removeException is null
                ? verificationException
                : new AggregateException(removeException, verificationException);
        }

        if (removeException is not null)
        {
            throw removeException;
        }
    }

    private DockerClient CreateClient()
    {
        DockerClientBuilder builder = new();

        if (dockerEndpoint is not null)
        {
            builder = builder.WithEndpoint(new Uri(dockerEndpoint, UriKind.Absolute));
        }

        return builder.Build();
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
