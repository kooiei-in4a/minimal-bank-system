using System.Text.Json;
using MinimalBankSystem.IntegrationTests.TestInfrastructure;

namespace MinimalBankSystem.IntegrationTests;

public sealed class CorrelationIdTests : IClassFixture<RuntimeContractTestHost>
{
    private const string CorrelationIdHeaderName = "X-Correlation-Id";

    private readonly RuntimeContractTestHost _host;
    private readonly HttpClient _client;

    public CorrelationIdTests(RuntimeContractTestHost host)
    {
        _host = host;
        _host.Logs.Clear();
        _client = host.CreateClient();
    }

    [Fact]
    public async Task WithoutHeaderCorrelationIdIsGeneratedEchoedAndLogged()
    {
        HttpResponseMessage response = await _client.GetAsync("/test/unmapped");
        string correlationId = Assert.Single(response.Headers.GetValues(CorrelationIdHeaderName));

        Assert.Matches("^[0-9a-f]{32}$", correlationId);
        Assert.Contains(
            _host.Logs.Snapshot(),
            line => line.Json.Contains($"\"CorrelationId\":\"{correlationId}\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidCallerSuppliedCorrelationIdIsHonored()
    {
        const string callerSupplied = "caller-trace-0001";
        using HttpRequestMessage request = new(HttpMethod.Get, "/test/runtime");
        request.Headers.Add(CorrelationIdHeaderName, callerSupplied);

        HttpResponseMessage response = await _client.SendAsync(request);
        string correlationId = Assert.Single(response.Headers.GetValues(CorrelationIdHeaderName));

        Assert.Equal(callerSupplied, correlationId);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(callerSupplied, body.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task InvalidCallerSuppliedCorrelationIdIsNotTrusted()
    {
        const string unsafeValue = "bad<value>injection";
        using HttpRequestMessage request = new(HttpMethod.Get, "/test/unmapped");
        request.Headers.Add(CorrelationIdHeaderName, unsafeValue);

        HttpResponseMessage response = await _client.SendAsync(request);
        string correlationId = Assert.Single(response.Headers.GetValues(CorrelationIdHeaderName));

        Assert.Matches("^[0-9a-f]{32}$", correlationId);
        Assert.NotEqual(unsafeValue, correlationId);
        Assert.All(
            _host.Logs.Snapshot(),
            line => Assert.DoesNotContain(unsafeValue, line.Json, StringComparison.Ordinal));
    }

    [Fact]
    public async Task NonAsciiCallerSuppliedCorrelationIdIsNotTrusted()
    {
        const string unsafeValue = "値-injection";
        using HttpRequestMessage request = new(HttpMethod.Get, "/test/unmapped");
        request.Headers.Add(CorrelationIdHeaderName, unsafeValue);

        HttpResponseMessage response = await _client.SendAsync(request);
        string correlationId = Assert.Single(response.Headers.GetValues(CorrelationIdHeaderName));

        Assert.Matches("^[0-9a-f]{32}$", correlationId);
        Assert.NotEqual(unsafeValue, correlationId);
    }

    [Fact]
    public async Task OversizedCallerSuppliedCorrelationIdIsNotTrusted()
    {
        string oversized = new('a', 65);
        using HttpRequestMessage request = new(HttpMethod.Get, "/test/runtime");
        request.Headers.Add(CorrelationIdHeaderName, oversized);

        HttpResponseMessage response = await _client.SendAsync(request);
        string correlationId = Assert.Single(response.Headers.GetValues(CorrelationIdHeaderName));

        Assert.Matches("^[0-9a-f]{32}$", correlationId);
        Assert.NotEqual(oversized, correlationId);
    }
}
