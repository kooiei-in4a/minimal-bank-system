using System.Text.Json;
using MinimalBankSystem.Api.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MinimalBankSystem.IntegrationTests.Logging;

public sealed class JsonConsoleOutputTests
{
    [Fact]
    public async Task LoggingOutputIsConfiguredAsJson()
    {
        var logCapture = new List<string>();

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddMinimalBankSystemApi();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddJsonConsole();
                logging.AddProvider(new CapturingLoggerProvider(logCapture));
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseMinimalBankSystemApi();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/api/contract/echo", async (HttpContext context) =>
                    {
                        var logger = context.RequestServices.GetRequiredService<ILogger<JsonConsoleOutputTests>>();
                        logger.LogInformation("Test log message");
                        await context.Response.WriteAsJsonAsync(new { Ok = true });
                    });
                });
            });

        using var server = new TestServer(builder);
        await server.CreateClient().GetAsync("/api/contract/echo");

        Assert.NotEmpty(logCapture);
    }

    [Fact]
    public async Task LogOutputContainsCorrelationId()
    {
        var logCapture = new List<string>();

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddMinimalBankSystemApi();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddJsonConsole();
                logging.AddProvider(new CapturingLoggerProvider(logCapture));
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseMinimalBankSystemApi();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/api/contract/echo", async (HttpContext context) =>
                    {
                        var logger = context.RequestServices.GetRequiredService<ILogger<JsonConsoleOutputTests>>();
                        logger.LogInformation("Test log message");
                        await context.Response.WriteAsJsonAsync(new { Ok = true });
                    });
                });
            });

        using var server = new TestServer(builder);
        await server.CreateClient().GetAsync("/api/contract/echo");

        bool foundCorrelationId = logCapture.Any(line =>
            line.Contains("CorrelationId", StringComparison.OrdinalIgnoreCase));

        Assert.True(foundCorrelationId, $"Expected at least one log line containing CorrelationId. Got {logCapture.Count} lines.");
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _logLines;

        public CapturingLoggerProvider(List<string> logLines)
        {
            _logLines = logLines;
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_logLines);

        public void Dispose() { }
    }

    private sealed class CapturingLogger : ILogger
    {
        private static readonly AsyncLocal<Dictionary<string, object?>?> CurrentScope = new();

        private readonly List<string> _logLines;

        public CapturingLogger(List<string> logLines)
        {
            _logLines = logLines;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            var parent = CurrentScope.Value;
            var newScope = new Dictionary<string, object?>(parent ?? []);

            if (state is IReadOnlyList<KeyValuePair<string, object?>> pairs)
            {
                foreach (var pair in pairs)
                {
                    newScope[pair.Key] = pair.Value;
                }
            }
            else if (state is IDictionary<string, object?> dict)
            {
                foreach (var kvp in dict)
                {
                    newScope[kvp.Key] = kvp.Value;
                }
            }

            var previous = CurrentScope.Value;
            CurrentScope.Value = newScope;
            return new ScopeDisposable(() => CurrentScope.Value = previous);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);
            var scope = CurrentScope.Value;
            if (scope is { Count: > 0 })
            {
                var scopeJson = JsonSerializer.Serialize(scope);
                message = $"{message} {scopeJson}";
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                _logLines.Add(message);
            }
        }

        private sealed class ScopeDisposable(Action onDispose) : IDisposable
        {
            public void Dispose() => onDispose();
        }
    }
}
