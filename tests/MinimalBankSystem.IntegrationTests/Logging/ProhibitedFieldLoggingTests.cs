using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using MinimalBankSystem.Api.Extensions;
using Xunit;

namespace MinimalBankSystem.IntegrationTests.Logging;

public sealed class ProhibitedFieldLoggingTests
{
    private const string SentinelPassword = "TEST_SENTINEL_PW_xx9Q";
    private const string SentinelJwt = "TEST_SENTINEL_JWT_eyJhbGciOiJIUzI1NiJ9.payload.sig";
    private const string SentinelSigningKey = "TEST_SENTINEL_SIGNING_KEY_abc123";
    private const string SentinelIdempotency = "TEST_SENTINEL_IDEMPOTENCY_20260808";
    private const string SentinelConnection = "TEST_SENTINEL_CONNSTR_Host=db;Password=xx";

    [Fact]
    public async Task ProhibitedFieldsAreNotEchoedInMappedExceptionLogEvent()
    {
        var sink = new CapturingProvider();
        using IHost host = BuildHost(sink);
        using HttpClient client = host.GetTestClient();

        using HttpResponseMessage response = await client.GetAsync("/_contract/boom-with-sentinels");
        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);

        // 1. Response must not echo the sentinels.
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SentinelPassword, body);
        Assert.DoesNotContain(SentinelJwt, body);
        Assert.DoesNotContain(SentinelSigningKey, body);
        Assert.DoesNotContain(SentinelIdempotency, body);
        Assert.DoesNotContain(SentinelConnection, body);

        // 2. Captured technical log lines must not echo the sentinels either.
        // The middleware logs a structured event with the fixed code and correlation id, not the
        // raw exception message. If a future change accidentally surfaces the exception text,
        // this assertion will fail.
        foreach (string line in sink.Lines)
        {
            Assert.DoesNotContain(SentinelPassword, line);
            Assert.DoesNotContain(SentinelJwt, line);
            Assert.DoesNotContain(SentinelSigningKey, line);
            Assert.DoesNotContain(SentinelIdempotency, line);
            Assert.DoesNotContain(SentinelConnection, line);
        }
    }

    [Fact]
    public async Task FixedErrorCodeAndCorrelationIdArePresentInCapturedLog()
    {
        var sink = new CapturingProvider();
        using IHost host = BuildHost(sink);
        using HttpClient client = host.GetTestClient();

        using HttpRequestMessage request = new(HttpMethod.Get, "/_contract/boom-with-sentinels");
        request.Headers.Add("X-Correlation-Id", "fix-code-trace-9");
        using HttpResponseMessage response = await client.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);

        bool found = false;
        foreach (string line in sink.Lines)
        {
            if (line.Contains("internal_error", StringComparison.Ordinal) &&
                line.Contains("fix-code-trace-9", StringComparison.Ordinal))
            {
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected at least one log line to include both 'internal_error' and the correlation id.");
    }

    private static IHost BuildHost(CapturingProvider sink)
    {
        return new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddMinimalBankSystemRuntime();
                        services.AddSingleton<ILoggerProvider>(sink);
                    })
                    .ConfigureLogging((_, logging) =>
                    {
                        logging.ClearProviders();
                        logging.AddJsonConsole();
                        logging.AddProvider(sink);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseMinimalBankSystemRuntime();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/_contract/boom-with-sentinels", () =>
                            {
                                throw new InvalidOperationException(
                                    $"{SentinelPassword}|{SentinelJwt}|{SentinelSigningKey}|{SentinelIdempotency}|{SentinelConnection}");
                            });
                        });
                    });
            })
            .Start();
    }

    private sealed class CapturingProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _lines = new();

        public IReadOnlyCollection<string> Lines => _lines;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_lines);

        public void Dispose() { }

        private sealed class CapturingLogger : ILogger
        {
            private readonly ConcurrentQueue<string> _sink;

            public CapturingLogger(ConcurrentQueue<string> sink)
            {
                _sink = sink;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                if (state is IReadOnlyList<KeyValuePair<string, object?>> pairs)
                {
                    var captured = new Dictionary<string, object?>();
                    foreach (KeyValuePair<string, object?> pair in pairs)
                    {
                        captured[pair.Key] = pair.Value;
                    }
                    return new Scope(captured);
                }
                return null;
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                string line = formatter(state, exception);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _sink.Enqueue(line);
                }
            }

            private sealed class Scope : IDisposable
            {
                public Scope(Dictionary<string, object?> _)
                {
                }

                public void Dispose()
                {
                }
            }
        }
    }
}
