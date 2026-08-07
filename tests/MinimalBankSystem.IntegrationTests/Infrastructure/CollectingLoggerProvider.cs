using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MinimalBankSystem.IntegrationTests.Infrastructure;

public sealed class CollectingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyCollection<string> Entries => _entries.ToArray();

    public string CombinedText => string.Join(Environment.NewLine, Entries);

    public ILogger CreateLogger(string categoryName) => new CollectingLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    public void Clear()
    {
        while (_entries.TryDequeue(out _))
        {
        }
    }

    private sealed class CollectingLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly ConcurrentQueue<string> _entries;
        private readonly AsyncLocal<Stack<string>> _scopes = new();

        public CollectingLogger(string categoryName, ConcurrentQueue<string> entries)
        {
            _categoryName = categoryName;
            _entries = entries;
        }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            Stack<string> stack = _scopes.Value ??= new Stack<string>();
            string rendered = state switch
            {
                IEnumerable<KeyValuePair<string, object?>> pairs => string.Join(
                    ", ",
                    pairs.Select(pair => $"{pair.Key}={pair.Value}")),
                _ => state.ToString() ?? string.Empty,
            };
            stack.Push(rendered);
            return new Scope(stack);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);
            string scopes = _scopes.Value is { Count: > 0 } stack
                ? string.Join(" | ", stack.Reverse())
                : string.Empty;

            _entries.Enqueue($"{logLevel} {_categoryName} {message} :: {scopes}");

            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                foreach (KeyValuePair<string, object?> pair in values)
                {
                    _entries.Enqueue($"STATE {pair.Key}={pair.Value}");
                }
            }
        }

        private sealed class Scope : IDisposable
        {
            private readonly Stack<string> _stack;
            private bool _disposed;

            public Scope(Stack<string> stack)
            {
                _stack = stack;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                if (_stack.Count > 0)
                {
                    _stack.Pop();
                }
            }
        }
    }
}
