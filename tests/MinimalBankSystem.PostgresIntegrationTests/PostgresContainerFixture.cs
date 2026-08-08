using Npgsql;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.PostgresIntegrationTests;

/// <summary>
/// Owns exactly one PostgreSQL 18 container for every test that shares
/// <see cref="PostgresCollection"/>. Individual databases, not containers, are the
/// per-test isolation unit: <see cref="CreateDatabaseAsync"/> provisions a uniquely
/// named database per caller so tests never observe each other's state even while the
/// underlying container is reused for cost reasons.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private PostgreSqlContainer? container;

    public PostgreSqlContainer Container =>
        container ?? throw new InvalidOperationException(
            "The PostgreSQL container has not started. Fixture initialization failed before any test could run.");

    public async Task InitializeAsync()
    {
        PostgreSqlContainer started = new PostgreSqlBuilder(PostgresImage.Reference).Build();
        await started.StartAsync();
        container = started;
    }

    public async Task DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    public async Task<PostgresTestDatabase> CreateDatabaseAsync(CancellationToken cancellationToken = default)
    {
        string databaseName = $"fnd03_{Guid.NewGuid():N}";

        await using NpgsqlConnection admin = new(Container.GetConnectionString());
        await admin.OpenAsync(cancellationToken);

        await using (NpgsqlCommand create = new($"CREATE DATABASE \"{databaseName}\"", admin))
        {
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        NpgsqlConnectionStringBuilder scoped = new(Container.GetConnectionString())
        {
            Database = databaseName,
        };

        return new PostgresTestDatabase(this, databaseName, scoped.ConnectionString);
    }

    internal async Task DropDatabaseAsync(
        string databaseName,
        bool force,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection admin = new(Container.GetConnectionString());
        await admin.OpenAsync(cancellationToken);

        string forceClause = force ? " WITH (FORCE)" : string.Empty;
        await using NpgsqlCommand drop = new($"DROP DATABASE \"{databaseName}\"{forceClause}", admin);
        await drop.ExecuteNonQueryAsync(cancellationToken);
    }
}
