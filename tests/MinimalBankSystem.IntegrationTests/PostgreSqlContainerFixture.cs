using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace MinimalBankSystem.IntegrationTests;

/// <summary>
/// Owns one disposable PostgreSQL 18 container for the PostgreSQL integration-test collection.
/// Each test receives a separate database through <see cref="CreateDatabaseAsync"/>.
/// </summary>
public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    public const string ImageReference =
        "postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a";

    private const string AdministrativeDatabase = "postgres";
    private PostgreSqlContainer? container;

    public async Task InitializeAsync()
    {
        PostgreSqlContainer createdContainer = new PostgreSqlBuilder(ImageReference).Build();

        try
        {
            await createdContainer.StartAsync();

            await using NpgsqlConnection connection = new(CreateAdministrativeConnectionString(createdContainer));
            await connection.OpenAsync();
            await using NpgsqlCommand command = new("SELECT 1", connection);
            _ = await command.ExecuteScalarAsync();

            container = createdContainer;
        }
        catch (Exception startupException)
        {
            try
            {
                await createdContainer.DisposeAsync();
            }
            catch (Exception containerCleanupException)
            {
                throw new InvalidOperationException(
                    "PostgreSQL Testcontainers fixture startup failed and its partial container could not be cleaned up.",
                    new AggregateException(startupException, containerCleanupException));
            }

            throw new InvalidOperationException(
                "PostgreSQL Testcontainers fixture could not start the pinned PostgreSQL image or open its control connection.",
                startupException);
        }
    }

    public async Task DisposeAsync()
    {
        PostgreSqlContainer? activeContainer = container;
        container = null;

        if (activeContainer is null)
        {
            return;
        }

        try
        {
            await activeContainer.DisposeAsync();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "PostgreSQL Testcontainers fixture shutdown failed.",
                exception);
        }
    }

    public async Task<PostgreSqlTestDatabase> CreateDatabaseAsync()
    {
        string databaseName = $"fnd03_{Guid.NewGuid():N}";

        try
        {
            await ExecuteAdministrativeCommandAsync($"CREATE DATABASE {QuoteIdentifier(databaseName)}");
            return new PostgreSqlTestDatabase(
                databaseName,
                CreateDatabaseConnectionString(databaseName),
                DropDatabaseAsync);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"PostgreSQL integration test database creation failed for '{databaseName}'.",
                exception);
        }
    }

    public async Task<bool> DatabaseExistsAsync(string databaseName)
    {
        const string query = "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @databaseName)";

        await using NpgsqlConnection connection = new(CreateAdministrativeConnectionString());
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(query, connection);
        command.Parameters.AddWithValue("databaseName", databaseName);

        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private async Task DropDatabaseAsync(string databaseName)
    {
        try
        {
            await ExecuteAdministrativeCommandAsync(
                $"DROP DATABASE IF EXISTS {QuoteIdentifier(databaseName)} WITH (FORCE)");
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"PostgreSQL integration test database cleanup failed for '{databaseName}'.",
                exception);
        }
    }

    private async Task ExecuteAdministrativeCommandAsync(string commandText)
    {
        await using NpgsqlConnection connection = new(CreateAdministrativeConnectionString());
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        _ = await command.ExecuteNonQueryAsync();
    }

    private string CreateAdministrativeConnectionString() =>
        CreateAdministrativeConnectionString(
            container ?? throw new InvalidOperationException("PostgreSQL Testcontainers fixture is not initialized."));

    private static string CreateAdministrativeConnectionString(PostgreSqlContainer postgreSqlContainer)
    {
        NpgsqlConnectionStringBuilder builder = new(postgreSqlContainer.GetConnectionString())
        {
            Database = AdministrativeDatabase,
        };

        return builder.ConnectionString;
    }

    private string CreateDatabaseConnectionString(string databaseName)
    {
        NpgsqlConnectionStringBuilder builder = new(CreateAdministrativeConnectionString())
        {
            Database = databaseName,
        };

        return builder.ConnectionString;
    }

    private static string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}

public sealed class PostgreSqlTestDatabase(
    string name,
    string connectionString,
    Func<string, Task> cleanup) : IAsyncDisposable
{
    private int disposed;

    public string Name { get; } = name;

    public string ConnectionString { get; } = connectionString;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            await cleanup(Name);
        }
    }
}

public abstract class PostgreSqlIntegrationTestBase(PostgreSqlContainerFixture fixture) : IAsyncLifetime
{
    protected PostgreSqlContainerFixture Fixture { get; } = fixture;

    protected PostgreSqlTestDatabase Database { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Database = await Fixture.CreateDatabaseAsync();
    }

    public Task DisposeAsync() => Database.DisposeAsync().AsTask();
}
