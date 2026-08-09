using System.Diagnostics;
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
    private PostgreSqlContainer? container;
    private ContainerCleanupHandle? cleanupHandle;

    public PostgreSqlContainerFixture()
    {
    }

    internal PostgreSqlContainerFixture(string dockerEndpoint)
    {
        this.dockerEndpoint = dockerEndpoint;
    }

    public int ServerVersionNumber { get; private set; }

    internal PostgreSqlContainer Container =>
        container ?? throw new InvalidOperationException(
            "The PostgreSQL test container is not running. Fixture initialization must succeed first.");

    internal ContainerCleanupHandle? CleanupHandle => cleanupHandle;

    internal bool HasContainerReference => container is not null;

    internal async Task<bool> ActualContainerExistsAsync()
    {
        if (cleanupHandle is null)
        {
            return false;
        }

        return await cleanupHandle.IsContainerRunningAsync();
    }

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

            string? containerId = null;
            try
            {
                containerId = candidate.Id;
            }
            catch
            {
            }

            if (containerId is not null)
            {
                cleanupHandle = new ContainerCleanupHandle(containerId);
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
            List<Exception> failures = [startupException];

            try
            {
                await candidate.DisposeAsync();
                container = null;
                cleanupHandle = null;
            }
            catch (Exception cleanupException)
            {
                failures.Add(cleanupException);

                if (cleanupHandle is not null)
                {
                    try
                    {
                        await cleanupHandle.ForceRemoveAsync(cancellationToken);
                        container = null;
                        cleanupHandle = null;
                    }
                    catch (Exception fallbackException)
                    {
                        failures.Add(fallbackException);
                    }
                }
            }

            throw new InvalidOperationException(
                $"Failed to start and connect to the PostgreSQL test container using '{ImageReference}'. " +
                "PostgreSQL integration tests require Docker and never fall back to another provider.",
                failures.Count == 1 ? failures[0] : new AggregateException(failures));
        }
    }

    public async Task DisposeAsync()
    {
        PostgreSqlContainer? candidate = container;
        ContainerCleanupHandle? handle = cleanupHandle;

        if (candidate is null && handle is null)
        {
            return;
        }

        if (candidate is not null)
        {
            try
            {
                await candidate.DisposeAsync();
                container = null;
                cleanupHandle = null;
                return;
            }
            catch (Exception disposeException)
            {
                if (handle is not null)
                {
                    try
                    {
                        await handle.ForceRemoveAsync();
                        container = null;
                        cleanupHandle = null;
                        return;
                    }
                    catch (Exception fallbackException)
                    {
                        throw new InvalidOperationException(
                            $"Failed to dispose the PostgreSQL test container using '{ImageReference}'. " +
                            $"Container ID: {handle.ContainerId}. " +
                            "Both primary and fallback cleanup failed.",
                            new AggregateException(disposeException, fallbackException));
                    }
                }

                throw new InvalidOperationException(
                    $"Failed to dispose the PostgreSQL test container using '{ImageReference}'. " +
                    "The Testcontainers instance is in a poisoned state and no independent cleanup handle exists.",
                    disposeException);
            }
        }

        if (handle is not null)
        {
            try
            {
                await handle.ForceRemoveAsync();
                cleanupHandle = null;
            }
            catch (Exception fallbackException)
            {
                throw new InvalidOperationException(
                    $"Failed to remove the PostgreSQL test container using fallback cleanup. " +
                    $"Container ID: {handle.ContainerId}.",
                    fallbackException);
            }
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

internal sealed class ContainerCleanupHandle
{
    public ContainerCleanupHandle(string containerId)
    {
        ContainerId = containerId;
    }

    public string ContainerId { get; }

    public async Task ForceRemoveAsync(CancellationToken cancellationToken = default)
    {
        ProcessStartInfo startInfo = new("docker", $"rm -f {ContainerId}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using Process process = new() { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Failed to start Docker CLI for container removal. Container ID: {ContainerId}.",
                exception);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(); } catch { }
            throw new InvalidOperationException(
                $"Docker CLI timed out while removing container {ContainerId}.");
        }

        await stdoutTask;
        string stderr = await stderrTask;

        if (process.ExitCode != 0 && !stderr.Contains("No such container", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Docker CLI failed to remove container {ContainerId}. Exit code: {process.ExitCode}. Error: {stderr.Trim()}");
        }
    }

    public async Task<bool> IsContainerRunningAsync(CancellationToken cancellationToken = default)
    {
        ProcessStartInfo startInfo = new("docker", $"inspect {ContainerId}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using Process process = new() { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch
        {
            return false;
        }

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        await stdoutTask;
        await stderrTask;

        return process.ExitCode == 0;
    }
}
