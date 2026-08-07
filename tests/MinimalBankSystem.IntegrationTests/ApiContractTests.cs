using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MinimalBankSystem.Api.Controllers;
using MinimalBankSystem.Api.Contracts;
using MinimalBankSystem.Api.Infrastructure;

namespace MinimalBankSystem.IntegrationTests;

public sealed class ApiContractTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public async Task UnmappedExceptionUsesSafeEnvelopeAndPropagatesCorrelationId()
    {
        using ApiApplicationFactory factory = new(FixedUtcNow);
        using HttpClient client = factory.CreateClient();
        const string callerCorrelationId = "AABBCCDD-EEFF-0011-2233-445566778899";
        client.DefaultRequestHeaders.Add(CorrelationId.HeaderName, callerCorrelationId);

        using HttpResponseMessage response = await client.GetAsync("/__contract/unmapped");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(
            callerCorrelationId.ToLowerInvariant(),
            Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)));

        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal(
            ["code", "message"],
            document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal("data_integrity_violation", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("An internal error occurred.", document.RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain("probe detail", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);

        CapturedLog log = Assert.Single(
            factory.Logs.Entries,
            entry => entry.Values.TryGetValue("ErrorCode", out string? errorCode)
                && errorCode == "data_integrity_violation");
        Assert.Equal(callerCorrelationId.ToLowerInvariant(), log.Values["CorrelationId"]);
        Assert.Equal("InvalidOperationException", log.Values["ExceptionType"]);
    }

    [Fact]
    public async Task InvalidCallerCorrelationIdIsReplacedWithGeneratedSafeId()
    {
        using ApiApplicationFactory factory = new(FixedUtcNow);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(CorrelationId.HeaderName, "not-a-guid");

        using HttpResponseMessage response = await client.GetAsync("/__contract/success");
        string responseCorrelationId = Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(Guid.TryParseExact(responseCorrelationId, "D", out Guid parsed));
        Assert.NotEqual(Guid.Empty, parsed);
        Assert.NotEqual("not-a-guid", responseCorrelationId);
    }

    [Fact]
    public async Task ValidationFailureUsesTheCommonErrorEnvelope()
    {
        using ApiApplicationFactory factory = new(FixedUtcNow);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/__contract/validation");
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            ["code", "message"],
            document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal("validation_failed", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("The request is invalid.", document.RootElement.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("11111111-1111-1111-1111-111111111111\r\nInjected: value")]
    public void CorrelationIdRejectsUnsafeOrInvalidCallerValues(string? candidate)
    {
        Assert.False(CorrelationId.TryNormalize(candidate, out _));
    }

    [Fact]
    public async Task ApplicationUsesTheInjectedTimeProvider()
    {
        using ApiApplicationFactory factory = new(FixedUtcNow);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/__contract/success");
        ContractProbeResponse? payload = await response.Content.ReadFromJsonAsync<ContractProbeResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(FixedUtcNow, payload!.UtcNow);
    }

    [Fact]
    public async Task TechnicalLogsDoNotContainProhibitedValuesOrExceptionDetails()
    {
        using ApiApplicationFactory factory = new(FixedUtcNow);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/__contract/unmapped");
        _ = await response.Content.ReadAsStringAsync();

        string logContent = string.Join(
            Environment.NewLine,
            factory.Logs.Entries.Select(entry =>
                entry.Message + Environment.NewLine + string.Join(",", entry.Values.Select(pair => $"{pair.Key}={pair.Value}"))));

        Assert.DoesNotContain("sentinel-password", logContent, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel-jwt", logContent, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel-signing-key", logContent, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel-idempotency-key", logContent, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel-connection-string", logContent, StringComparison.Ordinal);
        Assert.DoesNotContain("probe detail", logContent, StringComparison.Ordinal);
    }
}

[Collection("Console output")]
public sealed class JsonTechnicalLoggingTests
{
    [Fact]
    public void TechnicalLoggingWritesParseableJsonWithCorrelationAndErrorCode()
    {
        const string correlationId = "11111111-1111-1111-1111-111111111111";
        const string errorCode = "data_integrity_violation";
        TextWriter originalOutput = Console.Out;
        using StringWriter output = new(CultureInfo.InvariantCulture);

        try
        {
            Console.SetOut(output);
            using ILoggerFactory factory = LoggerFactory.Create(logging => logging.AddTechnicalLogging());
            ILogger logger = factory.CreateLogger("json-test");
            TechnicalLogging.UnhandledException(
                logger,
                correlationId,
                errorCode,
                new InvalidOperationException("sentinel exception detail"));
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        string serialized = output.ToString().Trim();
        using JsonDocument document = JsonDocument.Parse(serialized);

        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.Contains(correlationId, serialized, StringComparison.Ordinal);
        Assert.Contains(errorCode, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel exception detail", serialized, StringComparison.Ordinal);
    }
}

[CollectionDefinition("Console output", DisableParallelization = true)]
public sealed class ConsoleOutputTestGroup;

public sealed class ApiApplicationFactory(DateTimeOffset fixedUtcNow) : WebApplicationFactory<Program>
{
    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(fixedUtcNow));
        });
    }
}

public sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

public sealed record CapturedLog(
    LogLevel Level,
    string Message,
    IReadOnlyDictionary<string, string?> Values);

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<CapturedLog> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<CapturedLog> entries) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => EmptyScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Dictionary<string, string?> values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value?.ToString())
                : [];

            entries.Enqueue(new CapturedLog(logLevel, formatter(state, exception), values));
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public static EmptyScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
