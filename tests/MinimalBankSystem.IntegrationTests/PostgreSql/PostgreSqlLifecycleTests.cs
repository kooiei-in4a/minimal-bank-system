using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection(PostgreSqlIntegrationFixture.Name)]
[Trait(PostgreSqlTestCategories.TraitName, PostgreSqlTestCategories.Integration)]
public sealed class PostgreSqlLifecycleTests : PostgreSqlIntegrationTestBase
{
    public PostgreSqlLifecycleTests(SharedPostgreSqlContainer container)
        : base(container)
    {
    }

    [Fact]
    public void ContainerImageReferenceIsDigestPinned()
    {
        Assert.Equal(
            "postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a",
            PostgreSqlTestImage.Reference);
    }

    [Fact]
    public async Task ContainerAcceptsConnectionsAndReportsPostgreSql18()
    {
        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();

        await using NpgsqlCommand versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "SHOW server_version";
        string version = (string)(await versionCommand.ExecuteScalarAsync())!;

        Assert.StartsWith("18.", version, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestDatabaseIsCreatedBeforeTestExecution()
    {
        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT current_database()";
        string databaseName = (string)(await command.ExecuteScalarAsync())!;

        Assert.StartsWith("test_", databaseName, StringComparison.Ordinal);
    }

    [Fact]
    public void ConnectionToUnreachableHostFailsClearly()
    {
        Exception? exception = Record.Exception(() =>
        {
            using NpgsqlConnection connection = new(
                "Host=127.0.0.1;Port=1;Database=postgres;Username=postgres;Password=postgres;Timeout=2");
            connection.Open();
        });

        Assert.NotNull(exception);
        Assert.IsAssignableFrom<NpgsqlException>(exception);
    }
}
