using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MinimalBankSystem.IntegrationTests.TestInfrastructure;

namespace MinimalBankSystem.IntegrationTests;

public sealed class TechnicalLoggingTests : IClassFixture<RuntimeContractTestHost>
{
    private const string CorrelationIdHeaderName = "X-Correlation-Id";

    private readonly RuntimeContractTestHost _host;
    private readonly HttpClient _client;

    public TechnicalLoggingTests(RuntimeContractTestHost host)
    {
        _host = host;
        _host.Logs.Clear();
        _client = host.CreateClient();
    }

    [Fact]
    public async Task ErrorLogLineIsParsableJsonWithCorrelationIdAndFixedCode()
    {
        HttpResponseMessage response = await _client.GetAsync("/test/unmapped");
        string correlationId = Assert.Single(response.Headers.GetValues(CorrelationIdHeaderName));

        CapturedLogLine errorLine = Assert.Single(
            _host.Logs.Snapshot(),
            line => line.LogLevel == LogLevel.Error);

        using JsonDocument log = JsonDocument.Parse(errorLine.Json);
        JsonElement root = log.RootElement;

        Assert.Equal("Error", root.GetProperty("Level").GetString());
        Assert.Equal(correlationId, root.GetProperty("Scopes").GetProperty("CorrelationId").GetString());
        Assert.Equal("internal_error", root.GetProperty("ErrorCode").GetString());
        Assert.Contains("HTTP 500", root.GetProperty("Message").GetString());
    }

    [Fact]
    public async Task ErrorLogLineForApiExceptionContainsMappedCode()
    {
        await _client.GetAsync("/test/conflict");

        CapturedLogLine errorLine = Assert.Single(
            _host.Logs.Snapshot(),
            line => line.LogLevel == LogLevel.Error);

        using JsonDocument log = JsonDocument.Parse(errorLine.Json);
        Assert.Equal("concurrent_operation_conflict", log.RootElement.GetProperty("ErrorCode").GetString());
    }

    [Fact]
    public async Task ProhibitedFieldSentinelsAreNotWrittenToTechnicalLogs()
    {
        const string password = "sentinel-password-7f3a";
        const string jwt = "sentinel-jwt-9b1c";
        const string signingKey = "sentinel-signing-key-2d4e";
        const string idempotencyKey = "sentinel-idempotency-key-5a6b";
        const string connectionString = "sentinel-connection-string-8c9d";

        using HttpRequestMessage request = new(HttpMethod.Get, $"/test/unmapped?password={password}");
        request.Headers.Add("Authorization", $"Bearer {jwt}");
        request.Headers.Add("X-Signing-Key", signingKey);
        request.Headers.Add("X-Idempotency-Key", idempotencyKey);
        request.Headers.Add("X-Connection-String", connectionString);

        HttpResponseMessage response = await _client.SendAsync(request);
        string responseBody = await response.Content.ReadAsStringAsync();

        string[] sentinels = [password, jwt, signingKey, idempotencyKey, connectionString];

        Assert.DoesNotContain(sentinels, sentinel => responseBody.Contains(sentinel, StringComparison.Ordinal));
        Assert.All(
            _host.Logs.Snapshot(),
            line => Assert.DoesNotContain(sentinels, sentinel => line.Json.Contains(sentinel, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ProhibitedBodySentinelIsNotWrittenToTechnicalLogs()
    {
        const string password = "sentinel-body-password-c3d8";

        HttpRequestMessage request = new(HttpMethod.Post, "/test/validate")
        {
            Content = new StringContent($"{{\"name\":\"{password}\"}}", Encoding.UTF8, "application/json"),
        };

        await _client.SendAsync(request);

        Assert.All(
            _host.Logs.Snapshot(),
            line => Assert.DoesNotContain(password, line.Json, StringComparison.Ordinal));
    }
}
