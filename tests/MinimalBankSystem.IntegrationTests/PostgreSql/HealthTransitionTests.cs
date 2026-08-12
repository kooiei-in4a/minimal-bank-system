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
/// A real PostgreSQL stop/start test. One API factory remains alive for the entire outage and
/// recovery, so readiness cannot be accidentally proved by restarting the API.
/// </summary>
[Trait("Category", "PostgreSqlIntegration")]
public sealed class HealthTransitionTests(RestartablePostgreSqlFixture fixture)
    : IClassFixture<RestartablePostgreSqlFixture>
{
    private static readonly TimeSpan MigrationBudget = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan RecoveryBudget = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task LivenessSurvivesRealPostgreSqlStopAndReadinessRecoversWithoutApiRestart()
    {
        MigratorRun migration = await MigratorProcess.RunAsync(fixture.ConnectionString, MigrationBudget);
        Assert.Equal(MigratorExitCode.Success, migration.ExitCode);

        await using HealthApiFactory factory = new(fixture.ConnectionString);
        using HttpClient client = factory.CreateClient();

        await AssertReadyAsync(client);
        await fixture.StopAsync();
        Assert.False(await fixture.AcceptsConnectionsAsync());

        await AssertLiveAsync(client);
        await AssertNotReadyAsync(client);

        await fixture.StartAsync();
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
        long started = Environment.TickCount64;
        while (Environment.TickCount64 - started < (long)RecoveryBudget.TotalMilliseconds)
        {
            using HttpResponseMessage response = await client.GetAsync(HealthContract.ReadyPath);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                await HealthContractTests.AssertReadyAsync(response);
                return;
            }

            await Task.Delay(ProbeInterval);
        }

        Assert.Fail($"Readiness did not recover within {RecoveryBudget}.");
    }
}

public sealed class RestartablePostgreSqlFixture : IAsyncLifetime
{
    private const int PostgreSqlPort = 5432;
    private PostgreSqlContainer? container;
    private int hostPort;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        hostPort = ReserveHostPort();
        container = new PostgreSqlBuilder(PostgreSqlContainerFixture.ImageReference)
            .WithDatabase("mbs_health_transition")
            .WithUsername("postgres")
            .WithPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)))
            .WithPortBinding(hostPort, PostgreSqlPort)
            .Build();

        await container.StartAsync();
        ConnectionString = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Host = "127.0.0.1",
            Port = hostPort,
            Pooling = false,
            Timeout = 5,
            CommandTimeout = 10,
        }.ConnectionString;
    }

    public Task StopAsync() => RequireContainer().StopAsync();

    public Task StartAsync() => RequireContainer().StartAsync();

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
        if (container is not null)
        {
            await container.DisposeAsync();
            container = null;
        }
    }

    private PostgreSqlContainer RequireContainer() =>
        container ?? throw new InvalidOperationException("The restartable PostgreSQL fixture is not initialized.");

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
