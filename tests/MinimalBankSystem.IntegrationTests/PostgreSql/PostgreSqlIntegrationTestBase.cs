using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

public abstract class PostgreSqlIntegrationTestBase : IAsyncLifetime
{
    private readonly SharedPostgreSqlContainer _container;
    private PostgreSqlTestDatabase? _database;

    protected PostgreSqlIntegrationTestBase(SharedPostgreSqlContainer container)
    {
        _container = container;
    }

    protected string ConnectionString => _database!.ConnectionString;

    protected SharedPostgreSqlContainer Container => _container;

    public async Task InitializeAsync()
    {
        _database = await PostgreSqlTestDatabase.CreateAsync(_container);
    }

    public async Task DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }
}
