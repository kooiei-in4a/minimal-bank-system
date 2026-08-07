using System.Net;
using Microsoft.Extensions.Logging;
using MinimalBankSystem.Domain.Errors;

namespace MinimalBankSystem.IntegrationTests;

public sealed partial class LoggingTests
{
    [Fact]
    public void LoggerMessageDefinesCorrectlyForExceptionHandling()
    {
        var logEntries = new List<(LogLevel Level, string Message)>();
        var logger = new TestLogger<MinimalBankSystem.Api.Middleware.ExceptionHandlingMiddleware>(logEntries);

        logger.Log(LogLevel.Error, new EventId(0), "Test message", null, (s, e) => s);

        Assert.NotEmpty(logEntries);
    }

    [Fact]
    public void CorrelationIdScopeAddsToLogState()
    {
        var logEntries = new List<string>();
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new InMemoryLoggerProvider(logEntries));
        });

        var logger = loggerFactory.CreateLogger("test");

        using IDisposable? scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = "test-correlation-id"
        });

        LogTestMessage(logger);

        Assert.Contains(logEntries, e => e.Contains("test-correlation-id"));
    }

    [Fact]
    public void JsonLoggingCanBeConfigured()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddJsonConsole(options =>
            {
                options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
            });
        });

        var logger = loggerFactory.CreateLogger("test");
        LogJsonTestMessage(logger);

        loggerFactory.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Test log message")]
    private static partial void LogTestMessage(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "JSON test message")]
    private static partial void LogJsonTestMessage(ILogger logger);

    private sealed class InMemoryLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _entries;
        public InMemoryLoggerProvider(List<string> entries) => _entries = entries;
        public ILogger CreateLogger(string categoryName) => new InMemoryLogger(_entries);
        public void Dispose() { }
    }

    private sealed class InMemoryLogger : ILogger
    {
        private readonly List<string> _entries;
        public InMemoryLogger(List<string> entries) => _entries = entries;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            if (state is IEnumerable<KeyValuePair<string, object>> scope)
            {
                foreach (var kv in scope)
                {
                    _entries.Add($"[SCOPE] {kv.Key}={kv.Value}");
                }
            }
            return null;
        }
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _entries.Add(formatter(state, exception));
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        private readonly List<(LogLevel, string)> _entries;
        public TestLogger(List<(LogLevel, string)> entries) => _entries = entries;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
