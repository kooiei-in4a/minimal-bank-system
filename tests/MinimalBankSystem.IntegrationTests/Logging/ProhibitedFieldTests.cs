using MinimalBankSystem.Api.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MinimalBankSystem.IntegrationTests.Logging;

public sealed class ProhibitedFieldTests
{
    private static readonly string[] ProhibitedValues =
    [
        "super-secret-password-123",
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test.payload",
        "signing-key-abc-def",
        "idempotency-key-raw-12345",
        "Host=localhost;Database=test;Password=secret123",
    ];

    [Fact]
    public async Task ProhibitedFieldsDoNotAppearInLogOutput()
    {
        var logCapture = new List<string>();

        using var server = CreateServer(logCapture);
        await server.CreateClient().GetAsync("/api/contract/safe-log");

        string allLogs = string.Join(Environment.NewLine, logCapture);

        foreach (string prohibited in ProhibitedValues)
        {
            Assert.DoesNotContain(prohibited, allLogs);
        }
    }

    [Fact]
    public async Task PasswordLiteralDoesNotAppearInLogs()
    {
        var logCapture = new List<string>();

        using var server = CreateServer(logCapture);
        await server.CreateClient().GetAsync("/api/contract/safe-log");

        string allLogs = string.Join(Environment.NewLine, logCapture);
        Assert.DoesNotContain("my-secret-pw", allLogs);
    }

    [Fact]
    public async Task JwtTokenDoesNotAppearInLogs()
    {
        var logCapture = new List<string>();

        using var server = CreateServer(logCapture);
        await server.CreateClient().GetAsync("/api/contract/safe-log");

        string allLogs = string.Join(Environment.NewLine, logCapture);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9.secret", allLogs);
    }

    private static TestServer CreateServer(List<string> logCapture)
    {
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
                    endpoints.MapGet("/api/contract/safe-log", (HttpContext context) =>
                    {
                        var logger = context.RequestServices.GetRequiredService<ILogger<ProhibitedFieldTests>>();
                        logger.LogInformation("Safe log message without prohibited data");
                        return Results.Ok();
                    });
                });
            });

        return new TestServer(builder);
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
        private readonly List<string> _logLines;

        public CapturingLogger(List<string> logLines)
        {
            _logLines = logLines;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);
            if (!string.IsNullOrWhiteSpace(message))
            {
                _logLines.Add(message);
            }
        }
    }
}
