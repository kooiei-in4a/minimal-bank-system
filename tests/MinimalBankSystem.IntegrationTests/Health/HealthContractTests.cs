using System.Net;
using System.Text.Json;
using Docker.DotNet;
using DotNet.Testcontainers.Configurations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MinimalBankSystem.Api.Health;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.IntegrationTests.PostgreSql;
using MinimalBankSystem.Migrator;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.Health;

/// <summary>FND-06 live／ready contract against real PostgreSQL.</summary>
[Trait("Category", "PostgreSqlIntegration")]
public sealed class HealthContractTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string SecretPasswordSentinel = "FND06_HEALTH_PASSWORD_SENTINEL_9F2A41";
    private static readonly TimeSpan MigratorBudget = TimeSpan.FromSeconds(120);

    [Fact]
    public async Task LiveAndReadySucceedWhenPostgresIsMigrated()
    {
        await ApplyMigrationsAsync();

        await using HealthApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage live = await client.GetAsync(HealthEndpoints.LivePath);
        using HttpResponseMessage ready = await client.GetAsync(HealthEndpoints.ReadyPath);
        string readyBody = await ready.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal("Healthy", await live.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("Healthy", readyBody);
        AssertNotBusinessErrorEnvelope(readyBody);
    }

    [Fact]
    public async Task MigrationIncompleteKeepsLiveSuccessAndReadyFailureUntilMigratorRuns()
    {
        Assert.False(await MigrationHistoryExistsAsync());

        await using HealthApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using (HttpResponseMessage liveBefore = await client.GetAsync(HealthEndpoints.LivePath))
        using (HttpResponseMessage readyBefore = await client.GetAsync(HealthEndpoints.ReadyPath))
        {
            string readyBeforeBody = await readyBefore.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, liveBefore.StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, readyBefore.StatusCode);
            Assert.Equal("Unhealthy", readyBeforeBody);
            AssertNotBusinessErrorEnvelope(readyBeforeBody);
        }

        await ApplyMigrationsAsync();

        using HttpResponseMessage liveAfter = await client.GetAsync(HealthEndpoints.LivePath);
        using HttpResponseMessage readyAfter = await client.GetAsync(HealthEndpoints.ReadyPath);

        Assert.Equal(HttpStatusCode.OK, liveAfter.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readyAfter.StatusCode);
        Assert.Equal("Healthy", await readyAfter.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ReadyFailureDoesNotDiscloseSecretsOrExceptionDetails()
    {
        NpgsqlConnectionStringBuilder rejected = new(Database.ConnectionString)
        {
            Password = SecretPasswordSentinel,
            Host = "health-secret-host.invalid",
            Username = "health_secret_user",
        };

        await using HealthApiFactory factory = new(rejected.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage live = await client.GetAsync(HealthEndpoints.LivePath);
        using HttpResponseMessage ready = await client.GetAsync(HealthEndpoints.ReadyPath);
        string readyBody = await ready.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Unhealthy", readyBody);
        AssertNotBusinessErrorEnvelope(readyBody);

        Assert.DoesNotContain(SecretPasswordSentinel, readyBody, StringComparison.Ordinal);
        Assert.DoesNotContain(rejected.ConnectionString, readyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("health-secret-host.invalid", readyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("health_secret_user", readyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Npgsql", readyBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", readyBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stack", readyBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", readyBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection", readyBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthEndpointsDoNotMutateBusinessOrMigrationState()
    {
        await ApplyMigrationsAsync();
        string[] historyBefore = await ReadMigrationHistoryAsync();
        string[] tablesBefore = await ReadPublicTablesAsync();

        await using HealthApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        for (int attempt = 0; attempt < 3; attempt++)
        {
            using HttpResponseMessage live = await client.GetAsync(HealthEndpoints.LivePath);
            using HttpResponseMessage ready = await client.GetAsync(HealthEndpoints.ReadyPath);
            Assert.Equal(HttpStatusCode.OK, live.StatusCode);
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        }

        Assert.Equal(historyBefore, await ReadMigrationHistoryAsync());
        Assert.Equal(tablesBefore, await ReadPublicTablesAsync());
    }

    private async Task ApplyMigrationsAsync()
    {
        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, MigratorBudget);
        Assert.True(
            run.ExitCode == MigratorExitCode.Success,
            $"Migrator failed with exit code {run.ExitCode}. Output:\n{run.Output}");
    }

    private async Task<bool> MigrationHistoryExistsAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"SELECT to_regclass('{BankPersistence.MigrationsHistorySchema}.\"{BankPersistence.MigrationsHistoryTableName}\"') IS NOT NULL;",
            connection);
        return Assert.IsType<bool>(await command.ExecuteScalarAsync());
    }

    private Task<string[]> ReadMigrationHistoryAsync() =>
        ReadStringsAsync(
            $"""
             SELECT "MigrationId"
             FROM {BankPersistence.MigrationsHistorySchema}."{BankPersistence.MigrationsHistoryTableName}"
             ORDER BY "MigrationId";
             """);

    private Task<string[]> ReadPublicTablesAsync() =>
        ReadStringsAsync(
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
            ORDER BY table_name;
            """);

    private async Task<string[]> ReadStringsAsync(string commandText)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        List<string> values = [];
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return [.. values];
    }

    private static void AssertNotBusinessErrorEnvelope(string body)
    {
        Assert.DoesNotContain("internal_error", body, StringComparison.Ordinal);
        Assert.DoesNotContain("validation_failed", body, StringComparison.Ordinal);
        Assert.DoesNotContain("endpoint_not_found", body, StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(body) ||
            body is "Healthy" or "Unhealthy")
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        Assert.False(
            root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("code", out _) &&
            root.TryGetProperty("message", out _),
            "Health failure must not use the business ApiErrorEnvelope.");
    }
}

/// <summary>
/// Owns a dedicated PostgreSQL container so stop／start does not disturb other fixture users.
/// </summary>
[Trait("Category", "PostgreSqlIntegration")]
[Collection(HealthLifecycleTestGroup.Name)]
public sealed class HealthPostgresLifecycleTests :
    IClassFixture<PostgreSqlContainerFixture>,
    IAsyncLifetime
{
    private static readonly TimeSpan MigratorBudget = TimeSpan.FromSeconds(120);

    private readonly PostgreSqlContainerFixture fixture;
    private PostgreSqlTestDatabase? database;

    public HealthPostgresLifecycleTests(PostgreSqlContainerFixture fixture)
    {
        this.fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        database = await fixture.CreateDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        if (database is not null)
        {
            await database.DisposeAsync();
            database = null;
        }
    }

    [Fact]
    public async Task PostgresUnavailableKeepsLiveSuccessAndReadyFailureThenRecoveryRestoresReady()
    {
        PostgreSqlTestDatabase activeDatabase = database
            ?? throw new InvalidOperationException("Database was not initialized.");

        MigratorRun migration = await MigratorProcess.RunAsync(
            activeDatabase.ConnectionString,
            MigratorBudget);
        Assert.Equal(MigratorExitCode.Success, migration.ExitCode);

        NpgsqlConnectionStringBuilder apiConnection = new(activeDatabase.ConnectionString)
        {
            Timeout = 3,
            CommandTimeout = 3,
            Pooling = false,
        };

        await using HealthApiFactory factory = new(apiConnection.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using (HttpResponseMessage liveOk = await client.GetAsync(HealthEndpoints.LivePath))
        using (HttpResponseMessage readyOk = await client.GetAsync(HealthEndpoints.ReadyPath))
        {
            Assert.Equal(HttpStatusCode.OK, liveOk.StatusCode);
            Assert.Equal(HttpStatusCode.OK, readyOk.StatusCode);
        }

        PostgreSqlContainer container = fixture.Container;
        // Pause keeps the published host port stable so recovery can be observed without
        // restarting the API process (container Stop/Start remaps the host port on Docker Desktop).
        IDockerEndpointAuthenticationConfiguration dockerEndpoint =
            TestcontainersSettings.OS.DockerEndpointAuthConfig
            ?? throw new InvalidOperationException("Docker endpoint is required for this test.");

        using DockerClient docker = dockerEndpoint.GetDockerClientBuilder().Build();
        await docker.Containers.PauseContainerAsync(container.Id);

        try
        {
            using HttpResponseMessage liveWhileDown = await client.GetAsync(HealthEndpoints.LivePath);
            using HttpResponseMessage readyWhileDown = await client.GetAsync(HealthEndpoints.ReadyPath);
            string readyBody = await readyWhileDown.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, liveWhileDown.StatusCode);
            Assert.Equal("Healthy", await liveWhileDown.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.ServiceUnavailable, readyWhileDown.StatusCode);
            Assert.Equal("Unhealthy", readyBody);
            Assert.DoesNotContain("internal_error", readyBody, StringComparison.Ordinal);
            Assert.DoesNotContain("Npgsql", readyBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Exception", readyBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Stack", readyBody, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await docker.Containers.UnpauseContainerAsync(container.Id);
        }

        await WaitUntilReadySucceedsAsync(client);

        using HttpResponseMessage liveAfter = await client.GetAsync(HealthEndpoints.LivePath);
        using HttpResponseMessage readyAfter = await client.GetAsync(HealthEndpoints.ReadyPath);

        Assert.Equal(HttpStatusCode.OK, liveAfter.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readyAfter.StatusCode);
        Assert.Equal("Healthy", await readyAfter.Content.ReadAsStringAsync());
    }

    private static async Task WaitUntilReadySucceedsAsync(HttpClient client)
    {
        for (int attempt = 0; attempt < 60; attempt++)
        {
            using HttpResponseMessage ready = await client.GetAsync(HealthEndpoints.ReadyPath);
            if (ready.StatusCode == HttpStatusCode.OK)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new TimeoutException("Ready did not recover after PostgreSQL restart without API restart.");
    }
}

[CollectionDefinition(HealthLifecycleTestGroup.Name, DisableParallelization = true)]
public sealed class HealthLifecycleTestGroup
{
    public const string Name = "FND-06 health PostgreSQL lifecycle";
}

internal sealed class HealthApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(
            $"ConnectionStrings:{BankPersistence.ConnectionStringName}",
            connectionString);
    }
}
