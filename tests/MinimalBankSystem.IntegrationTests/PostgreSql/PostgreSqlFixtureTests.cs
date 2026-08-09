using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class PostgreSqlFixtureTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task PinnedPostgreSql184ContainerProvidesTheTestDatabase()
    {
        Assert.Equal(PostgreSqlContainerFixture.ImageReference, Fixture.Container.Image.FullName);
        Assert.Equal(
            "sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a",
            Fixture.Container.Image.Digest);
        Assert.Equal(PostgreSqlContainerFixture.ExpectedServerVersionNumber, Fixture.ServerVersionNumber);

        string currentDatabase = await ExecuteScalarAsync<string>(
            Database.ConnectionString,
            "SELECT current_database();");
        int serverVersionNumber = int.Parse(
            await ExecuteScalarAsync<string>(Database.ConnectionString, "SHOW server_version_num;"),
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(Database.DatabaseName, currentDatabase);
        Assert.Equal(PostgreSqlContainerFixture.ExpectedServerVersionNumber, serverVersionNumber);
    }

    [Fact]
    public async Task SeparateDatabasesDoNotShareProbeState()
    {
        await using PostgreSqlTestDatabase other = await Fixture.CreateDatabaseAsync();

        Assert.NotEqual(Database.DatabaseName, other.DatabaseName);

        await ExecuteNonQueryAsync(
            Database.ConnectionString,
            "CREATE TABLE isolation_probe (value integer NOT NULL); " +
            "INSERT INTO isolation_probe VALUES (41);");

        bool ownerHasProbe = await ExecuteScalarAsync<bool>(
            Database.ConnectionString,
            "SELECT to_regclass('public.isolation_probe') IS NOT NULL;");
        bool otherHasProbe = await ExecuteScalarAsync<bool>(
            other.ConnectionString,
            "SELECT to_regclass('public.isolation_probe') IS NOT NULL;");

        Assert.True(ownerHasProbe);
        Assert.False(otherHasProbe);
    }

    [Fact]
    public async Task DisposingADatabaseScopeRemovesTheDatabase()
    {
        PostgreSqlTestDatabase temporary = await Fixture.CreateDatabaseAsync();
        string databaseName = temporary.DatabaseName;

        Assert.True(await Fixture.DatabaseExistsAsync(databaseName));

        await temporary.DisposeAsync();

        Assert.False(await Fixture.DatabaseExistsAsync(databaseName));
    }

    [Fact]
    public async Task CleanupFailureIsVisibleAndTheDatabaseRemainsRetryable()
    {
        PostgreSqlTestDatabase temporary = await Fixture.CreateDatabaseAsync();
        string databaseName = temporary.DatabaseName;

        try
        {
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => temporary.DisposeAsync(cancellation.Token).AsTask());

            Assert.Contains("Failed to drop isolated PostgreSQL test database", failure.Message, StringComparison.Ordinal);
            Assert.Contains(databaseName, failure.Message, StringComparison.Ordinal);
            Assert.True(await Fixture.DatabaseExistsAsync(databaseName));
        }
        finally
        {
            await temporary.DisposeAsync();
        }

        Assert.False(await Fixture.DatabaseExistsAsync(databaseName));
    }

    [Fact]
    public async Task IndependentDatabaseScopesExecuteRealPostgreSqlWorkConcurrently()
    {
        await using PostgreSqlTestDatabase left = await Fixture.CreateDatabaseAsync();
        await using PostgreSqlTestDatabase right = await Fixture.CreateDatabaseAsync();

        Task<ExecutionInterval> leftExecution = MeasureServerExecutionAsync(left.ConnectionString);
        Task<ExecutionInterval> rightExecution = MeasureServerExecutionAsync(right.ConnectionString);
        ExecutionInterval[] intervals = await Task.WhenAll(leftExecution, rightExecution);

        Assert.True(
            intervals[0].StartedAt < intervals[1].FinishedAt &&
            intervals[1].StartedAt < intervals[0].FinishedAt,
            $"Expected overlapping PostgreSQL work, but observed {intervals[0]} and {intervals[1]}.");
    }

    [Fact]
    public async Task ContainerCleanupHandleIsCapturedAfterStartup()
    {
        Assert.NotNull(Fixture.CleanupHandle);
        Assert.NotEmpty(Fixture.CleanupHandle!.ContainerId);
        Assert.True(Fixture.HasContainerReference);
    }

    [Fact]
    public async Task ActualContainerIsRemovedAfterSuccessfulCleanup()
    {
        PostgreSqlContainerFixture standalone = new();

        try
        {
            await standalone.InitializeAsync();

            Assert.NotNull(standalone.CleanupHandle);
            string containerId = standalone.CleanupHandle!.ContainerId;
            Assert.NotEmpty(containerId);

            bool existsBefore = await ContainerExistsViaDockerCli(containerId);
            Assert.True(existsBefore, "Container should exist before cleanup.");

            await standalone.DisposeAsync();

            bool existsAfter = await ContainerExistsViaDockerCli(containerId);
            Assert.False(existsAfter, "Container should be removed after successful cleanup.");
        }
        finally
        {
            await standalone.DisposeAsync();
        }
    }

    [Fact]
    public async Task TestcontainersDisposedStateIsLatchedAfterFirstDispose()
    {
        Testcontainers.PostgreSql.PostgreSqlContainer directContainer =
            new Testcontainers.PostgreSql.PostgreSqlBuilder(PostgreSqlContainerFixture.ImageReference)
                .WithDatabase("test_disposed_state")
                .WithUsername("postgres")
                .WithPassword("test-only-password")
                .Build();

        try
        {
            await directContainer.StartAsync();
            string containerId = directContainer.Id;

            await directContainer.DisposeAsync();

            bool existsAfterFirstDispose = await ContainerExistsViaDockerCli(containerId);
            Assert.False(existsAfterFirstDispose, "Container should be removed after first DisposeAsync.");

            await directContainer.DisposeAsync();

            bool existsAfterSecondDispose = await ContainerExistsViaDockerCli(containerId);
            Assert.False(existsAfterSecondDispose, "Second DisposeAsync should be a no-op.");
        }
        finally
        {
            await directContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task FallbackCleanupRemovesContainerWhenPrimaryFails()
    {
        PostgreSqlContainerFixture standalone = new();

        try
        {
            await standalone.InitializeAsync();

            Assert.NotNull(standalone.CleanupHandle);
            string containerId = standalone.CleanupHandle!.ContainerId;

            bool existsBefore = await ContainerExistsViaDockerCli(containerId);
            Assert.True(existsBefore, "Container should exist before cleanup.");

            await standalone.CleanupHandle.ForceRemoveAsync();

            bool existsAfterFallback = await ContainerExistsViaDockerCli(containerId);
            Assert.False(existsAfterFallback, "Container should be removed after fallback cleanup.");

            await standalone.DisposeAsync();
        }
        finally
        {
            await standalone.DisposeAsync();
        }
    }

    private static async Task<bool> ContainerExistsViaDockerCli(string containerId)
    {
        System.Diagnostics.ProcessStartInfo startInfo = new("docker", $"ps -q --filter \"id={containerId}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using System.Diagnostics.Process process = new() { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch
        {
            return false;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            string stdout = await process.StandardOutput.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);
            return !string.IsNullOrWhiteSpace(stdout);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(); } catch { }
            return false;
        }
    }

    private static async Task<ExecutionInterval> MeasureServerExecutionAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT statement_timestamp(), pg_sleep(1), clock_timestamp();",
            connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        return new ExecutionInterval(reader.GetDateTime(0), reader.GetDateTime(2));
    }

    private static async Task<T> ExecuteScalarAsync<T>(
        string connectionString,
        string commandText)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        object? result = await command.ExecuteScalarAsync();
        return Assert.IsType<T>(result);
    }

    private static async Task ExecuteNonQueryAsync(
        string connectionString,
        string commandText)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record ExecutionInterval(DateTime StartedAt, DateTime FinishedAt);
}

public abstract class PostgreSqlDatabaseTestBase(
    PostgreSqlContainerFixture fixture) : IAsyncLifetime
{
    private PostgreSqlTestDatabase? database;

    protected PostgreSqlContainerFixture Fixture { get; } = fixture;

    protected PostgreSqlTestDatabase Database =>
        database ?? throw new InvalidOperationException(
            "The per-test PostgreSQL database has not been initialized.");

    public async Task InitializeAsync()
    {
        database = await Fixture.CreateDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        PostgreSqlTestDatabase? candidate = database;

        if (candidate is null)
        {
            return;
        }

        await candidate.DisposeAsync();
        database = null;
    }
}
