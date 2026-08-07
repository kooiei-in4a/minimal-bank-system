using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalBankSystem.Api.CorrelationId;
using MinimalBankSystem.Api.ErrorHandling;
using MinimalBankSystem.Api.Logging;

namespace MinimalBankSystem.IntegrationTests;

public sealed class ApiContractTests : IAsyncLifetime
{
    private IHost? _host;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        _host = await CreateHost();
        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    private static async Task<IHost> CreateHost(TimeProvider? timeProvider = null)
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddSingleton(timeProvider ?? TimeProvider.System);
                    services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
                    services.AddSingleton<IExceptionMapper, DefaultExceptionMapper>();
                    services.AddRouting();
                });
                webHost.Configure(app =>
                {
                    app.UseMiddleware<CorrelationIdMiddleware>();
                    app.UseMiddleware<ExceptionMiddleware>();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("api/test/ok", async context =>
                        {
                            var accessor = context.RequestServices.GetRequiredService<ICorrelationIdAccessor>();
                            await context.Response.WriteAsJsonAsync(new { correlationId = accessor.Current });
                        });

                        endpoints.MapGet("api/test/time", async context =>
                        {
                            var tp = context.RequestServices.GetRequiredService<TimeProvider>();
                            var now = tp.GetUtcNow();
                            await context.Response.WriteAsJsonAsync(new { utcNow = now.ToString("O") });
                        });

                        endpoints.MapGet("api/test/problem", _ =>
                        {
                            throw new ProblemException(400, "validation_failed", "Test validation error.");
                        });

                        endpoints.MapGet("api/test/unhandled", _ =>
                        {
                            throw new InvalidOperationException("Internal detail that must not leak.");
                        });

                        endpoints.MapPost("api/test/log-prohibited-fields", async context =>
                        {
                            var body = await System.Text.Json.JsonSerializer.DeserializeAsync<ProhibitedFieldPayload>(context.Request.Body);
                            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Test");
                            logger.LogInformation(
                                "Test log. Password: {Password}, JWT: {JWT}, SigningKey: {SigningKey}, IdempotencyKey: {IdempotencyKey}, ConnectionString: {ConnectionString}",
                                SensitiveFieldPolicy.IsProhibited("Password") ? "***" : body?.Password ?? "",
                                SensitiveFieldPolicy.IsProhibited("JWT") ? "***" : body?.Jwt ?? "",
                                SensitiveFieldPolicy.IsProhibited("SigningKey") ? "***" : body?.SigningKey ?? "",
                                SensitiveFieldPolicy.IsProhibited("IdempotencyKey") ? "***" : body?.IdempotencyKey ?? "",
                                SensitiveFieldPolicy.IsProhibited("ConnectionString") ? "***" : body?.ConnectionString ?? "");
                            await context.Response.WriteAsJsonAsync(new { logged = true });
                        });
                    });
                });
            });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    [Fact]
    public async Task ErrorEnvelopeHasCorrectStructureForMappedProblemException()
    {
        var response = await _client!.GetAsync("/api/test/problem");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonNode>();
        Assert.NotNull(body);

        string? code = body!["code"]?.GetValue<string>();
        string? message = body["message"]?.GetValue<string>();

        Assert.Equal("validation_failed", code);
        Assert.Equal("Test validation error.", message);
    }

    [Fact]
    public async Task UnmappedExceptionReturns500WithSafeEnvelope()
    {
        var response = await _client!.GetAsync("/api/test/unhandled");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonNode>();
        Assert.NotNull(body);

        string? code = body!["code"]?.GetValue<string>();
        string? message = body["message"]?.GetValue<string>();
        string? responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal("internal_server_error", code);
        Assert.Equal("An internal error occurred.", message);
        Assert.DoesNotContain("Internal detail that must not leak.", responseBody);
        Assert.DoesNotContain("InvalidOperationException", responseBody);
    }

    [Fact]
    public async Task CorrelationIdGeneratedWhenNotSupplied()
    {
        var response = await _client!.GetAsync("/api/test/ok");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-Id"));

        var correlationId = response.Headers.GetValues("X-Correlation-Id").First();
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
        Assert.True(Guid.TryParse(correlationId, out _));
    }

    [Fact]
    public async Task CorrelationIdPropagatedWhenValidCallerSupplied()
    {
        var callerId = "test-correlation-abc123";
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/test/ok");
        request.Headers.Add("X-Correlation-Id", callerId);

        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseCorrelationId = response.Headers.GetValues("X-Correlation-Id").First();
        Assert.Equal(callerId, responseCorrelationId);
    }

    [Fact]
    public async Task CorrelationIdRejectedWhenContainsDangerousCharacters()
    {
        var dangerousId = "<script>alert('xss')</script>";
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/test/ok");
        request.Headers.Add("X-Correlation-Id", dangerousId);

        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseCorrelationId = response.Headers.GetValues("X-Correlation-Id").First();
        Assert.NotEqual(dangerousId, responseCorrelationId);
        Assert.True(Guid.TryParse(responseCorrelationId, out _));
    }

    [Fact]
    public async Task CorrelationIdRejectedWhenExceedsMaxLength()
    {
        var tooLong = new string('a', 200);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/test/ok");
        request.Headers.Add("X-Correlation-Id", tooLong);

        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseCorrelationId = response.Headers.GetValues("X-Correlation-Id").First();
        Assert.NotEqual(tooLong, responseCorrelationId);
    }

    [Fact]
    public async Task TimeProviderReturnsInjectedTime()
    {
        var fixedTime = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        using var customHost = await CreateHost(new FakeTimeProvider(fixedTime));
        using var customClient = customHost.GetTestClient();

        var response = await customClient.GetAsync("/api/test/time");
        var body = await response.Content.ReadFromJsonAsync<JsonNode>();

        Assert.NotNull(body);
        string? utcNow = body!["utcNow"]?.GetValue<string>();
        Assert.NotNull(utcNow);

        var parsed = DateTimeOffset.Parse(utcNow, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(fixedTime, parsed);
    }

    [Fact]
    public async Task ExceptionDetailNotExposedInApiResponse()
    {
        var response = await _client!.GetAsync("/api/test/unhandled");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("stack trace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at MinimalBankSystem", body);
        Assert.DoesNotContain("System.InvalidOperationException", body);
    }

    [Fact]
    public async Task NoBusinessMappingOnlyGenericContract()
    {
        var response = await _client!.GetAsync("/api/test/problem");
        var body = await response.Content.ReadFromJsonAsync<JsonNode>();

        Assert.NotNull(body);
        string? code = body!["code"]?.GetValue<string>();

        Assert.Equal("validation_failed", code);
        Assert.DoesNotContain("customer", code, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("account", code, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ErrorEnvelopeContentTypeIsJson()
    {
        var response = await _client!.GetAsync("/api/test/problem");

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ProhibitedFieldSentinelsNotLeakedViaApiResponse()
    {
        var payload = new
        {
            Password = "SENTINEL-PASSWORD-xyz789!",
            Jwt = "SENTINEL-JWT-eyJhbGciOi.test.signature",
            SigningKey = "SENTINEL-SIGNINGKEY-ABCDEF123456",
            IdempotencyKey = "SENTINEL-IDEMPOTENCY-key-98765",
            ConnectionString = "Host=secret-db;Password=SENTINEL-CONN;Database=test",
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _client!.PostAsync("/api/test/log-prohibited-fields", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(payload.Password, body);
        Assert.DoesNotContain(payload.Jwt, body);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _fixedTime;
        public FakeTimeProvider(DateTimeOffset fixedTime) => _fixedTime = fixedTime;
        public override DateTimeOffset GetUtcNow() => _fixedTime;
    }

    private sealed class ProhibitedFieldPayload
    {
        public string Password { get; set; } = string.Empty;
        public string Jwt { get; set; } = string.Empty;
        public string SigningKey { get; set; } = string.Empty;
        public string IdempotencyKey { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
    }
}
