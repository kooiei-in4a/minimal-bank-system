using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using MinimalBankSystem.Api.RuntimeContract;

namespace MinimalBankSystem.IntegrationTests;

public sealed class ApiContractTests : IClassFixture<ApiContractApplicationFactory>
{
    private static readonly DateTimeOffset ExpectedUtcNow =
        new(2030, 4, 5, 6, 7, 8, TimeSpan.Zero);
    private static readonly Action<ILogger, string, Exception?> WriteJsonLog =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(9000, "JsonConsoleTest"),
            "Request completed for {CorrelationId}");

    private readonly ApiContractApplicationFactory _factory;

    public ApiContractTests(ApiContractApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ErrorResponseUsesTheCommonEnvelope()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/__contract/error");
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("The request is invalid.", document.RootElement.GetProperty("message").GetString());
        Assert.Equal(2, document.RootElement.EnumerateObject().Count());
        AssertCorrelationIdHeader(response, out _);
    }

    [Fact]
    public async Task UnmappedExceptionUsesSafeErrorAndCorrelatesTechnicalLogs()
    {
        _factory.Logs.Clear();
        const string suppliedCorrelationId = "not-a-safe-correlation-id";
        const string password = "password-test-sentinel";
        const string jwt = "jwt-test-sentinel";
        const string signingKey = "signing-key-test-sentinel";
        const string idempotencyKey = "idempotency-key-test-sentinel";
        const string connectionString = "connection-string-test-sentinel";

        using HttpClient client = _factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/__contract/unmapped-exception");
        request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, suppliedCorrelationId);
        request.Headers.TryAddWithoutValidation("X-Test-Password", password);
        request.Headers.TryAddWithoutValidation("X-Test-Jwt", jwt);
        request.Headers.TryAddWithoutValidation("X-Test-Signing-Key", signingKey);
        request.Headers.TryAddWithoutValidation("X-Test-Idempotency-Key", idempotencyKey);
        request.Headers.TryAddWithoutValidation("X-Test-Connection-String", connectionString);

        using HttpResponseMessage response = await client.SendAsync(request);
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("internal_error", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("An unexpected error occurred.", document.RootElement.GetProperty("message").GetString());
        Assert.Equal(2, document.RootElement.EnumerateObject().Count());

        string responseCorrelationId = AssertCorrelationIdHeader(response, out Guid parsedCorrelationId);
        Assert.NotEqual(suppliedCorrelationId, responseCorrelationId);
        Assert.NotEqual(Guid.Empty, parsedCorrelationId);
        Assert.DoesNotContain("unmapped exception internal detail", document.RootElement.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", document.RootElement.ToString(), StringComparison.Ordinal);

        string technicalLogText = string.Join(
            Environment.NewLine,
            _factory.Logs.Entries
                .Where(entry => entry.Category == typeof(ApiRequestContractMiddleware).FullName)
                .Select(entry => entry.ToText()));

        Assert.Contains(responseCorrelationId, technicalLogText, StringComparison.Ordinal);
        Assert.Contains("internal_error", technicalLogText, StringComparison.Ordinal);
        Assert.DoesNotContain(password, technicalLogText, StringComparison.Ordinal);
        Assert.DoesNotContain(jwt, technicalLogText, StringComparison.Ordinal);
        Assert.DoesNotContain(signingKey, technicalLogText, StringComparison.Ordinal);
        Assert.DoesNotContain(idempotencyKey, technicalLogText, StringComparison.Ordinal);
        Assert.DoesNotContain(connectionString, technicalLogText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidCallerCorrelationIdIsCanonicalizedAndPropagated()
    {
        Guid suppliedCorrelationId = Guid.NewGuid();
        string suppliedHeader = suppliedCorrelationId.ToString("D").ToUpperInvariant();

        using HttpClient client = _factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/runtime-contract/ping");
        request.Headers.Add(CorrelationId.HeaderName, suppliedHeader);

        using HttpResponseMessage response = await client.SendAsync(request);

        string responseCorrelationId = AssertCorrelationIdHeader(response, out _);
        Assert.Equal(suppliedCorrelationId.ToString("D"), responseCorrelationId);
    }

    [Fact]
    public async Task ApplicationEndpointUsesTheInjectedTimeProvider()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/runtime-contract/ping");
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ExpectedUtcNow, document.RootElement.GetProperty("utcNow").GetDateTimeOffset());
    }

    [Fact]
    public void ApplicationLoggingUsesJsonConsoleWithScopes()
    {
        IOptionsMonitor<ConsoleLoggerOptions> loggerOptions =
            _factory.Services.GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>();
        IOptionsMonitor<JsonConsoleFormatterOptions> formatterOptions =
            _factory.Services.GetRequiredService<IOptionsMonitor<JsonConsoleFormatterOptions>>();

        Assert.Equal(ConsoleFormatterNames.Json, loggerOptions.CurrentValue.FormatterName);
        Assert.True(formatterOptions.CurrentValue.IncludeScopes);
        Assert.True(formatterOptions.CurrentValue.UseUtcTimestamp);
    }

    [Fact]
    public void JsonConsoleEmitsParseableStructuredOutput()
    {
        const string correlationId = "00000000-0000-0000-0000-000000000001";
        using StringWriter standardOutput = new();
        using StringWriter standardError = new();
        TextWriter originalOutput = Console.Out;
        TextWriter originalError = Console.Error;

        try
        {
            Console.SetOut(standardOutput);
            Console.SetError(standardError);

            using (ILoggerFactory loggerFactory = LoggerFactory.Create(logging =>
            {
                logging.AddJsonConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.UseUtcTimestamp = true;
                });
            }))
            {
                ILogger logger = loggerFactory.CreateLogger("json-console-test");
                WriteJsonLog(logger, correlationId, null);
            }
        }
        finally
        {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }

        string? jsonLine = (standardOutput.ToString() + standardError.ToString())
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.TrimStart().StartsWith('{'));

        Assert.NotNull(jsonLine);
        using JsonDocument document = JsonDocument.Parse(jsonLine);
        Assert.Equal("Information", document.RootElement.GetProperty("LogLevel").GetString());
        Assert.Contains(correlationId, document.RootElement.ToString(), StringComparison.Ordinal);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using Stream content = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(content);
    }

    private static string AssertCorrelationIdHeader(HttpResponseMessage response, out Guid parsed)
    {
        string value = Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName));
        Assert.True(Guid.TryParseExact(value, "D", out parsed));
        return value;
    }
}

public sealed class ApiContractApplicationFactory : WebApplicationFactory<Program>
{
    public ApiContractApplicationFactory()
    {
        Logs = new CapturingLoggerProvider();
    }

    public CapturingLoggerProvider Logs { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(
                new FixedTimeProvider(new DateTimeOffset(2030, 4, 5, 6, 7, 8, TimeSpan.Zero)));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Logs.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();

    public IReadOnlyCollection<CapturedLogEntry> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public void Clear()
    {
        while (_entries.TryDequeue(out _))
        {
        }
    }

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(
        string categoryName,
        ConcurrentQueue<CapturedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(pair => pair.Key, pair => pair.Value?.ToString() ?? string.Empty)
                : new Dictionary<string, string>();

            entries.Enqueue(
                new CapturedLogEntry(
                    categoryName,
                    logLevel,
                    eventId,
                    formatter(state, exception),
                    properties));
        }
    }
}

public sealed record CapturedLogEntry(
    string Category,
    LogLevel LogLevel,
    EventId EventId,
    string Message,
    IReadOnlyDictionary<string, string> Properties)
{
    public string ToText()
    {
        return string.Join(
            "|",
            Category,
            LogLevel,
            EventId.Id,
            Message,
            string.Join(",", Properties.Select(pair => $"{pair.Key}={pair.Value}")));
    }
}
