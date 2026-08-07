using System.Text;
using System.Text.Json;
using MinimalBankSystem.Api.Runtime;

namespace MinimalBankSystem.IntegrationTests.Api;

[Collection(ConsoleLogCaptureGroup.Name)]
public sealed class CorrelationAndLoggingTests
{
    [Fact]
    public async Task CorrelationIdIsEstablishedOnResponseAndTechnicalLog()
    {
        using var console = new ConsoleOutputCapture();
        string correlationId;
        string collectedLogs;

        await using (ApiContractWebApplicationFactory factory = new())
        {
            factory.LoggerProvider.Clear();
            HttpClient client = factory.CreateClient();

            HttpResponseMessage response = await client.GetAsync("/__contract__/ping");
            string body = await response.Content.ReadAsStringAsync();

            Assert.True(response.IsSuccessStatusCode);
            Assert.True(response.Headers.TryGetValues(CorrelationId.HeaderName, out IEnumerable<string>? values));
            correlationId = Assert.Single(values);

            using JsonDocument document = JsonDocument.Parse(body);
            Assert.Equal(correlationId, document.RootElement.GetProperty("correlationId").GetString());

            collectedLogs = factory.LoggerProvider.CombinedText;
        }

        Assert.Contains(correlationId, collectedLogs, StringComparison.Ordinal);

        string consoleOutput = console.GetOutput();
        Assert.Contains(correlationId, consoleOutput, StringComparison.Ordinal);
        AssertJsonConsoleContainsCorrelation(consoleOutput, correlationId);
    }

    [Fact]
    public async Task CallerSuppliedSafeCorrelationIdIsAccepted()
    {
        await using ApiContractWebApplicationFactory factory = new();
        HttpClient client = factory.CreateClient();

        const string supplied = "client-correlation-001";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/__contract__/ping");
        request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, supplied);

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues(CorrelationId.HeaderName, out IEnumerable<string>? values));
        Assert.Equal(supplied, Assert.Single(values));
    }

    [Theory]
    [InlineData("bad value with spaces")]
    [InlineData("has\nnewline")]
    [InlineData("has\ttab")]
    [InlineData("has/slash")]
    [InlineData("")]
    public async Task CallerSuppliedUnsafeCorrelationIdIsRejectedAndReplaced(string unsafeValue)
    {
        await using ApiContractWebApplicationFactory factory = new();
        HttpClient client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/__contract__/ping");
        request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, unsafeValue);

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues(CorrelationId.HeaderName, out IEnumerable<string>? values));
        string established = Assert.Single(values);
        Assert.NotEqual(unsafeValue, established);
        Assert.True(CorrelationId.TryNormalize(established, out _));
    }

    [Fact]
    public async Task OversizedCallerCorrelationIdIsRejectedAndReplaced()
    {
        await using ApiContractWebApplicationFactory factory = new();
        HttpClient client = factory.CreateClient();

        string oversized = new('a', CorrelationId.MaxLength + 1);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/__contract__/ping");
        request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, oversized);

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues(CorrelationId.HeaderName, out IEnumerable<string>? values));
        string established = Assert.Single(values);
        Assert.NotEqual(oversized, established);
        Assert.True(established.Length <= CorrelationId.MaxLength);
    }

    [Fact]
    public async Task UnmappedExceptionTechnicalLogIncludesCorrelationAndErrorCodeAsJsonConsole()
    {
        using var console = new ConsoleOutputCapture();
        string correlationId;
        string collectedLogs;

        await using (ApiContractWebApplicationFactory factory = new())
        {
            factory.LoggerProvider.Clear();
            HttpClient client = factory.CreateClient();

            HttpResponseMessage response = await client.GetAsync("/__contract__/unmapped-exception");

            Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.True(response.Headers.TryGetValues(CorrelationId.HeaderName, out IEnumerable<string>? values));
            correlationId = Assert.Single(values);

            collectedLogs = factory.LoggerProvider.CombinedText;
        }

        Assert.Contains(correlationId, collectedLogs, StringComparison.Ordinal);
        Assert.Contains(ApiErrorCatalog.InternalErrorCode, collectedLogs, StringComparison.Ordinal);

        string consoleOutput = console.GetOutput();
        AssertJsonConsoleContainsCorrelation(consoleOutput, correlationId);
        Assert.Contains(ApiErrorCatalog.InternalErrorCode, consoleOutput, StringComparison.Ordinal);
    }

    private static void AssertJsonConsoleContainsCorrelation(string consoleOutput, string correlationId)
    {
        Assert.False(string.IsNullOrWhiteSpace(consoleOutput));

        string[] lines = consoleOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(lines);

        bool found = false;
        foreach (string line in lines)
        {
            // Console capture may include non-JSON noise; only assert on JSON lines.
            if (line.Length == 0 || line[0] != '{')
            {
                continue;
            }

            using JsonDocument document = JsonDocument.Parse(line);
            string raw = document.RootElement.GetRawText();
            if (raw.Contains(correlationId, StringComparison.Ordinal))
            {
                found = true;
                break;
            }
        }

        Assert.True(found, "Expected JSON console output to include the correlation ID.");
    }
}

[CollectionDefinition(ConsoleLogCaptureGroup.Name, DisableParallelization = true)]
public sealed class ConsoleLogCaptureGroup : ICollectionFixture<object>
{
    public const string Name = "ConsoleLogCapture";
}

internal sealed class ConsoleOutputCapture : IDisposable
{
    private readonly TextWriter _originalOut;
    private readonly StringBuilder _buffer = new();
    private readonly StringWriter _writer;

    public ConsoleOutputCapture()
    {
        _originalOut = Console.Out;
        _writer = new StringWriter(_buffer);
        Console.SetOut(_writer);
    }

    public string GetOutput()
    {
        _writer.Flush();
        return _buffer.ToString();
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        _writer.Dispose();
    }
}
