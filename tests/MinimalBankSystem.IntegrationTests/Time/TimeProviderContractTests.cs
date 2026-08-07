using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MinimalBankSystem.IntegrationTests.TestOnly;

namespace MinimalBankSystem.IntegrationTests.Time;

public sealed class TimeProviderContractTests
{
    [Fact]
    public async Task EchoReturnsTimeFromInjectedTimeProviderNotSystemClock()
    {
        DateTimeOffset fixedTime = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

        await using ContractTestWebApplicationFactory baseFactory = new();
        await using Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory =
            baseFactory.WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(new ManualTimeProvider(fixedTime));
                }));

        using HttpClient client = factory.CreateClient();

        EchoResponse? body = await client.GetFromJsonAsync<EchoResponse>("/__contract-test/echo");

        Assert.NotNull(body);
        Assert.Equal(fixedTime, body!.CurrentTime);
    }

    private sealed record EchoResponse(DateTimeOffset CurrentTime);
}
