using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

public class PostgreSqlTestDatabase : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture container;

    public PostgreSqlTestDatabase(PostgreSqlContainerFixture container)
        : this(container, "db")
    {
    }

    protected PostgreSqlTestDatabase(PostgreSqlContainerFixture container, string ownerName)
    {
        this.container = container;
        DatabaseName = CreateDatabaseName(ownerName);
    }

    public string DatabaseName { get; }

    public string ConnectionString => container.GetDatabaseConnectionString(DatabaseName);

    public async Task InitializeAsync()
    {
        await CreateAsync(container, DatabaseName);
        if (!await ExistsAsync(container, DatabaseName))
        {
            throw new InvalidOperationException(
                $"Test database '{DatabaseName}' was not created before the test class started.");
        }
    }

    public async Task DisposeAsync() => await DropAsync(container, DatabaseName);

    public static async Task CreateAsync(
        PostgreSqlContainerFixture container,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        await container.EnsureStartedAsync(cancellationToken);
        await PostgreSqlTestSql.ExecuteNonQueryAsync(
            container.AdminConnectionString,
            $"CREATE DATABASE {QuoteIdentifier(databaseName)}",
            cancellationToken);
    }

    public static async Task DropAsync(
        PostgreSqlContainerFixture container,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        await container.EnsureStartedAsync(cancellationToken);
        await PostgreSqlTestSql.ExecuteNonQueryAsync(
            container.AdminConnectionString,
            $"DROP DATABASE {QuoteIdentifier(databaseName)}",
            cancellationToken);
    }

    public static async Task<bool> ExistsAsync(
        PostgreSqlContainerFixture container,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        await container.EnsureStartedAsync(cancellationToken);
        object? result = await PostgreSqlTestSql.ExecuteScalarAsync(
            container.AdminConnectionString,
            "SELECT 1 FROM pg_database WHERE datname = @databaseName",
            cancellationToken,
            new NpgsqlParameter("@databaseName", databaseName));

        return result is not null;
    }

    public static string CreateDatabaseName(string ownerName)
    {
        string sanitized = SanitizeIdentifier(ownerName);
        if (sanitized.Length > 44)
        {
            sanitized = sanitized[..44];
        }

        return $"minibank_{sanitized}_{Guid.NewGuid().ToString("N")[..8]}";
    }

    private static string SanitizeIdentifier(string value)
    {
        char[] chars = value.ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsAsciiLetterOrDigit(chars[i]))
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }

    private static string QuoteIdentifier(string identifier) =>
        string.Concat("\"", identifier.Replace("\"", "\"\""), "\"");
}
