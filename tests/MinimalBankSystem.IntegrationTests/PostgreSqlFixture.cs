using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    public const string FixedImage = "postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a";

    public const string FixedDatabase = "postgres";
    public const string FixedUsername = "postgres";
    public const string FixedPassword = "postgres";

    private readonly PostgreSqlContainer _container;

    public string ConnectionString => _container.GetConnectionString();

    public string ImageFullName { get; private set; } = string.Empty;

    public PostgreSqlFixture()
    {
        _container = new PostgreSqlBuilder(FixedImage)
            .WithDatabase(FixedDatabase)
            .WithUsername(FixedUsername)
            .WithPassword(FixedPassword)
            .Build();
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
            ImageFullName = _container.Image?.FullName ?? string.Empty;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"PostgreSQL container failed to start. " +
                $"Image: {FixedImage}. " +
                $"Ensure Docker is running and the image is accessible.", ex);
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            await _container.DisposeAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"PostgreSQL container cleanup failed. Image: {FixedImage}. Error: {ex.Message}", ex);
        }
    }

    public async Task<ExecResult> ExecuteSqlAsync(string database, string sql)
    {
        return await _container.ExecAsync([
            "psql",
            "-U", FixedUsername,
            "-d", database,
            "-c", sql
        ]);
    }

    public string GetConnectionStringForDatabase(string databaseName)
    {
        var baseConnectionString = _container.GetConnectionString();
        return baseConnectionString.Replace($"Database={FixedDatabase}", $"Database={databaseName}");
    }
}
