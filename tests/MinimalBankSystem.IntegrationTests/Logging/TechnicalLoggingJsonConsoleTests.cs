using System.Text.Json;
using MinimalBankSystem.Api.Correlation;
using MinimalBankSystem.IntegrationTests.TestOnly;

namespace MinimalBankSystem.IntegrationTests.Logging;

public sealed class TechnicalLoggingJsonConsoleTests
{
    [Fact]
    public async Task ConsoleOutputIsValidJsonPerLineAndIncludesCorrelationIdAndErrorCode()
    {
        const string suppliedId = "json-console-test-correlation-id";
        string capturedOutput = await ConsoleCapture.CaptureAsync(async () =>
        {
            await using ContractTestWebApplicationFactory factory = new();
            using HttpClient client = factory.CreateClient();

            using HttpRequestMessage request = new(HttpMethod.Get, "/__contract-test/throw");
            request.Headers.Add(CorrelationIdMiddleware.HeaderName, suppliedId);

            await client.SendAsync(request);
        });

        string[] lines = capturedOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        Assert.NotEmpty(lines);

        foreach (string line in lines)
        {
            using JsonDocument document = JsonDocument.Parse(line);
        }

        Assert.Contains(suppliedId, capturedOutput);
        Assert.Contains("internal_error", capturedOutput);
    }
}
