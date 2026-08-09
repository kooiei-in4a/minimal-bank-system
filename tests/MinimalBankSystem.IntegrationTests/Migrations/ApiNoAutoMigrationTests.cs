using System.Net;
using System.Net.Sockets;
using MinimalBankSystem.IntegrationTests.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.Migrations;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class ApiNoAutoMigrationTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task ApiStartupDoesNotCreateOrModifySchema()
    {
        Assert.Empty(await PostgreSqlProbe.GetAppliedMigrationIdsAsync(Database.ConnectionString));
        Assert.Empty(await PostgreSqlProbe.GetUserTablesAsync(Database.ConnectionString));

        int port = ReserveFreePort();

        await using ApiProcess api = await ApiProcess.StartAsync(Database.ConnectionString, port);

        using HttpResponseMessage response = await api.GetAsync("/");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await api.DisposeAsync();

        Assert.Empty(await PostgreSqlProbe.GetAppliedMigrationIdsAsync(Database.ConnectionString));
        Assert.Empty(await PostgreSqlProbe.GetUserTablesAsync(Database.ConnectionString));
        Assert.Empty(await PostgreSqlProbe.GetUserSequencesAsync(Database.ConnectionString));
        Assert.Empty(await PostgreSqlProbe.GetUserTriggersAsync(Database.ConnectionString));
    }

    private static int ReserveFreePort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
