using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MinimalBankSystem.IntegrationTests.Fixtures;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace MinimalBankSystem.IntegrationTests.Api;

public sealed class TimeProviderTests
{
    [Fact]
    public async Task TimeProviderIsInjectedAndReturnsUtcNow()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero));

        using var server = TestApiServer.CreateWithCustomServices(services =>
        {
            services.AddSingleton<TimeProvider>(fakeTime);
        });

        using HttpResponseMessage response = await server.Client.GetAsync("/api/contract/time");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.NotNull(body);
        Assert.Equal(2025, body!["utcNow"].GetDateTimeOffset().Year);
        Assert.Equal(6, body["utcNow"].GetDateTimeOffset().Month);
        Assert.Equal(15, body["utcNow"].GetDateTimeOffset().Day);
    }

    [Fact]
    public async Task TimeProviderCanBeAdvanced()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));

        using var server = TestApiServer.CreateWithCustomServices(services =>
        {
            services.AddSingleton<TimeProvider>(fakeTime);
        });

        fakeTime.Advance(TimeSpan.FromHours(24));

        using HttpResponseMessage response = await server.Client.GetAsync("/api/contract/time");
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();

        Assert.Equal(2, body!["utcNow"].GetDateTimeOffset().Day);
    }

    [Fact]
    public async Task TimeProviderDoesNotUseSystemClock()
    {
        var fixedTime = new FakeTimeProvider(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));

        using var server = TestApiServer.CreateWithCustomServices(services =>
        {
            services.AddSingleton<TimeProvider>(fixedTime);
        });

        using HttpResponseMessage response = await server.Client.GetAsync("/api/contract/time");
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();

        var returned = body!["utcNow"].GetDateTimeOffset();
        Assert.Equal(2000, returned.Year);
        Assert.True(Math.Abs((returned - fixedTime.GetUtcNow()).TotalSeconds) < 1);
    }
}
