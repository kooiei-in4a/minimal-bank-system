using System.Net;
using Docker.DotNet;
using DotNet.Testcontainers.Configurations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>Real PostgreSQL verification for the FND-06 live and ready contract.</summary>
[Trait("Category", "PostgreSqlIntegration")]
public sealed class HealthContractTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly TimeSpan MigratorBudget = TimeSpan.FromSeconds(120);

    [Fact]
    public async Task CleanDatabaseIsLiveButNotReadyUntilCanonicalMigrationIsApplied()
    {
        Assert.Empty(await ReadPublicTablesAsync());

        await using HealthApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        await AssertHealthAsync(client, "/health/live", HttpStatusCode.OK, "Healthy");
        await AssertHealthAsync(
            client,
            "/health/ready",
            HttpStatusCode.ServiceUnavailable,
            "Unhealthy");

        Assert.Empty(await ReadPublicTablesAsync());

        MigratorRun migration = await MigratorProcess.RunAsync(Database.ConnectionString, MigratorBudget);
        Assert.Equal(MigratorExitCode.Success, migration.ExitCode);

        string[] schemaAfterMigration = await ReadPublicTablesAsync();
        string[] historyAfterMigration = await ReadMigrationHistoryAsync();
        Assert.Equal([BankPersistence.MigrationsHistoryTableName], schemaAfterMigration);
        Assert.Single(historyAfterMigration);

        await AssertHealthAsync(client, "/health/live", HttpStatusCode.OK, "Healthy");
        await AssertHealthAsync(client, "/health/ready", HttpStatusCode.OK, "Healthy");

        Assert.Equal(schemaAfterMigration, await ReadPublicTablesAsync());
        Assert.Equal(historyAfterMigration, await ReadMigrationHistoryAsync());
    }

    [Fact]
    public async Task PostgreSqlUnavailabilityOnlyFailsReadyAndReadyRecoversWithoutApiRestart()
    {
        MigratorRun migration = await MigratorProcess.RunAsync(Database.ConnectionString, MigratorBudget);
        Assert.Equal(MigratorExitCode.Success, migration.ExitCode);

        await using HealthApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(20);

        await AssertHealthAsync(client, "/health/live", HttpStatusCode.OK, "Healthy");
        await AssertHealthAsync(client, "/health/ready", HttpStatusCode.OK, "Healthy");

        bool postgresPaused = false;
        string postgresContainerId = Fixture.Container.Id;
        using DockerClient docker = CreateDockerClient();

        try
        {
            await docker.Containers.PauseContainerAsync(postgresContainerId);
            postgresPaused = true;

            await AssertHealthAsync(client, "/health/live", HttpStatusCode.OK, "Healthy");
            string failureBody = await AssertHealthAsync(
                client,
                "/health/ready",
                HttpStatusCode.ServiceUnavailable,
                "Unhealthy");
            AssertSanitizedOperationalFailure(failureBody, Database.ConnectionString);

            await docker.Containers.UnpauseContainerAsync(postgresContainerId);
            postgresPaused = false;

            await WaitForReadyAsync(client);
            await AssertHealthAsync(client, "/health/live", HttpStatusCode.OK, "Healthy");
        }
        finally
        {
            if (postgresPaused)
            {
                await docker.Containers.UnpauseContainerAsync(postgresContainerId);
            }
        }
    }

    private static DockerClient CreateDockerClient()
    {
        IDockerEndpointAuthenticationConfiguration dockerEndpoint =
            TestcontainersSettings.OS.DockerEndpointAuthConfig
            ?? throw new InvalidOperationException(
                "Testcontainers could not resolve a Docker endpoint for the outage test.");

        return dockerEndpoint.GetDockerClientBuilder().Build();
    }

    private static async Task<string> AssertHealthAsync(
        HttpClient client,
        string path,
        HttpStatusCode expectedStatus,
        string expectedBody)
    {
        using HttpResponseMessage response = await client.GetAsync(path);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(expectedBody, body);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);

        return body;
    }

    private static void AssertSanitizedOperationalFailure(string body, string connectionString)
    {
        NpgsqlConnectionStringBuilder connection = new(connectionString);

        Assert.DoesNotContain(connectionString, body, StringComparison.Ordinal);
        AssertNotExposed(connection.Password, body);
        AssertNotExposed(connection.Host, body);
        Assert.DoesNotContain("credential", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ApiErrorEnvelope", body, StringComparison.Ordinal);
        Assert.DoesNotContain("internal_error", body, StringComparison.Ordinal);
        Assert.DoesNotContain("validation_failed", body, StringComparison.Ordinal);
    }

    private static void AssertNotExposed(string? sensitiveValue, string body)
    {
        if (!string.IsNullOrEmpty(sensitiveValue))
        {
            Assert.DoesNotContain(sensitiveValue, body, StringComparison.Ordinal);
        }
    }

    private static async Task WaitForReadyAsync(HttpClient client)
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            using HttpResponseMessage response = await client.GetAsync("/health/ready");

            if (response.StatusCode == HttpStatusCode.OK)
            {
                Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Assert.Fail("Readiness did not recover after PostgreSQL restarted.");
    }

    private Task<string[]> ReadPublicTablesAsync() =>
        ReadStringsAsync(
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
            ORDER BY table_name;
            """);

    private Task<string[]> ReadMigrationHistoryAsync() =>
        ReadStringsAsync(
            $"""
             SELECT "MigrationId"
             FROM {BankPersistence.MigrationsHistorySchema}."{BankPersistence.MigrationsHistoryTableName}"
             ORDER BY "MigrationId";
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

    private sealed class HealthApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting(
                $"ConnectionStrings:{BankPersistence.ConnectionStringName}",
                connectionString);
        }
    }
}
