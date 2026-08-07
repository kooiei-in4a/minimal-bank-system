using System.Globalization;
using System.Net;
using System.Text.Json;
using MinimalBankSystem.Api.Controllers;
using MinimalBankSystem.Api.Runtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MinimalBankSystem.IntegrationTests;

[CollectionDefinition("Console output", DisableParallelization = true)]
public sealed class ConsoleOutputTestGroup
{
}

[Collection("Console output")]
public sealed class ApiRuntimeContractTests
{
    [Fact]
    public async Task UnmappedExceptionReturnsSafeCommonErrorEnvelope()
    {
        using RuntimeContractFactory factory = new();
        using HttpClient client = factory.CreateClient();
        const string expectedCorrelationId = "5f0e7480-3b80-4b28-bbd8-86d4a5f099bb";
        const string sensitiveExceptionMessage =
            "SENTINEL_PASSWORD SENTINEL_JWT SENTINEL_SIGNING_KEY SENTINEL_IDEMPOTENCY_KEY SENTINEL_CONNECTION_STRING";

        using HttpRequestMessage request = new(HttpMethod.Get, "/_test/runtime/unmapped-exception");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, expectedCorrelationId);
        request.Headers.Add(RuntimeContractTestController.TestExceptionMessageHeaderName, sensitiveExceptionMessage);

        using HttpResponseMessage response = await client.SendAsync(request);
        string responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out IEnumerable<string>? values));
        Assert.Equal(expectedCorrelationId, Assert.Single(values));
        Assert.DoesNotContain(sensitiveExceptionMessage, responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", responseBody, StringComparison.Ordinal);

        using JsonDocument document = JsonDocument.Parse(responseBody);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.Equal(2, document.RootElement.EnumerateObject().Count());
        Assert.Equal("internal_error", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("An unexpected error occurred.", document.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task InvalidCallerSuppliedCorrelationIdIsReplacedWithGeneratedCanonicalValue()
    {
        using RuntimeContractFactory factory = new();
        using HttpClient client = factory.CreateClient();
        const string invalidCorrelationId = "not-a-correlation-id";

        using HttpRequestMessage request = new(HttpMethod.Get, "/_test/runtime/unmapped-exception");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, invalidCorrelationId);

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out IEnumerable<string>? values));
        string responseCorrelationId = Assert.Single(values);
        Assert.NotEqual(invalidCorrelationId, responseCorrelationId);
        Assert.True(Guid.TryParseExact(responseCorrelationId, "D", out _));
    }

    [Fact]
    public async Task TestHostUsesInjectedDeterministicTimeProvider()
    {
        DateTimeOffset expectedUtcNow = new(2026, 8, 8, 13, 14, 15, TimeSpan.Zero);
        using RuntimeContractFactory factory = new(new FixedTimeProvider(expectedUtcNow));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/_test/runtime/utc-now");
        string responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(responseBody);
        DateTimeOffset actualUtcNow = document.RootElement.GetProperty("utcNow").GetDateTimeOffset();
        Assert.Equal(expectedUtcNow, actualUtcNow);
    }

    [Fact]
    public async Task JsonConsoleLogContainsCorrelationAndErrorCodeWithoutProhibitedFields()
    {
        const string expectedCorrelationId = "fa8e1c20-13ed-4ed6-91ca-39947afbd4d1";
        string[] prohibitedSentinels =
        [
            "SENTINEL_PASSWORD",
            "SENTINEL_JWT",
            "SENTINEL_SIGNING_KEY",
            "SENTINEL_IDEMPOTENCY_KEY",
            "SENTINEL_CONNECTION_STRING",
        ];

        string consoleOutput;

        using (ConsoleCapture capture = new())
        {
            using (RuntimeContractFactory factory = new())
            using (HttpClient client = factory.CreateClient())
            using (HttpRequestMessage request = new(HttpMethod.Get, "/_test/runtime/unmapped-exception"))
            {
                request.Headers.Add(CorrelationIdMiddleware.HeaderName, expectedCorrelationId);
                request.Headers.Add(
                    RuntimeContractTestController.TestExceptionMessageHeaderName,
                    string.Join(' ', prohibitedSentinels));

                using HttpResponseMessage response = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            }

            consoleOutput = capture.GetText();
        }

        string[] jsonLogLines = consoleOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith('{'))
            .ToArray();

        Assert.NotEmpty(jsonLogLines);

        foreach (string jsonLogLine in jsonLogLines)
        {
            using JsonDocument document = JsonDocument.Parse(jsonLogLine);
            Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        }

        string errorLogLine = Assert.Single(
            jsonLogLines,
            line => line.Contains("Unhandled API exception.", StringComparison.Ordinal));
        Assert.Contains(expectedCorrelationId, errorLogLine, StringComparison.Ordinal);
        Assert.Contains("internal_error", errorLogLine, StringComparison.Ordinal);

        foreach (string prohibitedSentinel in prohibitedSentinels)
        {
            Assert.DoesNotContain(prohibitedSentinel, consoleOutput, StringComparison.Ordinal);
        }
    }

    private sealed class RuntimeContractFactory : WebApplicationFactory<Program>
    {
        private readonly TimeProvider? timeProvider;

        public RuntimeContractFactory(TimeProvider? timeProvider = null)
        {
            this.timeProvider = timeProvider;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            if (timeProvider is not null)
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton(timeProvider);
                });
            }
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }

    private sealed class ConsoleCapture : IDisposable
    {
        private readonly TextWriter originalOutput = Console.Out;
        private readonly TextWriter originalError = Console.Error;
        private readonly StringWriter writer = new(CultureInfo.InvariantCulture);

        public ConsoleCapture()
        {
            Console.SetOut(writer);
            Console.SetError(writer);
        }

        public string GetText()
        {
            return writer.ToString();
        }

        public void Dispose()
        {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
            writer.Dispose();
        }
    }
}
