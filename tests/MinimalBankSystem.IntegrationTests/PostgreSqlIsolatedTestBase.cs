namespace MinimalBankSystem.IntegrationTests;

[Collection("PostgreSQL")]
[Trait("Category", "PostgreSQL")]
public abstract class PostgreSqlIsolatedTestBase : IAsyncLifetime
{
    protected PostgreSqlFixture Fixture { get; }

    protected string DatabaseName { get; private set; } = string.Empty;
    protected string ConnectionString => Fixture.GetConnectionStringForDatabase(DatabaseName);

    protected PostgreSqlIsolatedTestBase(PostgreSqlFixture fixture)
    {
        Fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        DatabaseName = $"test_{GetType().Name}_{Guid.NewGuid():N}";

        var result = await Fixture.ExecuteSqlAsync(
            PostgreSqlFixture.FixedDatabase,
            $"CREATE DATABASE \"{DatabaseName}\";");

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to create test database '{DatabaseName}': {result.Stderr}");
        }
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(DatabaseName))
        {
            return;
        }

        try
        {
            var result = await Fixture.ExecuteSqlAsync(
                PostgreSqlFixture.FixedDatabase,
                $"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE);");

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Cleanup failed: could not drop database '{DatabaseName}': {result.Stderr}");
            }
        }
        catch
        {
            throw;
        }
    }
}
