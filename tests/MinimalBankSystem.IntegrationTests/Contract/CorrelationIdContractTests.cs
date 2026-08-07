using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using MinimalBankSystem.Application.Time;
using MinimalBankSystem.IntegrationTests.Infrastructure;
using Xunit;

namespace MinimalBankSystem.IntegrationTests.Contract;

public sealed class CorrelationIdContractTests
{
    [Fact]
    public async Task RequestWithoutHeaderGeneratesNew()
    {
        using IHost host = TestHostFactory.Build();
        using HttpClient client = host.GetTestClient();

        using HttpResponseMessage response = await client.GetAsync("/_contract/echo");

        Assert.True(response.Headers.TryGetValues(TimeProviderKeys.CorrelationIdHeader, out var values));
        string id = Assert.Single(values);
        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.Matches(@"^[0-9a-f]{32}$", id);
    }

    [Fact]
    public async Task RequestWithSafeHeaderEchoesBackAndPropagates()
    {
        const string supplied = "abc.123_def-456";

        using IHost host = TestHostFactory.Build();
        using HttpClient client = host.GetTestClient();

        using HttpRequestMessage request = new(HttpMethod.Get, "/_contract/echo");
        request.Headers.Add(TimeProviderKeys.CorrelationIdHeader, supplied);
        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues(TimeProviderKeys.CorrelationIdHeader, out var echo));
        Assert.Equal(supplied, Assert.Single(echo));

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(supplied, doc.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task RequestWithUnsafeHeaderReplacedWithGenerated()
    {
        string malicious = "abc\r\nSet-Cookie: evil=1";

        using IHost host = TestHostFactory.Build();
        using HttpClient client = host.GetTestClient();

        using HttpRequestMessage request = new(HttpMethod.Get, "/_contract/echo");
        request.Headers.TryAddWithoutValidation(TimeProviderKeys.CorrelationIdHeader, malicious);
        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues(TimeProviderKeys.CorrelationIdHeader, out var values));
        string id = Assert.Single(values);
        Assert.NotEqual(malicious, id);
        Assert.Matches(@"^[0-9a-f]{32}$", id);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task RequestWithTooLongHeaderReplaced()
    {
        string tooLong = new('a', 200);

        using IHost host = TestHostFactory.Build();
        using HttpClient client = host.GetTestClient();

        using HttpRequestMessage request = new(HttpMethod.Get, "/_contract/echo");
        request.Headers.TryAddWithoutValidation(TimeProviderKeys.CorrelationIdHeader, tooLong);
        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues(TimeProviderKeys.CorrelationIdHeader, out var values));
        Assert.NotEqual(tooLong, Assert.Single(values));
    }
}
