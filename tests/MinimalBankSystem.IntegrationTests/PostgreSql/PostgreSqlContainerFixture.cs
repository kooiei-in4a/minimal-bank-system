using System.Globalization;
using System.Security.Cryptography;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

public sealed class PostgreSqlContainerFixture : IAsyncLifetime, IAsyncDisposable
{
    public const string ImageReference =
        "postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a";

    public const int ExpectedServerVersionNumber = 180004;

    internal const string DatabaseNamePrefix = "mbs_test_";

    private const string AdministrativeDatabaseName = "fixture_admin";
    private const string ContainerOwnershipLabel =
        "com.in4a.minimal-bank-system.postgresql-fixture";

    private readonly SemaphoreSlim containerCleanupGate = new(1, 1);
    private readonly IPostgreSqlContainerDisposer containerDisposer;
    private readonly IContainerResourceOwnerFactory resourceOwnerFactory;
    private readonly Func<PostgreSqlContainer, CancellationToken, Task<int>> serverVersionReader;
    private PostgreSqlContainer? container;
    private IContainerResourceOwner? resourceOwner;
    private bool containerDisposeAttempted;

    public PostgreSqlContainerFixture()
        : this(
            new TestcontainersPostgreSqlContainerDisposer(),
            new DockerContainerResourceOwnerFactory(),
            ReadServerVersionNumberAsync)
    {
    }

    internal PostgreSqlContainerFixture(
        IPostgreSqlContainerDisposer containerDisposer,
        IContainerResourceOwnerFactory resourceOwnerFactory,
        Func<PostgreSqlContainer, CancellationToken, Task<int>>? serverVersionReader = null)
    {
        this.containerDisposer = containerDisposer;
        this.resourceOwnerFactory = resourceOwnerFactory;
        this.serverVersionReader = serverVersionReader ?? ReadServerVersionNumberAsync;
    }

    public int ServerVersionNumber { get; private set; }

    internal PostgreSqlContainer Container =>
        container is not null && !containerDisposeAttempted
            ? container
            : throw new InvalidOperationException(
                "The PostgreSQL test container is not running. Fixture initialization must succeed first.");

    internal bool HasPendingContainerCleanup => resourceOwner is not null;

    public Task InitializeAsync() => InitializeAsync(CancellationToken.None);

    internal async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (container is not null || resourceOwner is not null)
        {
            throw new InvalidOperationException(
                "The PostgreSQL test fixture has already been initialized or still owns pending cleanup.");
        }

        string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        string ownershipId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        containerDisposeAttempted = false;

        try
        {
            resourceOwner = resourceOwnerFactory.Create(ContainerOwnershipLabel, ownershipId);

            PostgreSqlContainer candidate = new PostgreSqlBuilder(ImageReference)
                .WithDatabase(AdministrativeDatabaseName)
                .WithUsername("postgres")
                .WithPassword(password)
                .WithLabel(ContainerOwnershipLabel, ownershipId)
                .Build();
            container = candidate;

            await candidate.StartAsync(cancellationToken);
            ServerVersionNumber = await serverVersionReader(candidate, cancellationToken);

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
                await CleanupContainerAsync();
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
        await CleanupContainerAsync();
    }

    ValueTask IAsyncDisposable.DisposeAsync() => new(DisposeAsync());

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
        PostgreSqlContainer candidate,
        CancellationToken cancellationToken)
    {
        string connectionString = BuildConnectionString(candidate, AdministrativeDatabaseName);
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

    private async Task CleanupContainerAsync()
    {
        await containerCleanupGate.WaitAsync(CancellationToken.None);

        try
        {
            IContainerResourceOwner? cleanupOwner = resourceOwner;

            if (cleanupOwner is null)
            {
                return;
            }

            Exception? testcontainersException = null;

            if (!containerDisposeAttempted && container is not null)
            {
                // Testcontainers 4.13.0 latches its disposed state before Docker removal.
                // Once this call starts, only the independent resource owner may retry cleanup.
                containerDisposeAttempted = true;

                try
                {
                    await containerDisposer.DisposeAsync(container);
                }
                catch (Exception exception)
                {
                    testcontainersException = exception;
                }
            }

            Exception? independentCleanupException = null;

            try
            {
                await cleanupOwner.RemoveContainersAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                independentCleanupException = exception;
            }

            if (independentCleanupException is null)
            {
                container = null;
                resourceOwner = null;
                cleanupOwner.Dispose();
            }

            if (testcontainersException is not null && independentCleanupException is not null)
            {
                throw new InvalidOperationException(
                    $"Failed to dispose the PostgreSQL test container using '{ImageReference}', " +
                    "and independent Docker cleanup also failed. The fixture retains the " +
                    "independent resource identity so cleanup can be retried without reusing " +
                    "the poisoned Testcontainers instance.",
                    new AggregateException(testcontainersException, independentCleanupException));
            }

            if (testcontainersException is not null)
            {
                throw new InvalidOperationException(
                    $"Failed to dispose the PostgreSQL test container using '{ImageReference}'. " +
                    "Independent Docker cleanup removed the owned resource; the original " +
                    "cleanup failure remains visible.",
                    testcontainersException);
            }

            if (independentCleanupException is not null)
            {
                throw new InvalidOperationException(
                    $"Independent Docker cleanup failed for the PostgreSQL test container " +
                    $"using '{ImageReference}'. The fixture retains the independent resource " +
                    "identity so cleanup can be retried.",
                    independentCleanupException);
            }
        }
        finally
        {
            containerCleanupGate.Release();
        }
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
