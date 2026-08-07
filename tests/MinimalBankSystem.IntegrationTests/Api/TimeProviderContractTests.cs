using System.Text.Json;

namespace MinimalBankSystem.IntegrationTests.Api;

public sealed class TimeProviderContractTests
{
    [Fact]
    public async Task ApplicationCodeUsesInjectedTimeProvider()
    {
        DateTimeOffset fixedUtc = new(2024, 6, 15, 12, 30, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(fixedUtc);

        await using ApiContractWebApplicationFactory factory = new()
        {
            TimeProviderOverride = timeProvider,
        };
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/__contract__/utc-now");
        Assert.True(response.IsSuccessStatusCode);

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        DateTimeOffset utcNow = document.RootElement.GetProperty("utcNow").GetDateTimeOffset();
        Assert.Equal(fixedUtc, utcNow);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
