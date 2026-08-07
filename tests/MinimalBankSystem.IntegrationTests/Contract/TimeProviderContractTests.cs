using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MinimalBankSystem.IntegrationTests.Infrastructure;
using Xunit;

namespace MinimalBankSystem.IntegrationTests.Contract;

public sealed class TimeProviderContractTests
{
    [Fact]
    public async Task EndpointUsesInjectedFakeTimeProvider()
    {
        DateTimeOffset fixedNow = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var fake = new FakeTimeProvider(fixedNow);

        using IHost host = TestHostFactory.Build(services =>
        {
            services.AddSingleton<TimeProvider>(fake);
        });
        using HttpClient client = host.GetTestClient();

        using HttpResponseMessage response = await client.GetAsync("/_contract/time");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        DateTimeOffset returned = doc.RootElement.GetProperty("utcNow").GetDateTimeOffset();
        Assert.Equal(fixedNow, returned);
    }

    [Fact]
    public async Task EndpointAdvancingFakeClockReflectsInResponse()
    {
        var fake = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));

        using IHost host = TestHostFactory.Build(services =>
        {
            services.AddSingleton<TimeProvider>(fake);
        });
        using HttpClient client = host.GetTestClient();

        fake.Advance(TimeSpan.FromHours(2));

        using HttpResponseMessage response = await client.GetAsync("/_contract/time");
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        DateTimeOffset returned = doc.RootElement.GetProperty("utcNow").GetDateTimeOffset();
        Assert.Equal(new DateTimeOffset(2025, 1, 1, 2, 0, 0, TimeSpan.Zero), returned);
    }
}
