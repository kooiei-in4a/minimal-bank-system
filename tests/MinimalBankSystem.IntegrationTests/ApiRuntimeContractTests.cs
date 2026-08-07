using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MinimalBankSystem.Api.Errors;
using MinimalBankSystem.Api.Requests;
using MinimalBankSystem.Application;

namespace MinimalBankSystem.IntegrationTests;

public sealed class ApiRuntimeContractTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 8, 8, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    public async Task MappedExceptionUsesExtensionPointAndCommonEnvelope()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/__contract/error/mapped");

        CapturedResponse response = await SendAndCaptureAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        AssertErrorEnvelope(
            response.Body,
            "contract_probe_conflict",
            "The contract probe failed.");
        AssertTechnicalFailureLog(
            response.Logs,
            response.CorrelationId,
            "contract_probe_conflict");
    }

    [Fact]
    public async Task UnmappedExceptionReturnsSafe500AndLogsNoProhibitedValues()
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/__contract/error/unmapped")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ProhibitedLogSentinels.AsPayload()),
                Encoding.UTF8,
                "application/json"),
        };

        foreach ((string headerName, string value) in ProhibitedLogSentinels.AsHeaders())
        {
            request.Headers.TryAddWithoutValidation(headerName, value);
        }

        CapturedResponse response = await SendAndCaptureAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertErrorEnvelope(response.Body, "internal_error", "An internal error occurred.");
        AssertTechnicalFailureLog(response.Logs, response.CorrelationId, "internal_error");

        foreach (string sentinel in ProhibitedLogSentinels.All)
        {
            Assert.DoesNotContain(sentinel, response.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(sentinel, response.Logs, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("stack trace", response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CorrelationIdIsGeneratedAndAvailableInRequestAndResponse()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/__contract/correlation");

        CapturedResponse response = await SendAndCaptureAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(Guid.TryParseExact(response.CorrelationId, "N", out _));

        using JsonDocument document = JsonDocument.Parse(response.Body);
        Assert.Equal(
            response.CorrelationId,
            document.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task SafeCallerCorrelationIdIsNormalizedAndPropagatedToTechnicalLog()
    {
        const string suppliedCorrelationId = "4d36e967e3254e45a9215ee7be25ac7a";
        using HttpRequestMessage request = new(HttpMethod.Get, "/__contract/error/mapped");
        request.Headers.Add(CorrelationIdContract.HeaderName, suppliedCorrelationId);

        CapturedResponse response = await SendAndCaptureAsync(request);

        Assert.Equal(suppliedCorrelationId, response.CorrelationId);
        AssertTechnicalFailureLog(
            response.Logs,
            suppliedCorrelationId,
            "contract_probe_conflict");
    }

    [Fact]
    public async Task UnsafeCallerCorrelationIdIsNotTrusted()
    {
        const string unsafeCorrelationId = "caller-controlled-unsafe-value";
        using HttpRequestMessage request = new(HttpMethod.Get, "/__contract/correlation");
        request.Headers.TryAddWithoutValidation(
            CorrelationIdContract.HeaderName,
            unsafeCorrelationId);

        CapturedResponse response = await SendAndCaptureAsync(request);

        Assert.NotEqual(unsafeCorrelationId, response.CorrelationId);
        Assert.True(Guid.TryParseExact(response.CorrelationId, "N", out _));
        Assert.DoesNotContain(unsafeCorrelationId, response.Logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InjectedTimeProviderDrivesApplicationClockThroughApi()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/__contract/time");

        CapturedResponse response = await SendAndCaptureAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(response.Body);
        DateTimeOffset actual = document.RootElement.GetProperty("utcNow").GetDateTimeOffset();
        Assert.Equal(FixedUtcNow, actual);
    }

    private static async Task<CapturedResponse> SendAndCaptureAsync(HttpRequestMessage request)
    {
        using ConsoleCapture capture = new();

        HttpStatusCode statusCode;
        string body;
        string correlationId;

        using (ContractWebApplicationFactory factory = new(FixedUtcNow))
        using (HttpClient client = factory.CreateClient())
        using (HttpResponseMessage response = await client.SendAsync(request))
        {
            statusCode = response.StatusCode;
            body = await response.Content.ReadAsStringAsync();
            correlationId = Assert.Single(
                response.Headers.GetValues(CorrelationIdContract.HeaderName));
        }

        return new CapturedResponse(statusCode, body, correlationId, capture.Content);
    }

    private static void AssertErrorEnvelope(string body, string expectedCode, string expectedMessage)
    {
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;

        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal(
            ["code", "message"],
            root.EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(expectedCode, root.GetProperty("code").GetString());
        Assert.Equal(expectedMessage, root.GetProperty("message").GetString());
    }

    private static void AssertTechnicalFailureLog(
        string logs,
        string expectedCorrelationId,
        string expectedErrorCode)
    {
        JsonElement[] entries = ParseJsonLogEntries(logs);

        Assert.Contains(
            entries,
            entry => ContainsStringProperty(entry, "CorrelationId", expectedCorrelationId) &&
                ContainsStringProperty(entry, "ErrorCode", expectedErrorCode));
    }

    private static JsonElement[] ParseJsonLogEntries(string logs)
    {
        string[] lines = logs.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.NotEmpty(lines);

        return lines
            .Select(line =>
            {
                using JsonDocument document = JsonDocument.Parse(line);
                return document.RootElement.Clone();
            })
            .ToArray();
    }

    private static bool ContainsStringProperty(
        JsonElement element,
        string propertyName,
        string expectedValue)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.NameEquals(propertyName) &&
                    property.Value.ValueKind == JsonValueKind.String &&
                    property.Value.GetString() == expectedValue)
                {
                    return true;
                }

                if (ContainsStringProperty(property.Value, propertyName, expectedValue))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (ContainsStringProperty(item, propertyName, expectedValue))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed record CapturedResponse(
        HttpStatusCode StatusCode,
        string Body,
        string CorrelationId,
        string Logs);
}

[ApiController]
public sealed class ContractProbeController(ApplicationClock applicationClock) : ControllerBase
{
    [HttpGet("/__contract/correlation")]
    public ActionResult<object> GetCorrelation() =>
        Ok(new { CorrelationId = HttpContext.TraceIdentifier });

    [HttpGet("/__contract/time")]
    public ActionResult<object> GetTime() =>
        Ok(new { UtcNow = applicationClock.GetUtcNow() });

    [HttpGet("/__contract/error/mapped")]
    public IActionResult GetMappedError()
    {
        _ = HttpContext;
        throw new ContractProbeException();
    }

    [HttpPost("/__contract/error/unmapped")]
    public IActionResult GetUnmappedError()
    {
        _ = HttpContext;
        throw new InvalidOperationException(ProhibitedLogSentinels.ExceptionMessage);
    }
}

internal sealed class ContractWebApplicationFactory(DateTimeOffset utcNow)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services
                .AddControllers()
                .AddApplicationPart(typeof(ContractProbeController).Assembly);

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FrozenTimeProvider(utcNow));
            services.AddSingleton<IExceptionToHttpMapper, ContractProbeExceptionMapper>();
        });
    }
}

internal sealed class ContractProbeExceptionMapper : IExceptionToHttpMapper
{
    public ApiErrorMapping? Map(Exception exception) =>
        exception is ContractProbeException
            ? new ApiErrorMapping(
                StatusCodes.Status409Conflict,
                "contract_probe_conflict",
                "The contract probe failed.")
            : null;
}

internal sealed class ContractProbeException : Exception;

internal sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

internal static class ProhibitedLogSentinels
{
    public const string Password = "DUMMY_PASSWORD_SENTINEL";
    public const string Jwt = "DUMMY_JWT_SENTINEL";
    public const string SigningKey = "DUMMY_SIGNING_KEY_SENTINEL";
    public const string IdempotencyKey = "DUMMY_IDEMPOTENCY_KEY_SENTINEL";
    public const string ConnectionString = "DUMMY_CONNECTION_STRING_SENTINEL";

    public static readonly string[] All =
    [
        Password,
        Jwt,
        SigningKey,
        IdempotencyKey,
        ConnectionString,
    ];

    public static string ExceptionMessage => string.Join('|', All);

    public static Dictionary<string, string> AsPayload() => new()
    {
        ["password"] = Password,
        ["jwt"] = Jwt,
        ["signingKey"] = SigningKey,
        ["idempotencyKey"] = IdempotencyKey,
        ["connectionString"] = ConnectionString,
    };

    public static (string Name, string Value)[] AsHeaders() =>
    [
        ("X-Test-Password", Password),
        ("X-Test-Jwt", Jwt),
        ("X-Test-Signing-Key", SigningKey),
        ("Idempotency-Key", IdempotencyKey),
        ("X-Test-Connection-String", ConnectionString),
    ];
}

internal sealed class ConsoleCapture : IDisposable
{
    private readonly TextWriter originalOutput = Console.Out;
    private readonly StringWriter buffer = new(CultureInfo.InvariantCulture);

    public ConsoleCapture() => Console.SetOut(TextWriter.Synchronized(buffer));

    public string Content => buffer.ToString();

    public void Dispose()
    {
        Console.SetOut(originalOutput);
        buffer.Dispose();
    }
}
