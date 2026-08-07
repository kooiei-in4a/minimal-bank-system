using System.Net;
using System.Text.Json;

namespace MinimalBankSystem.IntegrationTests.Api;

public sealed class TimeProviderContractTests : IClassFixture<ApiContractWebApplicationFactory>
{
    private readonly ApiContractWebApplicationFactory _factory;

    public TimeProviderContractTests(ApiContractWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ApplicationUsesInjectedTimeProvider()
    {
        DateTimeOffset expected = new(2026, 8, 8, 12, 30, 0, TimeSpan.Zero);
        _factory.FakeTimeProvider.SetUtcNow(expected);

        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/__contract__/time");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(body);
        DateTimeOffset actual = document.RootElement.GetProperty("utc").GetDateTimeOffset();

        Assert.Equal(expected, actual);
    }
}
