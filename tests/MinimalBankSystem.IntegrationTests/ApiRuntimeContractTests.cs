using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Runtime;

namespace MinimalBankSystem.IntegrationTests;

public sealed class ApiRuntimeContractTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2030, 4, 5, 6, 7, 8, TimeSpan.Zero);

    [Fact]
    public async Task UnmappedExceptionUsesSafe500EnvelopeAndProductionHasNoBusinessMapper()
    {
        using ContractWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        Assert.Empty(factory.Services.GetServices<IApiExceptionMapper>());

        using HttpResponseMessage response = await client.GetAsync("/__contract/error/unmapped");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertErrorEnvelope(body, "internal_error", "An internal error occurred.");
        Assert.DoesNotContain("unmapped internal detail", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisteredMapperExtendsTheProductionPipeline()
    {
        using ContractWebApplicationFactory factory = new(
            services => services.AddSingleton<IApiExceptionMapper>(new ContractProbeExceptionMapper()));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/__contract/error/mapped");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        AssertErrorEnvelope(body, "contract_probe_conflict", "The contract probe failed.");
    }

    [Fact]
    public async Task MapperFailureFallsBackToTheGenericError()
    {
        using ConsoleCapture capture = new();

        using (ContractWebApplicationFactory factory = new(
                   services => services.AddSingleton<IApiExceptionMapper>(new ThrowingExceptionMapper())))
        using (HttpClient client = factory.CreateClient())
        using (HttpResponseMessage response = await client.GetAsync("/__contract/error/mapped"))
        {
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            AssertErrorEnvelope(body, "internal_error", "An internal error occurred.");
            Assert.DoesNotContain(SecretSentinels.MapperFailure, body, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(SecretSentinels.MapperFailure, capture.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiControllerValidationUsesTheCommonEnvelope()
    {
        using ContractWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/__contract/validation",
            new { });
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertErrorEnvelope(body, "validation_failed", "The request is invalid.");
    }

    [Fact]
    public async Task MissingCorrelationIdIsGeneratedForRequestAndResponse()
    {
        using ContractWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/__contract/correlation");
        string responseCorrelationId = GetCorrelationId(response);

        Assert.True(Guid.TryParseExact(responseCorrelationId, "N", out _));

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            responseCorrelationId,
            body.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task SafeCallerCorrelationIdIsKeptAcrossRequestResponseErrorAndJsonLog()
    {
        const string supplied = "caller-trace_01.safe";
        using ConsoleCapture capture = new();

        using (ContractWebApplicationFactory factory = new())
        using (HttpClient client = factory.CreateClient())
        using (HttpRequestMessage request = new(HttpMethod.Get, "/__contract/error/unmapped"))
        {
            request.Headers.Add(CorrelationIdMiddleware.HeaderName, supplied);

            using HttpResponseMessage response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(supplied, GetCorrelationId(response));
        }

        JsonElement[] entries = ParseJsonLogLines(capture.Content);
        Assert.Contains(
            entries,
            entry => ContainsStringProperty(entry, "CorrelationId", supplied) &&
                ContainsStringProperty(entry, "ErrorCode", "internal_error"));
    }

    [Theory]
    [InlineData("caller value with spaces")]
    [InlineData("caller\nnewline")]
    [InlineData("caller\u0001control")]
    public async Task UnsafeCallerCorrelationIdIsRejected(string supplied)
    {
        using ContractWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/__contract/correlation");
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, supplied);

        using HttpResponseMessage response = await client.SendAsync(request);
        string established = GetCorrelationId(response);

        Assert.NotEqual(supplied, established);
        Assert.True(Guid.TryParseExact(established, "N", out _));
    }

    [Fact]
    public async Task OversizedCallerCorrelationIdIsRejected()
    {
        string supplied = new('a', 65);
        using ContractWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/__contract/correlation");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, supplied);

        using HttpResponseMessage response = await client.SendAsync(request);
        string established = GetCorrelationId(response);

        Assert.NotEqual(supplied, established);
        Assert.True(Guid.TryParseExact(established, "N", out _));
    }

    [Fact]
    public async Task MultipleCallerCorrelationIdsAreRejected()
    {
        using ContractWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/__contract/correlation");
        request.Headers.TryAddWithoutValidation(
            CorrelationIdMiddleware.HeaderName,
            ["safe-first", "safe-second"]);

        using HttpResponseMessage response = await client.SendAsync(request);
        string established = GetCorrelationId(response);

        Assert.NotEqual("safe-first", established);
        Assert.NotEqual("safe-second", established);
        Assert.True(Guid.TryParseExact(established, "N", out _));
    }

    [Fact]
    public async Task HttpRequestUsesApplicationConsumerAndInjectedTimeProvider()
    {
        using ContractWebApplicationFactory factory = new(
            timeProvider: new FrozenTimeProvider(FixedUtcNow));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/__contract/time");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(FixedUtcNow, body.RootElement.GetProperty("utcNow").GetDateTimeOffset());
    }

    [Fact]
    public async Task ActualJsonConsoleOutputIsParseableAndDoesNotDiscloseSecrets()
    {
        const string suppliedCorrelationId = "safe-log-correlation";
        using ConsoleCapture capture = new();

        using (ContractWebApplicationFactory factory = new())
        using (HttpClient client = factory.CreateClient())
        using (HttpRequestMessage request = new(
            HttpMethod.Post,
            $"/__contract/error/secret?password={Uri.EscapeDataString(SecretSentinels.QueryPassword)}"))
        {
            request.Headers.Add(CorrelationIdMiddleware.HeaderName, suppliedCorrelationId);
            request.Headers.TryAddWithoutValidation(
                "Authorization",
                $"Bearer {SecretSentinels.HeaderJwt}");
            request.Headers.TryAddWithoutValidation("X-Signing-Key", SecretSentinels.HeaderSigningKey);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", SecretSentinels.HeaderIdempotencyKey);
            request.Headers.TryAddWithoutValidation("X-Connection-String", SecretSentinels.HeaderConnectionString);
            request.Content = JsonContent.Create(new { password = SecretSentinels.BodyPassword });

            using HttpResponseMessage response = await client.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(suppliedCorrelationId, GetCorrelationId(response));
            AssertErrorEnvelope(body, "internal_error", "An internal error occurred.");

            foreach (string sentinel in SecretSentinels.All)
            {
                Assert.DoesNotContain(sentinel, body, StringComparison.Ordinal);
            }
        }

        string logs = capture.Content;
        JsonElement[] entries = ParseJsonLogLines(logs);

        Assert.Contains(
            entries,
            entry => ContainsStringProperty(entry, "CorrelationId", suppliedCorrelationId) &&
                ContainsStringProperty(entry, "ErrorCode", "internal_error"));

        foreach (JsonElement entry in entries)
        {
            DateTimeOffset timestamp = entry.GetProperty("Timestamp").GetDateTimeOffset();
            Assert.Equal(TimeSpan.Zero, timestamp.Offset);
        }

        foreach (string sentinel in SecretSentinels.All)
        {
            Assert.DoesNotContain(sentinel, logs, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task RejectedRawCorrelationIdIsNotLogged()
    {
        const string rejected = "rejected-correlation!sentinel";
        using ConsoleCapture capture = new();

        using (ContractWebApplicationFactory factory = new())
        using (HttpClient client = factory.CreateClient())
        using (HttpRequestMessage request = new(HttpMethod.Get, "/__contract/error/unmapped"))
        {
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, rejected);

            using HttpResponseMessage response = await client.SendAsync(request);
            Assert.NotEqual(rejected, GetCorrelationId(response));
        }

        Assert.DoesNotContain(rejected, capture.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OperationCanceledExceptionIsNotConvertedToGeneric500()
    {
        using ContractWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        try
        {
            using HttpResponseMessage response = await client.GetAsync("/__contract/canceled");
            string body = await response.Content.ReadAsStringAsync();

            Assert.DoesNotContain("internal_error", body, StringComparison.Ordinal);
        }
        catch (Exception exception)
        {
            Assert.Contains(
                EnumerateExceptionChain(exception),
                candidate => candidate is OperationCanceledException);
        }
    }

    [Fact]
    public async Task ResponseStartedExceptionIsRethrownByProductionPipeline()
    {
        using ConsoleCapture capture = new();
        Exception exception;

        using (ContractWebApplicationFactory factory = new())
        using (HttpClient client = factory.CreateClient())
        {
            exception = await Assert.ThrowsAnyAsync<Exception>(
                () => client.GetAsync("/__contract/response-started"));
        }

        Assert.Contains(
            EnumerateExceptionChain(exception),
            candidate => candidate is InvalidOperationException invalidOperation &&
                invalidOperation.Message == SecretSentinels.ResponseStartedException);
        Assert.DoesNotContain(
            SecretSentinels.ResponseStartedException,
            capture.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task OperationCanceledExceptionIsRethrownByMiddleware()
    {
        OperationCanceledException expected = new("request canceled");
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();

        ApiExceptionMiddleware middleware = new(
            _ => Task.FromException(expected),
            NullLogger<ApiExceptionMiddleware>.Instance);

        OperationCanceledException actual = await Assert.ThrowsAsync<OperationCanceledException>(
            () => middleware.InvokeAsync(context, []));

        Assert.Same(expected, actual);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task ResponseStartedBranchDoesNotAppendAnErrorEnvelope()
    {
        InvalidOperationException expected = new("response already started");
        StartedResponseFeature responseFeature = new();
        DefaultHttpContext context = new();
        context.Features.Set<IHttpResponseFeature>(responseFeature);

        ApiExceptionMiddleware middleware = new(
            _ => Task.FromException(expected),
            NullLogger<ApiExceptionMiddleware>.Instance);

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context, []));

        Assert.Same(expected, actual);
        Assert.Equal(0, responseFeature.Body.Length);
    }

    private static string GetCorrelationId(HttpResponseMessage response) =>
        Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));

    private static IEnumerable<Exception> EnumerateExceptionChain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
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

    private static JsonElement[] ParseJsonLogLines(string logs)
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

    private sealed class StartedResponseFeature : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = StatusCodes.Status200OK;

        public string? ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public Stream Body { get; set; } = new MemoryStream();

        public bool HasStarted => true;

        public void OnStarting(Func<object, Task> callback, object state)
        {
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }
    }
}

[ApiController]
public sealed class RuntimeContractController(ApplicationTime applicationTime) : ControllerBase
{
    [HttpGet("/__contract/correlation")]
    public ActionResult<object> GetCorrelation() =>
        Ok(new { CorrelationId = HttpContext.TraceIdentifier });

    [HttpGet("/__contract/time")]
    public ActionResult<object> GetTime() =>
        Ok(new { UtcNow = applicationTime.GetUtcNow() });

    [HttpGet("/__contract/error/unmapped")]
    public IActionResult GetUnmappedError()
    {
        _ = HttpContext;
        throw new InvalidOperationException("unmapped internal detail");
    }

    [HttpGet("/__contract/error/mapped")]
    public IActionResult GetMappedError()
    {
        _ = HttpContext;
        throw new ContractProbeException();
    }

    [HttpPost("/__contract/error/secret")]
    public IActionResult GetSecretError([FromBody] SecretPayload payload)
    {
        _ = HttpContext;
        _ = payload;
        throw new InvalidOperationException(SecretSentinels.ExceptionMessage);
    }

    [HttpPost("/__contract/validation")]
    public IActionResult Validate([FromBody] ValidationRequest request)
    {
        _ = request;
        return Ok();
    }

    [HttpGet("/__contract/canceled")]
    public IActionResult GetCanceled()
    {
        _ = HttpContext;
        throw new OperationCanceledException("request canceled");
    }

    [HttpGet("/__contract/response-started")]
    public async Task GetResponseStarted()
    {
        await Response.WriteAsync("prefix");
        await Response.Body.FlushAsync();
        throw new InvalidOperationException(SecretSentinels.ResponseStartedException);
    }
}

public sealed record SecretPayload(string? Password);

public sealed record ValidationRequest([Required] string? Name);

internal sealed class ContractWebApplicationFactory(
    Action<IServiceCollection>? configureServices = null,
    TimeProvider? timeProvider = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureServices(services =>
        {
            services
                .AddControllers()
                .AddApplicationPart(typeof(RuntimeContractController).Assembly);

            if (timeProvider is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(timeProvider);
            }

            configureServices?.Invoke(services);
        });
    }
}

internal sealed class ContractProbeExceptionMapper : IApiExceptionMapper
{
    public ApiErrorMapping? TryMap(Exception exception) =>
        exception is ContractProbeException
            ? new ApiErrorMapping(
                StatusCodes.Status409Conflict,
                "contract_probe_conflict",
                "The contract probe failed.")
            : null;
}

internal sealed class ThrowingExceptionMapper : IApiExceptionMapper
{
    public ApiErrorMapping? TryMap(Exception exception)
    {
        _ = exception;
        throw new InvalidOperationException(SecretSentinels.MapperFailure);
    }
}

internal sealed class ContractProbeException : Exception;

internal sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

internal static class SecretSentinels
{
    public const string QueryPassword = "QUERY_PASSWORD_SENTINEL";
    public const string HeaderJwt = "HEADER_JWT_SENTINEL";
    public const string HeaderSigningKey = "HEADER_SIGNING_KEY_SENTINEL";
    public const string HeaderIdempotencyKey = "HEADER_RAW_IDEMPOTENCY_KEY_SENTINEL";
    public const string HeaderConnectionString = "HEADER_CONNECTION_STRING_SENTINEL";
    public const string BodyPassword = "BODY_PASSWORD_SENTINEL";
    public const string ExceptionPassword = "EXCEPTION_PASSWORD_SENTINEL";
    public const string ExceptionJwt = "EXCEPTION_JWT_SENTINEL";
    public const string ExceptionSigningKey = "EXCEPTION_SIGNING_KEY_SENTINEL";
    public const string ExceptionIdempotencyKey = "EXCEPTION_RAW_IDEMPOTENCY_KEY_SENTINEL";
    public const string ExceptionConnectionString = "EXCEPTION_CONNECTION_STRING_SENTINEL";
    public const string MapperFailure = "MAPPER_FAILURE_SECRET_SENTINEL";
    public const string ResponseStartedException = "RESPONSE_STARTED_EXCEPTION_SECRET_SENTINEL";

    public static string ExceptionMessage => string.Join(
        '|',
        ExceptionPassword,
        ExceptionJwt,
        ExceptionSigningKey,
        ExceptionIdempotencyKey,
        ExceptionConnectionString);

    public static readonly string[] All =
    [
        QueryPassword,
        HeaderJwt,
        HeaderSigningKey,
        HeaderIdempotencyKey,
        HeaderConnectionString,
        BodyPassword,
        ExceptionPassword,
        ExceptionJwt,
        ExceptionSigningKey,
        ExceptionIdempotencyKey,
        ExceptionConnectionString,
        MapperFailure,
        ResponseStartedException,
    ];
}

internal sealed class ConsoleCapture : IDisposable
{
    private readonly TextWriter originalOutput = Console.Out;
    private readonly TextWriter originalError = Console.Error;
    private readonly StringWriter buffer = new(CultureInfo.InvariantCulture);
    private readonly TextWriter synchronizedWriter;

    public ConsoleCapture()
    {
        synchronizedWriter = TextWriter.Synchronized(buffer);
        Console.SetOut(synchronizedWriter);
        Console.SetError(synchronizedWriter);
    }

    public string Content
    {
        get
        {
            synchronizedWriter.Flush();
            return buffer.ToString();
        }
    }

    public void Dispose()
    {
        Console.SetOut(originalOutput);
        Console.SetError(originalError);
        synchronizedWriter.Dispose();
        buffer.Dispose();
    }
}
