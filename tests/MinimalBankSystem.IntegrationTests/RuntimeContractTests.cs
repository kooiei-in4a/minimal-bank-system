using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MinimalBankSystem.IntegrationTests.TestInfrastructure;

namespace MinimalBankSystem.IntegrationTests;

public sealed class RuntimeContractTests : IClassFixture<RuntimeContractTestHost>
{
    private readonly RuntimeContractTestHost _host;
    private readonly HttpClient _client;

    public RuntimeContractTests(RuntimeContractTestHost host)
    {
        _host = host;
        _host.Logs.Clear();
        _client = host.CreateClient();
    }

    [Fact]
    public async Task RuntimeEndpointUsesInjectedTimeProvider()
    {
        HttpResponseMessage response = await _client.GetAsync("/test/runtime");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        DateTimeOffset serverTimeUtc = DateTimeOffset.Parse(
            body.RootElement.GetProperty("serverTimeUtc").GetString()!,
            CultureInfo.InvariantCulture);

        Assert.Equal(FixedTimeProvider.FixedUtcNow, serverTimeUtc);
    }

    [Fact]
    public async Task ValidationErrorReturns400ValidationFailedEnvelope()
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/test/validate")
        {
            Content = JsonContent.Create(new { name = (string?)null }),
        };

        HttpResponseMessage response = await _client.SendAsync(request);
        string responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(responseBody);
        JsonElement root = body.RootElement;

        Assert.Equal(2, root.EnumerateObject().Count());
        Assert.Equal("validation_failed", root.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task ValidationErrorWithMissingBodyReturns400ValidationFailed()
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/test/validate")
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
        };

        HttpResponseMessage response = await _client.SendAsync(request);
        string responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(responseBody);
        Assert.Equal("validation_failed", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ValidRequestPassesThroughModelState()
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/test/validate")
        {
            Content = JsonContent.Create(new { name = "taro" }),
        };

        HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("taro", body.RootElement.GetProperty("name").GetString());
    }
}
