using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace MinimalBankSystem.IntegrationTests.ApiRuntime;

/// <summary>
/// Verifies that application code reads time from the injected <see cref="TimeProvider"/> required
/// by ADR-0006 instead of the system clock.
/// </summary>
public sealed class TimeProviderContractTests
{
    [Fact]
    public async Task ApplicationCodeReadsTimeFromTheInjectedTimeProvider()
    {
        DateTimeOffset instant = new(2026, 8, 8, 1, 2, 3, TimeSpan.Zero);

        await using ApiRuntimeTestServer server = await ApiRuntimeTestServer.StartAsync(
            services => services.AddSingleton<TimeProvider>(new FixedTimeProvider(instant)));

        using HttpResponseMessage first = await server.Client.GetAsync(TimeRoute);
        using HttpResponseMessage second = await server.Client.GetAsync(TimeRoute);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(instant, await ReadUtcNowAsync(first));
        Assert.Equal(instant, await ReadUtcNowAsync(second));
    }

    [Fact]
    public async Task ApplicationCodeReadsRealTimeWithoutASubstitutedProvider()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow.AddMinutes(-1);

        await using ApiRuntimeTestServer server = await ApiRuntimeTestServer.StartAsync();

        using HttpResponseMessage response = await server.Client.GetAsync(TimeRoute);

        DateTimeOffset observed = await ReadUtcNowAsync(response);

        Assert.InRange(observed, before, DateTimeOffset.UtcNow.AddMinutes(1));
    }

    private static string TimeRoute => $"/{RuntimeContractTestController.RouteBase}/time";

    private static async Task<DateTimeOffset> ReadUtcNowAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("utcNow").GetDateTimeOffset();
    }
}
