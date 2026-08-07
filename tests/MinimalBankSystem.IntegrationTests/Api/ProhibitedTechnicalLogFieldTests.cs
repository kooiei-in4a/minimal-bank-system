using System.Text;
using System.Text.Json;
using MinimalBankSystem.Api.Runtime;

namespace MinimalBankSystem.IntegrationTests.Api;

[Collection(ConsoleLogCaptureGroup.Name)]
public sealed class ProhibitedTechnicalLogFieldTests
{
    // Explicitly fake sentinels — not real credentials.
    private const string SentinelPassword = "SENTINEL_PASSWORD_VALUE";
    private const string SentinelJwt = "SENTINEL_JWT_VALUE";
    private const string SentinelSigningKey = "SENTINEL_SIGNING_KEY_VALUE";
    private const string SentinelIdempotencyKey = "SENTINEL_IDEMPOTENCY_KEY_VALUE";
    private const string SentinelConnectionString = "SENTINEL_CONNECTION_STRING_VALUE";

    [Fact]
    public async Task TechnicalLogsDoNotContainProhibitedSensitiveSentinels()
    {
        using var console = new ConsoleOutputCapture();
        string collectedLogs;
        string consoleOutput;

        await using (ApiContractWebApplicationFactory factory = new())
        {
            factory.LoggerProvider.Clear();
            HttpClient client = factory.CreateClient();

            using var request = new HttpRequestMessage(HttpMethod.Post, "/__contract__/safe-log");
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {SentinelJwt}");
            request.Headers.TryAddWithoutValidation("Idempotency-Key", SentinelIdempotencyKey);
            request.Headers.TryAddWithoutValidation("X-Signing-Key", SentinelSigningKey);
            request.Headers.TryAddWithoutValidation("X-Connection-String", SentinelConnectionString);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { password = SentinelPassword, connectionString = SentinelConnectionString }),
                Encoding.UTF8,
                "application/json");

            HttpResponseMessage response = await client.SendAsync(request);
            Assert.True(response.IsSuccessStatusCode);
            collectedLogs = factory.LoggerProvider.CombinedText;
        }

        consoleOutput = console.GetOutput();
        string combined = collectedLogs + Environment.NewLine + consoleOutput;

        Assert.DoesNotContain(SentinelPassword, combined, StringComparison.Ordinal);
        Assert.DoesNotContain(SentinelJwt, combined, StringComparison.Ordinal);
        Assert.DoesNotContain(SentinelSigningKey, combined, StringComparison.Ordinal);
        Assert.DoesNotContain(SentinelIdempotencyKey, combined, StringComparison.Ordinal);
        Assert.DoesNotContain(SentinelConnectionString, combined, StringComparison.Ordinal);

        Assert.Contains("/__contract__/safe-log", collectedLogs, StringComparison.Ordinal);
        Assert.NotEmpty(TechnicalLogFieldPolicy.ProhibitedCategories);
        Assert.Contains("Authorization", TechnicalLogFieldPolicy.ProhibitedRequestHeaderNames);
    }

    [Fact]
    public async Task UnmappedExceptionPathDoesNotLeakProhibitedSentinelsIntoTechnicalLogs()
    {
        using var console = new ConsoleOutputCapture();
        string collectedLogs;
        string consoleOutput;

        await using (ApiContractWebApplicationFactory factory = new())
        {
            factory.LoggerProvider.Clear();
            HttpClient client = factory.CreateClient();

            using var request = new HttpRequestMessage(HttpMethod.Get, "/__contract__/unmapped-exception");
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {SentinelJwt}");
            request.Headers.TryAddWithoutValidation("Idempotency-Key", SentinelIdempotencyKey);
            request.Headers.TryAddWithoutValidation("X-Signing-Key", SentinelSigningKey);
            request.Headers.TryAddWithoutValidation("X-Connection-String", SentinelConnectionString);

            HttpResponseMessage response = await client.SendAsync(request);
            Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
            collectedLogs = factory.LoggerProvider.CombinedText;
        }

        consoleOutput = console.GetOutput();
        string combined = collectedLogs + Environment.NewLine + consoleOutput;

        // Exception message contains password/jwt sentinels; technical logging must not echo them.
        Assert.DoesNotContain(SentinelPassword, combined, StringComparison.Ordinal);
        Assert.DoesNotContain(SentinelJwt, combined, StringComparison.Ordinal);
        Assert.DoesNotContain(SentinelSigningKey, combined, StringComparison.Ordinal);
        Assert.DoesNotContain(SentinelIdempotencyKey, combined, StringComparison.Ordinal);
        Assert.DoesNotContain(SentinelConnectionString, combined, StringComparison.Ordinal);
        Assert.Contains(ApiErrorCatalog.InternalErrorCode, collectedLogs, StringComparison.Ordinal);
    }
}
