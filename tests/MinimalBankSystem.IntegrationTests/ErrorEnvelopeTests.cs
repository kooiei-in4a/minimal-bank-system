using System.Net;
using System.Text.Json;
using MinimalBankSystem.IntegrationTests.TestInfrastructure;

namespace MinimalBankSystem.IntegrationTests;

public sealed class ErrorEnvelopeTests : IClassFixture<RuntimeContractTestHost>
{
    private const string CorrelationIdHeaderName = "X-Correlation-Id";

    private readonly RuntimeContractTestHost _host;
    private readonly HttpClient _client;

    public ErrorEnvelopeTests(RuntimeContractTestHost host)
    {
        _host = host;
        _host.Logs.Clear();
        _client = host.CreateClient();
    }

    [Fact]
    public async Task UnmappedExceptionReturns500WithSpecEnvelope()
    {
        HttpResponseMessage response = await _client.GetAsync("/test/unmapped");
        string responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(responseBody);
        JsonElement root = body.RootElement;

        Assert.Equal(2, root.EnumerateObject().Count());
        Assert.Equal("internal_error", root.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task UnmappedExceptionDoesNotDiscloseInternalDetails()
    {
        HttpResponseMessage response = await _client.GetAsync("/test/unmapped");
        string responseBody = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Simulated unmapped exception", responseBody);
        Assert.DoesNotContain("InvalidOperationException", responseBody);
        Assert.DoesNotContain(" at ", responseBody);
        Assert.DoesNotContain("StackTrace", responseBody);
    }

    [Fact]
    public async Task UnmappedExceptionResponseCarriesCorrelationId()
    {
        HttpResponseMessage response = await _client.GetAsync("/test/unmapped");

        Assert.NotNull(response.Headers.GetValues(CorrelationIdHeaderName).SingleOrDefault());
    }

    [Fact]
    public async Task ApiExceptionMapsToFixedSpecCode()
    {
        HttpResponseMessage response = await _client.GetAsync("/test/conflict");
        string responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(responseBody);
        JsonElement root = body.RootElement;

        Assert.Equal(2, root.EnumerateObject().Count());
        Assert.Equal("concurrent_operation_conflict", root.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("message").GetString()));
    }
}
