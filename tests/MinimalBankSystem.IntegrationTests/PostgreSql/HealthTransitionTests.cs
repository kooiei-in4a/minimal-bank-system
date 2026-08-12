using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// FND-06 transition evidence against a real PostgreSQL server that is actually stopped and
/// started again. Liveness must survive the outage, readiness must fail during it, and the same
/// API process must recover without being restarted.
/// </summary>
[Trait("Category", "PostgreSqlIntegration")]
public sealed class HealthTransitionTests(RestartablePostgreSqlFixture fixture)
    : IClassFixture<RestartablePostgreSqlFixture>
{
    private static readonly TimeSpan MigrationBudget = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan RecoveryBudget = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task LivenessSurvivesARealPostgreSqlOutageAndReadinessRecoversWithoutAnApiRestart()
    {
        MigratorRun migration = await MigratorProcess.RunAsync(
            fixture.ConnectionString,
            MigrationBudget);
        Assert.Equal(MigratorExitCode.Success, migration.ExitCode);

        await using HealthApiFactory factory = new(fixture.ConnectionString);
        using HttpClient client = factory.CreateClient();

        await AssertLiveAsync(client);
        await AssertReadyAsync(client);

        await fixture.StopServerAsync();
        Assert.False(await fixture.AcceptsConnectionsAsync());

        // AC-03: the API process is untouched by the dependency outage.
        await AssertLiveAsync(client);

        // AC-04: readiness must fail while PostgreSQL is stopped.
        await AssertNotReadyAsync(client);

        await fixture.StartServerAsync();

        // AC-05: the same API instance returns to ready. No restart is part of the contract.
        await WaitForReadyAsync(client);
        await AssertLiveAsync(client);
    }

    private static async Task AssertLiveAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync(HealthContract.LivePath);
        await HealthContractTests.AssertLiveAsync(response);
    }

    private static async Task AssertReadyAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync(HealthContract.ReadyPath);
        await HealthContractTests.AssertReadyAsync(response);
    }

    private static async Task AssertNotReadyAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync(HealthContract.ReadyPath);
        await HealthContractTests.AssertNotReadyAsync(response);
    }

    private static async Task WaitForReadyAsync(HttpClient client)
    {
        long startedAt = Environment.TickCount64;
        HttpStatusCode lastStatusCode = HttpStatusCode.ServiceUnavailable;

        while (Environment.TickCount64 - startedAt < (long)RecoveryBudget.TotalMilliseconds)
        {
            using (HttpResponseMessage response = await client.GetAsync(HealthContract.ReadyPath))
            {
                lastStatusCode = response.StatusCode;

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    await HealthContractTests.AssertReadyAsync(response);
                    return;
                }
            }

            await Task.Delay(ProbeInterval);
        }

        Assert.Fail(
            $"Readiness did not recover within {RecoveryBudget}. Last status: {lastStatusCode}.");
    }
}

/// <summary>
/// A digest-pinned PostgreSQL container published on a reserved host port so the destination
/// survives a real container stop and start. The API therefore keeps one fixed connection string
/// across the whole outage.
/// </summary>
public sealed class RestartablePostgreSqlFixture : IAsyncLifetime
{
    private const string DatabaseName = "mbs_health_transition";
    private const string Username = "postgres";
    private const int ServerPort = 5432;

    internal const string ContainerOwnershipLabel =
        "com.in4a.minimal-bank-system.health-transition-fixture";

    private PostgreSqlContainer? container;
    private int hostPort;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        hostPort = ReserveHostPort();

        PostgreSqlContainer candidate = new PostgreSqlBuilder(PostgreSqlContainerFixture.ImageReference)
            .WithDatabase(DatabaseName)
            .WithUsername(Username)
            .WithPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)))
            .WithPortBinding(hostPort, ServerPort)
            .WithLabel(ContainerOwnershipLabel, Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture))
            .Build();
        container = candidate;

        try
        {
            await candidate.StartAsync();
        }
        catch (Exception startupException)
        {
            throw new InvalidOperationException(
                $"Failed to start the restartable PostgreSQL container on host port {hostPort} " +
                $"using '{PostgreSqlContainerFixture.ImageReference}'. FND-06 transition tests " +
                "require Docker and never fall back to another provider.",
                startupException);
        }

        ConnectionString = new NpgsqlConnectionStringBuilder(candidate.GetConnectionString())
        {
            Host = "127.0.0.1",
            Port = hostPort,
            Database = DatabaseName,
            Pooling = false,
            Timeout = 5,
            CommandTimeout = 10,
        }.ConnectionString;
    }

    public Task StopServerAsync() => Container.StopAsync();

    public Task StartServerAsync() => Container.StartAsync();

    /// <summary>Independent transport-level observation of the published PostgreSQL port.</summary>
    public async Task<bool> AcceptsConnectionsAsync()
    {
        using TcpClient probe = new();

        try
        {
            await probe.ConnectAsync(IPAddress.Loopback, hostPort);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    public async Task DisposeAsync()
    {
        if (container is null)
        {
            return;
        }

        await container.DisposeAsync();
        container = null;
    }

    private PostgreSqlContainer Container =>
        container ?? throw new InvalidOperationException(
            "The restartable PostgreSQL container has not been initialized.");

    private static int ReserveHostPort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
