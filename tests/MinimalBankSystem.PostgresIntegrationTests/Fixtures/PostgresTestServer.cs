using System.Globalization;
using System.Text;
using DotNet.Testcontainers.Images;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.PostgresIntegrationTests.Fixtures;

/// <summary>
/// Owns the single pinned PostgreSQL container used by this test assembly and hands out
/// isolated per-test databases on it.
/// </summary>
/// <remarks>
/// <para>
/// Ownership: exactly one container per test assembly (one test process). It is started lazily on
/// first use and removed by <see cref="PostgresTestFramework"/> when the assembly run ends.
/// </para>
/// <para>
/// The container is shared, but it is not shared mutable test state: the only mutable state a test
/// touches is its own database, created by <see cref="CreateDatabaseAsync"/> and dropped when the
/// test ends. Cluster-level DDL is serialized by <see cref="clusterDdlGate"/> so that concurrent
/// tests never race on <c>CREATE DATABASE</c> / <c>DROP DATABASE</c>.
/// </para>
/// </remarks>
public sealed class PostgresTestServer : IAsyncDisposable
{
    /// <summary>The database name prefix used for every isolated test database.</summary>
    public const string DatabaseNamePrefix = "mbs_";

    private const string MaintenanceDatabase = "postgres";
    private const string SuperUser = "postgres";
    private const int MaximumLabelLength = 20;

    private static readonly Lazy<Task<PostgresTestServer>> SharedServer =
        new(() => StartAsync(), LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly PostgreSqlContainer container;
    private readonly NpgsqlDataSource maintenanceDataSource;
    private readonly SemaphoreSlim clusterDdlGate = new(1, 1);
    private int disposed;

    private PostgresTestServer(PostgreSqlContainer container, NpgsqlDataSource maintenanceDataSource)
    {
        this.container = container;
        this.maintenanceDataSource = maintenanceDataSource;
    }

    /// <summary>The identifier of the running container. Identical for every test in the assembly.</summary>
    public string ContainerId => container.Id;

    /// <summary>The image the container actually runs, used to prove the digest pin at runtime.</summary>
    public IImage Image => container.Image;

    /// <summary>
    /// Returns the assembly-wide server, starting the pinned container on first use.
    /// </summary>
    /// <remarks>
    /// A failed start is cached: every subsequent test fails immediately with the same
    /// <see cref="PostgresTestInfrastructureException"/> instead of retrying a container start.
    /// </remarks>
    public static Task<PostgresTestServer> SharedAsync() => SharedServer.Value;

    /// <summary>
    /// Starts a PostgreSQL server from the pinned image and verifies that it is reachable and is
    /// really PostgreSQL 18.
    /// </summary>
    /// <param name="dockerEndpoint">
    /// An explicit container runtime endpoint. Only used by the failure-reporting tests; production
    /// fixture usage passes <see langword="null"/> and relies on the ambient runtime.
    /// </param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <exception cref="PostgresTestInfrastructureException">
    /// The container runtime is unavailable, the container fails to start, the server cannot be
    /// reached, or the server is not the pinned PostgreSQL 18 image.
    /// </exception>
    public static async Task<PostgresTestServer> StartAsync(
        string? dockerEndpoint = null,
        CancellationToken cancellationToken = default)
    {
        PostgreSqlBuilder builder = new PostgreSqlBuilder(PostgresTestImage.Reference)
            .WithDatabase(MaintenanceDatabase)
            .WithUsername(SuperUser)
            .WithPassword(Guid.NewGuid().ToString("N"))
            .WithCleanUp(true);

        if (dockerEndpoint is not null)
        {
            builder = builder.WithDockerEndpoint(dockerEndpoint);
        }

        PostgreSqlContainer container = builder.Build();

        try
        {
            await container.StartAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            await DisposeQuietlyAsync(container);
            throw new PostgresTestInfrastructureException(
                $"Could not start the pinned PostgreSQL test container '{PostgresTestImage.Reference}'. " +
                "PostgreSQL integration tests require a working container runtime and are never " +
                "skipped or redirected to another database provider.",
                exception);
        }

        NpgsqlDataSource maintenanceDataSource =
            new NpgsqlDataSourceBuilder(container.GetConnectionString()).Build();

        try
        {
            VerifyPinnedImage(container);
            await VerifyServerAsync(maintenanceDataSource, cancellationToken);
        }
        catch
        {
            await maintenanceDataSource.DisposeAsync();
            await DisposeQuietlyAsync(container);
            throw;
        }

        return new PostgresTestServer(container, maintenanceDataSource);
    }

    /// <summary>
    /// Creates an isolated database owned by a single test.
    /// </summary>
    /// <param name="label">An optional readable hint, sanitized into the database name.</param>
    /// <param name="cancellationToken">Cancels the creation.</param>
    /// <remarks>
    /// The database is created from <c>template0</c>, which cannot be connected to and therefore
    /// cannot carry state left behind by another test. Because PostgreSQL scopes advisory locks,
    /// sequences, temporary objects and schema-qualified names per database, a database is the
    /// isolation unit rather than a schema.
    /// </remarks>
    public async Task<PostgresTestDatabase> CreateDatabaseAsync(
        string? label = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

        string databaseName = BuildDatabaseName(label);

        try
        {
            await ExecuteClusterStatementAsync(
                $"CREATE DATABASE \"{databaseName}\" TEMPLATE template0",
                cancellationToken);
        }
        catch (Exception exception)
        {
            throw new PostgresTestInfrastructureException(
                $"Could not create the isolated test database '{databaseName}' on {Describe()}.",
                exception);
        }

        try
        {
            NpgsqlDataSource dataSource =
                new NpgsqlDataSourceBuilder(BuildConnectionString(databaseName)).Build();

            return new PostgresTestDatabase(this, databaseName, dataSource);
        }
        catch
        {
            // The database exists but nothing owns it yet, so drop it here rather than leak it.
            await ExecuteClusterStatementAsync(
                $"DROP DATABASE \"{databaseName}\" WITH (FORCE)",
                CancellationToken.None);
            throw;
        }
    }

    /// <summary>Builds a connection string for a database on this server.</summary>
    public string BuildConnectionString(string databaseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        return new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = databaseName,
        }.ConnectionString;
    }

    /// <summary>Reports whether a database currently exists on this server.</summary>
    public async Task<bool> DatabaseExistsAsync(
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        await using NpgsqlConnection connection =
            await maintenanceDataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = $1)";
        command.Parameters.AddWithValue(databaseName);

        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    /// <summary>
    /// Runs a cluster-level statement on the maintenance database, serialized against every other
    /// cluster-level statement issued through this server.
    /// </summary>
    /// <remarks>
    /// Test bodies run fully in parallel; only <c>CREATE DATABASE</c> and <c>DROP DATABASE</c> are
    /// serialized, because PostgreSQL locks the template and the target database while copying and
    /// dropping. Serializing here keeps the parallel policy free of retry loops.
    /// </remarks>
    public async Task ExecuteClusterStatementAsync(
        string sql,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

        await clusterDdlGate.WaitAsync(cancellationToken);

        try
        {
            await using NpgsqlConnection connection =
                await maintenanceDataSource.OpenConnectionAsync(cancellationToken);
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            clusterDdlGate.Release();
        }
    }

    /// <summary>Stops and removes the container owned by this server.</summary>
    /// <exception cref="PostgresTestInfrastructureException">The container could not be removed.</exception>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        await maintenanceDataSource.DisposeAsync();

        try
        {
            await container.DisposeAsync();
        }
        catch (Exception exception)
        {
            throw new PostgresTestInfrastructureException(
                $"Could not remove the PostgreSQL test container '{container.Id}'. " +
                "The container may still be running and must be removed manually.",
                exception);
        }
        finally
        {
            clusterDdlGate.Dispose();
        }
    }

    /// <summary>
    /// Opens a connection through a data source and fails with a fixture-specific exception when
    /// the server cannot be reached or is not the pinned PostgreSQL 18 build.
    /// </summary>
    internal static async Task VerifyServerAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        string version;
        int serverVersionNumber;

        try
        {
            await using NpgsqlConnection connection =
                await dataSource.OpenConnectionAsync(cancellationToken);
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT version(), current_setting('server_version_num')";
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new PostgresTestInfrastructureException(
                    "The PostgreSQL test server did not report its version.");
            }

            version = reader.GetString(0);
            serverVersionNumber = int.Parse(reader.GetString(1), CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is not PostgresTestInfrastructureException)
        {
            throw new PostgresTestInfrastructureException(
                $"Could not connect to the PostgreSQL test server at {Describe(dataSource.ConnectionString)}. " +
                "PostgreSQL integration tests fail instead of falling back to an in-memory or SQLite provider.",
                exception);
        }

        if (!version.StartsWith(PostgresTestImage.ExpectedVersionPrefix, StringComparison.Ordinal) ||
            serverVersionNumber < PostgresTestImage.MinimumServerVersionNumber)
        {
            throw new PostgresTestInfrastructureException(
                $"Expected the pinned image '{PostgresTestImage.Reference}' to report " +
                $"'{PostgresTestImage.ExpectedVersionPrefix}', but the server reported '{version}'.");
        }
    }

    /// <summary>
    /// Disposes the assembly-wide server if it was started. Called once by
    /// <see cref="PostgresTestFramework"/> at the end of the assembly run.
    /// </summary>
    internal static async Task ShutdownSharedAsync()
    {
        if (!SharedServer.IsValueCreated)
        {
            return;
        }

        Task<PostgresTestServer> pending = SharedServer.Value;

        if (pending.IsFaulted || pending.IsCanceled)
        {
            // The start failure was already reported to every test that awaited it.
            return;
        }

        PostgresTestServer server = await pending;
        await server.DisposeAsync();
    }

    private static void VerifyPinnedImage(PostgreSqlContainer container)
    {
        string? digest = container.Image.Digest;

        if (!string.Equals(digest, PostgresTestImage.Digest, StringComparison.Ordinal))
        {
            throw new PostgresTestInfrastructureException(
                $"The PostgreSQL test container runs image '{container.Image.FullName}' with digest " +
                $"'{digest ?? "<none>"}', but '{PostgresTestImage.Digest}' is pinned.");
        }
    }

    private static async ValueTask DisposeQuietlyAsync(PostgreSqlContainer container)
    {
        try
        {
            await container.DisposeAsync();
        }
        catch (Exception)
        {
            // A container that never started may not exist. The original start failure is the
            // reported one; the Testcontainers resource reaper removes any leftover container.
        }
    }

    private static string Describe(string connectionString)
    {
        try
        {
            NpgsqlConnectionStringBuilder builder = new(connectionString);
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{builder.Host}:{builder.Port}/{builder.Database}");
        }
        catch (ArgumentException)
        {
            return "<unparsable connection string>";
        }
    }

    private static string BuildDatabaseName(string? label)
    {
        string sanitizedLabel = SanitizeLabel(label);
        string unique = Guid.NewGuid().ToString("N");

        return sanitizedLabel.Length == 0
            ? DatabaseNamePrefix + unique
            : DatabaseNamePrefix + sanitizedLabel + "_" + unique;
    }

    private static string SanitizeLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return string.Empty;
        }

        StringBuilder sanitized = new(MaximumLabelLength);

        foreach (char character in label.ToLowerInvariant())
        {
            if (sanitized.Length == MaximumLabelLength)
            {
                break;
            }

            if (char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character))
            {
                sanitized.Append(character);
            }
            else if (sanitized.Length > 0 && sanitized[^1] != '_')
            {
                sanitized.Append('_');
            }
        }

        return sanitized.ToString().TrimEnd('_');
    }

    private string Describe() => Describe(container.GetConnectionString());
}
