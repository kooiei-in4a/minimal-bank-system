using Npgsql;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests;

public sealed class PostgreSqlTestFixture : IAsyncLifetime
{
    public const string Category = "PostgreSql";

    public const string ImageReference =
        "postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a";

    private const string AdminDatabase = "postgres";
    private const string AdminUsername = "postgres";
    private const string AdminPassword = "postgres";

    private PostgreSqlContainer? container;

    public string AdminConnectionString => GetContainer().GetConnectionString();

    public async Task InitializeAsync()
    {
        PostgreSqlContainer candidate = new PostgreSqlBuilder(ImageReference)
            .WithDatabase(AdminDatabase)
            .WithUsername(AdminUsername)
            .WithPassword(AdminPassword)
            .Build();

        try
        {
            await candidate.StartAsync();
            container = candidate;
        }
        catch (Exception startException)
        {
            try
            {
                await candidate.DisposeAsync();
            }
            catch (Exception cleanupException)
            {
                throw new PostgreSqlFixtureException(
                    $"PostgreSQL container could not start from image '{ImageReference}', " +
                    "and cleanup of the failed container also failed.",
                    new AggregateException(startException, cleanupException));
            }

            throw new PostgreSqlFixtureException(
                $"PostgreSQL container could not start from image '{ImageReference}'. " +
                "Docker is required for tests in the PostgreSql category.",
                startException);
        }
    }

    public async Task DisposeAsync()
    {
        PostgreSqlContainer? current = Interlocked.Exchange(ref container, null);
        if (current is null)
        {
            return;
        }

        try
        {
            await current.DisposeAsync();
        }
        catch (Exception exception)
        {
            throw new PostgreSqlFixtureException(
                $"PostgreSQL container cleanup failed for image '{ImageReference}'.",
                exception);
        }
    }

    public async Task<PostgreSqlDatabaseLease> CreateDatabaseAsync()
    {
        string databaseName = $"fnd03_{Guid.NewGuid():N}";

        try
        {
            await using NpgsqlConnection connection = await OpenAdminConnectionAsync();
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{databaseName}\";";
            await command.ExecuteNonQueryAsync();

            NpgsqlConnectionStringBuilder connectionString =
                new(GetContainer().GetConnectionString())
                {
                    Database = databaseName,
                };

            return new PostgreSqlDatabaseLease(this, databaseName, connectionString.ConnectionString);
        }
        catch (PostgreSqlFixtureException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PostgreSqlFixtureException(
                $"PostgreSQL isolated database '{databaseName}' could not be created.",
                exception);
        }
    }

    internal async Task DropDatabaseAsync(string databaseName)
    {
        try
        {
            await using NpgsqlConnection connection = await OpenAdminConnectionAsync();
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE \"{databaseName}\" WITH (FORCE);";
            await command.ExecuteNonQueryAsync();
        }
        catch (PostgreSqlFixtureException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PostgreSqlFixtureException(
                $"Cleanup failed while dropping PostgreSQL database '{databaseName}'.",
                exception);
        }
    }

    private async Task<NpgsqlConnection> OpenAdminConnectionAsync()
    {
        NpgsqlConnection connection = new(GetContainer().GetConnectionString());

        try
        {
            await connection.OpenAsync();
            return connection;
        }
        catch (Exception exception)
        {
            await connection.DisposeAsync();
            throw new PostgreSqlFixtureException(
                $"PostgreSQL connection to the test container failed for database '{AdminDatabase}'.",
                exception);
        }
    }

    private PostgreSqlContainer GetContainer() =>
        container ?? throw new PostgreSqlFixtureException(
            "The PostgreSQL test fixture is not initialized. Container startup must succeed before a test runs.");
}

public sealed class PostgreSqlDatabaseLease(
    PostgreSqlTestFixture owner,
    string databaseName,
    string connectionString) : IAsyncDisposable
{
    private int disposed;

    public string DatabaseName { get; } = databaseName;

    public string ConnectionString { get; } = connectionString;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        await owner.DropDatabaseAsync(DatabaseName);
    }
}

public sealed class PostgreSqlFixtureException(string message, Exception? innerException = null)
    : Exception(message, innerException);
