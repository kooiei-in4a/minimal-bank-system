using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MinimalBankSystem.Api.Runtime;

namespace MinimalBankSystem.IntegrationTests.Infrastructure;

public sealed class CollectingLoggerProvider : ILoggerProvider
{
    private readonly List<CollectedLogEntry> _entries = [];
    private readonly Lock _sync = new();
    private readonly LoggerExternalScopeProvider _scopeProvider = new();

    public IReadOnlyList<CollectedLogEntry> Entries
    {
        get
        {
            lock (_sync)
            {
                return _entries.ToArray();
            }
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _entries.Clear();
        }
    }

    public ILogger CreateLogger(string categoryName) =>
        new CollectingLogger(categoryName, this, _scopeProvider);

    public void Dispose()
    {
    }

    internal void Add(CollectedLogEntry entry)
    {
        lock (_sync)
        {
            _entries.Add(entry);
        }
    }

    private sealed class CollectingLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly CollectingLoggerProvider _provider;
        private readonly LoggerExternalScopeProvider _scopeProvider;

        public CollectingLogger(
            string categoryName,
            CollectingLoggerProvider provider,
            LoggerExternalScopeProvider scopeProvider)
        {
            _categoryName = categoryName;
            _provider = provider;
            _scopeProvider = scopeProvider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return _scopeProvider.Push(state);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Dictionary<string, object?> properties = new(StringComparer.Ordinal);

            _scopeProvider.ForEachScope(
                static (scope, properties) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object>> objectState)
                    {
                        foreach (KeyValuePair<string, object> entry in objectState)
                        {
                            properties[entry.Key] = entry.Value;
                        }
                    }
                    else if (scope is IDictionary<string, object?> nullableDictionary)
                    {
                        foreach (KeyValuePair<string, object?> entry in nullableDictionary)
                        {
                            properties[entry.Key] = entry.Value;
                        }
                    }
                },
                properties);

            if (state is IEnumerable<KeyValuePair<string, object?>> structuredState)
            {
                foreach (KeyValuePair<string, object?> entry in TechnicalLogFieldPolicy.SanitizeState(structuredState))
                {
                    properties[entry.Key] = entry.Value;
                }
            }

            _provider.Add(new CollectedLogEntry(
                _categoryName,
                logLevel,
                formatter(state, exception),
                exception,
                properties));
        }
    }
}

public sealed record CollectedLogEntry(
    string Category,
    LogLevel Level,
    string Message,
    Exception? Exception,
    IReadOnlyDictionary<string, object?> Properties);
