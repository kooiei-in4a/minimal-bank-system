using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
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

[Collection(TestExecutionCollections.ConsoleSensitive)]
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
    public async Task MapperOperationCanceledExceptionFallsBackWhenRequestIsNotAborted()
    {
        using ConsoleCapture capture = new();

        using (ContractWebApplicationFactory factory = new(
                   services => services.AddSingleton<IApiExceptionMapper>(new ThrowingOperationCanceledExceptionMapper())))
        using (HttpClient client = factory.CreateClient())
        using (HttpResponseMessage response = await client.GetAsync("/__contract/error/mapped"))
        {
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            AssertErrorEnvelope(body, "internal_error", "An internal error occurred.");
            Assert.DoesNotContain(SecretSentinels.MapperCancellation, body, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(SecretSentinels.MapperCancellation, capture.Content, StringComparison.Ordinal);
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

    [Theory]
    [InlineData("GET", "/__contract/does-not-exist", 404, "endpoint_not_found", "The requested endpoint was not found.")]
    [InlineData("GET", "/__contract/validation", 405, "method_not_allowed", "The HTTP method is not allowed for this endpoint.")]
    [InlineData("POST", "/__contract/media-type", 415, "unsupported_media_type", "The request media type is not supported.")]
    public async Task FrameworkErrorsUseTheApprovedCommonEnvelope(
        string method,
        string path,
        int expectedStatusCode,
        string expectedCode,
        string expectedMessage)
    {
        using ContractWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(new HttpMethod(method), path);

        if (expectedStatusCode == StatusCodes.Status415UnsupportedMediaType)
        {
            request.Content = new StringContent("not-json", Encoding.UTF8, "text/plain");
        }

        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)expectedStatusCode, response.StatusCode);
        AssertErrorEnvelope(body, expectedCode, expectedMessage);
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
    public async Task NonRequestAbortedOperationCanceledExceptionUsesTheGenericErrorContract()
    {
        using ConsoleCapture capture = new();

        using ContractWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/__contract/canceled");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertErrorEnvelope(body, "internal_error", "An internal error occurred.");
        Assert.DoesNotContain(SecretSentinels.InternalCancellationException, body, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretSentinels.InternalCancellationException, capture.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResponseStartedExceptionDoesNotCompleteAsSuccessInTestServerPipeline()
    {
        using ConsoleCapture capture = new();

        bool completedAsSuccess = false;
        using (ContractWebApplicationFactory factory = new())
        using (HttpClient client = factory.CreateClient())
        {
            try
            {
                using HttpResponseMessage response = await client.GetAsync("/__contract/response-started");
                completedAsSuccess = response.StatusCode == HttpStatusCode.OK &&
                    await response.Content.ReadAsStringAsync() == "prefix";
            }
            catch (HttpRequestException)
            {
            }
            catch (OperationCanceledException)
            {
            }
        }

        Assert.False(completedAsSuccess);
        JsonElement[] entries = ParseJsonLogLines(capture.Content);
        Assert.Contains(entries, entry => ContainsStringProperty(entry, "ErrorCode", "internal_error"));
        Assert.DoesNotContain(
            SecretSentinels.ResponseStartedException,
            capture.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task KestrelResponseStartedExceptionDoesNotLeakSecretsOrEscapeToServerLogging()
    {
        using ConsoleCapture capture = new();
        bool completedAsSuccess = false;

        using (ContractWebApplicationFactory factory = new())
        {
            factory.UseKestrel(0);

            using HttpClient client = factory.CreateClient();
            try
            {
                using HttpResponseMessage response = await client.GetAsync("/__contract/response-started");
                completedAsSuccess = response.StatusCode == HttpStatusCode.OK &&
                    await response.Content.ReadAsStringAsync() == "prefix";
            }
            catch (HttpRequestException)
            {
            }
            catch (OperationCanceledException)
            {
            }
        }

        Assert.False(completedAsSuccess);
        JsonElement[] entries = ParseJsonLogLines(capture.Content);
        Assert.Contains(entries, entry => ContainsStringProperty(entry, "ErrorCode", "internal_error"));
        Assert.DoesNotContain(SecretSentinels.ResponseStartedException, capture.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", capture.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exceptionDetail", capture.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task KestrelNonRequestAbortedOperationCanceledExceptionUsesSafeEnvelopeAndLogging()
    {
        using ConsoleCapture capture = new();

        using (ContractWebApplicationFactory factory = new())
        {
            factory.UseKestrel(0);

            using HttpClient client = factory.CreateClient();
            using HttpResponseMessage response = await client.GetAsync("/__contract/canceled");
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            AssertErrorEnvelope(body, "internal_error", "An internal error occurred.");
            Assert.DoesNotContain(SecretSentinels.InternalCancellationException, body, StringComparison.Ordinal);
        }

        JsonElement[] entries = ParseJsonLogLines(capture.Content);
        Assert.Contains(entries, entry => ContainsStringProperty(entry, "ErrorCode", "internal_error"));
        Assert.DoesNotContain(SecretSentinels.InternalCancellationException, capture.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", capture.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exceptionDetail", capture.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task KestrelRequestAbortIsRethrownWithoutGeneratingAnErrorEnvelopeOrSecretLog()
    {
        using ConsoleCapture capture = new();
        ContractProbeSignals signals = new();

        using (ContractWebApplicationFactory factory = new(
                   services => services.AddSingleton(signals)))
        {
            factory.UseKestrel(0);

            using HttpClient client = factory.CreateClient();
            using CancellationTokenSource cancellation = new();
            using HttpRequestMessage request = new(HttpMethod.Get, "/__contract/client-abort");

            Task<HttpResponseMessage> pending = client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellation.Token);

            await signals.RequestAbortStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
            await signals.RequestAbortObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.DoesNotContain(SecretSentinels.RequestAbortException, capture.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("internal_error", capture.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OperationCanceledExceptionIsRethrownByMiddleware()
    {
        OperationCanceledException expected = new("request canceled");
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        using CancellationTokenSource requestAborted = new();
        requestAborted.Cancel();
        context.RequestAborted = requestAborted.Token;

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

        await middleware.InvokeAsync(context, []);

        Assert.Equal(0, responseFeature.Body.Length);
    }

    private static string GetCorrelationId(HttpResponseMessage response) =>
        Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));

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
public sealed class RuntimeContractController(
    ApplicationTime applicationTime,
    ContractProbeSignals signals) : ControllerBase
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

    [HttpPost("/__contract/media-type")]
    [Consumes("application/json")]
    public IActionResult MediaType([FromBody] ValidationRequest request)
    {
        _ = request;
        return Ok();
    }

    [HttpGet("/__contract/canceled")]
    public IActionResult GetCanceled()
    {
        _ = HttpContext;
        throw new OperationCanceledException(SecretSentinels.InternalCancellationException);
    }

    [HttpGet("/__contract/client-abort")]
    public async Task GetClientAbort()
    {
        signals.RequestAbortStarted.TrySetResult();

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, HttpContext.RequestAborted);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            signals.RequestAbortObserved.TrySetResult();
            throw new OperationCanceledException(
                SecretSentinels.RequestAbortException,
                HttpContext.RequestAborted);
        }
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
            services.TryAddSingleton<ContractProbeSignals>();

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

internal sealed class ThrowingOperationCanceledExceptionMapper : IApiExceptionMapper
{
    public ApiErrorMapping? TryMap(Exception exception)
    {
        _ = exception;
        throw new OperationCanceledException(SecretSentinels.MapperCancellation);
    }
}

internal sealed class ContractProbeException : Exception;

public sealed class ContractProbeSignals
{
    public TaskCompletionSource RequestAbortStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource RequestAbortObserved { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

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
    public const string MapperCancellation = "MAPPER_CANCELLATION_SECRET_SENTINEL";
    public const string InternalCancellationException = "INTERNAL_CANCELLATION_SECRET_SENTINEL";
    public const string RequestAbortException = "REQUEST_ABORT_SECRET_SENTINEL";
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
        MapperCancellation,
        InternalCancellationException,
        RequestAbortException,
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
            lock (synchronizedWriter)
            {
                synchronizedWriter.Flush();
                return buffer.ToString();
            }
        }
    }

    public void Dispose()
    {
        Console.SetOut(originalOutput);
        Console.SetError(originalError);

        lock (synchronizedWriter)
        {
            synchronizedWriter.Dispose();
            buffer.Dispose();
        }
    }
}
