using Npgsql;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

public sealed class PostgreSqlContainerFixture : IAsyncDisposable
{
    public const string ImageReference =
        "postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a";

    public const string AdminDatabaseName = "postgres";

    private const string Username = "minibank";
    private const string Password = "minibank";

    private readonly SemaphoreSlim startLock = new(1, 1);
    private readonly PostgreSqlContainer container = CreateContainer(ImageReference);
    private bool started;

    public string AdminConnectionString => container.GetConnectionString();

    public string ImageRepository => container.Image.Repository;

    public string ImageTag => container.Image.Tag;

    public string ImageDigest => container.Image.Digest;

    public async Task EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        if (started)
        {
            return;
        }

        await startLock.WaitAsync(cancellationToken);
        try
        {
            if (started)
            {
                return;
            }

            await container.StartAsync(cancellationToken);
            started = true;
        }
        finally
        {
            startLock.Release();
        }
    }

    public string GetDatabaseConnectionString(string databaseName)
    {
        NpgsqlConnectionStringBuilder builder = new(AdminConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        };

        return builder.ConnectionString;
    }

    public static PostgreSqlContainer CreateContainer(string imageReference) =>
        new PostgreSqlBuilder(imageReference)
            .WithDatabase(AdminDatabaseName)
            .WithUsername(Username)
            .WithPassword(Password)
            .Build();

    public async ValueTask DisposeAsync()
    {
        await startLock.WaitAsync();
        try
        {
            await container.DisposeAsync();
        }
        finally
        {
            startLock.Release();
            startLock.Dispose();
        }
    }
}
