using Npgsql;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Verifies shared container lifecycle and digest-pinned PostgreSQL 18 startup.
/// </summary>
[Trait("Category", PostgreSqlTestCategories.Category)]
public sealed class PostgreSqlLifecycleTests
{
    [Fact]
    public void ImageReferenceIsDigestPinnedToBenchmarkImage()
    {
        Assert.Equal(
            "postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a",
            PostgreSqlTestImage.Reference);
        Assert.Contains("@sha256:", PostgreSqlTestImage.Reference, StringComparison.Ordinal);
        Assert.Equal(PostgreSqlTestImage.DigestSha256, PostgreSqlTestImage.Reference.Split("sha256:")[1]);
    }

    [Fact]
    public async Task SharedContainerStartsRealPostgreSql18()
    {
        PostgreSqlContainer container = await SharedPostgreSqlContainer.GetOrStartAsync();

        string image = container.Image.FullName;
        Assert.Contains(PostgreSqlTestImage.DigestSha256, image, StringComparison.Ordinal);

        await using NpgsqlConnection connection = new(container.GetConnectionString());
        await connection.OpenAsync();

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SHOW server_version;";
        string? version = (string?)await command.ExecuteScalarAsync();

        Assert.NotNull(version);
        Assert.StartsWith("18.", version, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestDatabaseLifecycleCreatesAndDropsDatabase()
    {
        string databaseName;
        await using (PostgreSqlTestDatabase database = await PostgreSqlTestDatabase.CreateAsync())
        {
            databaseName = database.DatabaseName;

            await using NpgsqlConnection connection = new(database.ConnectionString);
            await connection.OpenAsync();

            await using NpgsqlCommand createProbe = connection.CreateCommand();
            createProbe.CommandText = "CREATE TABLE lifecycle_probe(id int PRIMARY KEY);";
            await createProbe.ExecuteNonQueryAsync();
        }

        PostgreSqlContainer container = await SharedPostgreSqlContainer.GetOrStartAsync();
        await using NpgsqlConnection admin = new(container.GetConnectionString());
        await admin.OpenAsync();
        await using NpgsqlCommand exists = admin.CreateCommand();
        exists.CommandText = "SELECT COUNT(*) FROM pg_database WHERE datname = @name;";
        exists.Parameters.AddWithValue("name", databaseName);
        long count = (long)(await exists.ExecuteScalarAsync())!;
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ConnectionFailureIsAClearHardFailure()
    {
        NpgsqlConnectionStringBuilder builder = new()
        {
            Host = "127.0.0.1",
            Port = 1,
            Database = "postgres",
            Username = "postgres",
            Password = "postgres",
            Timeout = 1,
        };

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
            {
                try
                {
                    await using NpgsqlConnection connection = new(builder.ConnectionString);
                    await connection.OpenAsync();
                }
                catch (Exception exception) when (exception is not InvalidOperationException)
                {
                    throw new InvalidOperationException(
                        "Failed to connect to PostgreSQL for an integration test. " +
                        "This is a hard test failure; provider tests do not fall back to InMemory or SQLite.",
                        exception);
                }
            });

        Assert.Contains("Failed to connect to PostgreSQL", failure.Message, StringComparison.Ordinal);
        Assert.NotNull(failure.InnerException);
    }
}
