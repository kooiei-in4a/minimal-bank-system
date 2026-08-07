using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MinimalBankSystem.IntegrationTests.Fixtures;
using Xunit;

namespace MinimalBankSystem.IntegrationTests.Api;

public sealed class CorrelationIdTests : IClassFixture<TestApiServer>
{
    private readonly TestApiServer _server;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public CorrelationIdTests(TestApiServer server)
    {
        _server = server;
    }

    [Fact]
    public async Task RequestWithoutCorrelationIdGeneratesOne()
    {
        using HttpResponseMessage response = await _server.Client.GetAsync("/api/contract/echo");

        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        string? correlationId = response.Headers.GetValues("X-Correlation-Id").FirstOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
    }

    [Fact]
    public async Task RequestWithCorrelationIdEchoesSameValue()
    {
        const string expected = "test-correlation-id-12345";

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/contract/echo");
        request.Headers.Add("X-Correlation-Id", expected);

        using HttpResponseMessage response = await _server.Client.SendAsync(request);

        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        string? actual = response.Headers.GetValues("X-Correlation-Id").FirstOrDefault();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task RequestWithCorrelationIdReturnsInBody()
    {
        const string expected = "body-test-id";

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/contract/echo");
        request.Headers.Add("X-Correlation-Id", expected);

        using HttpResponseMessage response = await _server.Client.SendAsync(request);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string?>>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(expected, body!["correlationId"]);
    }

    [Fact]
    public async Task RequestWithEmptyCorrelationIdGeneratesNew()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/contract/echo");
        request.Headers.Add("X-Correlation-Id", "");

        using HttpResponseMessage response = await _server.Client.SendAsync(request);

        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        string? correlationId = response.Headers.GetValues("X-Correlation-Id").FirstOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
        Assert.NotEqual("", correlationId);
    }

    [Fact]
    public async Task RequestWithTooLongCorrelationIdGeneratesNew()
    {
        string tooLong = new string('x', 129);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/contract/echo");
        request.Headers.Add("X-Correlation-Id", tooLong);

        using HttpResponseMessage response = await _server.Client.SendAsync(request);

        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        string? actual = response.Headers.GetValues("X-Correlation-Id").FirstOrDefault();
        Assert.NotEqual(tooLong, actual);
    }

    [Fact]
    public async Task RequestWithMaxLengthCorrelationIdAcceptsIt()
    {
        string maxLength = new string('a', 128);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/contract/echo");
        request.Headers.Add("X-Correlation-Id", maxLength);

        using HttpResponseMessage response = await _server.Client.SendAsync(request);

        string? actual = response.Headers.GetValues("X-Correlation-Id").FirstOrDefault();
        Assert.Equal(maxLength, actual);
    }

    [Fact]
    public async Task DifferentRequestsGetDifferentCorrelationIds()
    {
        using HttpResponseMessage response1 = await _server.Client.GetAsync("/api/contract/echo");
        using HttpResponseMessage response2 = await _server.Client.GetAsync("/api/contract/echo");

        string? id1 = response1.Headers.GetValues("X-Correlation-Id").FirstOrDefault();
        string? id2 = response2.Headers.GetValues("X-Correlation-Id").FirstOrDefault();

        Assert.NotEqual(id1, id2);
    }
}
