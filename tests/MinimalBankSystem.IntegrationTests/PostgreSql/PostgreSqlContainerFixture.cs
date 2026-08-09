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
    private readonly Func<IPostgreSqlTestContainer>? containerFactory;
    private readonly IContainerResourceCleanup containerResourceCleanup;
    private IPostgreSqlTestContainer? container;
    private ContainerCleanupOwner? containerCleanupOwner;

    public PostgreSqlContainerFixture()
        : this(null, new DockerCliContainerResourceCleanup(null), null)
    {
    }

    internal PostgreSqlContainerFixture(string dockerEndpoint)
        : this(dockerEndpoint, new DockerCliContainerResourceCleanup(dockerEndpoint), null)
    {
    }

    internal PostgreSqlContainerFixture(
        Func<IPostgreSqlTestContainer> containerFactory,
        IContainerResourceCleanup containerResourceCleanup)
        : this(null, containerResourceCleanup, containerFactory)
    {
    }

    private PostgreSqlContainerFixture(
        string? dockerEndpoint,
        IContainerResourceCleanup containerResourceCleanup,
        Func<IPostgreSqlTestContainer>? containerFactory)
    {
        this.dockerEndpoint = dockerEndpoint;
        this.containerResourceCleanup = containerResourceCleanup;
        this.containerFactory = containerFactory;
    }

    public int ServerVersionNumber { get; private set; }

    internal IPostgreSqlTestContainer Container =>
        container ?? throw new InvalidOperationException(
            "The PostgreSQL test container is not running. Fixture initialization must succeed first.");

    public Task InitializeAsync() => InitializeAsync(CancellationToken.None);

    internal async Task InitializeAsync(CancellationToken cancellationToken)
    {
        IPostgreSqlTestContainer candidate = CreateContainer();
        container = candidate;
        ContainerCleanupOwner cleanupOwner = new(candidate.DisposeAsync, containerResourceCleanup);
        containerCleanupOwner = cleanupOwner;

        try
        {
            await candidate.StartAsync(cancellationToken);
            CaptureContainerIdentity(cleanupOwner, candidate);
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
                CaptureContainerIdentity(cleanupOwner, candidate);
                await cleanupOwner.DisposeAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                cleanupException = exception;
            }

            if (cleanupOwner.IsReleased)
            {
                ClearContainerOwnership(cleanupOwner);
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
        ContainerCleanupOwner? cleanupOwner = containerCleanupOwner;

        if (cleanupOwner is null)
        {
            return;
        }

        try
        {
            await cleanupOwner.DisposeAsync();
        }
        catch (Exception exception)
        {
            if (cleanupOwner.IsReleased)
            {
                ClearContainerOwnership(cleanupOwner);
            }

            throw new InvalidOperationException(
                $"Failed to dispose the PostgreSQL test container using '{ImageReference}'. " +
                "The original cleanup failure remains visible; any retry uses the independent Docker cleanup path.",
                exception);
        }

        ClearContainerOwnership(cleanupOwner);
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
        IPostgreSqlTestContainer candidate,
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

    internal bool HasOutstandingContainerCleanup =>
        containerCleanupOwner?.HasOutstandingResource == true;

    private IPostgreSqlTestContainer CreateContainer()
    {
        if (containerFactory is not null)
        {
            return containerFactory();
        }

        string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        PostgreSqlBuilder builder = new PostgreSqlBuilder(ImageReference)
            .WithDatabase(AdministrativeDatabaseName)
            .WithUsername("postgres")
            .WithPassword(password);

        if (dockerEndpoint is not null)
        {
            builder = builder.WithDockerEndpoint(dockerEndpoint);
        }

        return new TestcontainersPostgreSqlTestContainer(builder.Build());
    }

    private static void CaptureContainerIdentity(
        ContainerCleanupOwner cleanupOwner,
        IPostgreSqlTestContainer candidate)
    {
        try
        {
            cleanupOwner.CaptureContainer(candidate.Id);
        }
        catch (InvalidOperationException)
        {
            // Docker did not create a container, so no independent cleanup identity exists.
        }
    }

    private void ClearContainerOwnership(ContainerCleanupOwner cleanupOwner)
    {
        if (ReferenceEquals(containerCleanupOwner, cleanupOwner))
        {
            container = null;
            containerCleanupOwner = null;
            cleanupOwner.Dispose();
        }
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
