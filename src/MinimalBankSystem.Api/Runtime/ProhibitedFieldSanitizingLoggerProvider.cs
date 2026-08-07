namespace MinimalBankSystem.Api.Runtime;

internal sealed class ProhibitedFieldSanitizingLoggerProvider : ILoggerProvider, IDisposable
{
    private readonly ILoggerFactory _innerFactory;

    public ProhibitedFieldSanitizingLoggerProvider()
    {
        _innerFactory = LoggerFactory.Create(loggingBuilder =>
        {
            loggingBuilder.AddJsonConsole(options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "O";
            });
        });
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new ProhibitedFieldSanitizingLogger(_innerFactory.CreateLogger(categoryName));
    }

    public void Dispose()
    {
        _innerFactory.Dispose();
    }

    private sealed class ProhibitedFieldSanitizingLogger : ILogger
    {
        private readonly ILogger _innerLogger;

        public ProhibitedFieldSanitizingLogger(ILogger innerLogger)
        {
            _innerLogger = innerLogger;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            if (state is IReadOnlyList<KeyValuePair<string, object?>> structuredState)
            {
                return _innerLogger.BeginScope(TechnicalLogFieldPolicy.SanitizeState(structuredState));
            }

            return _innerLogger.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel) => _innerLogger.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!_innerLogger.IsEnabled(logLevel))
            {
                return;
            }

            if (state is Dictionary<string, object?> dictionaryState)
            {
                IReadOnlyList<KeyValuePair<string, object?>> sanitizedState =
                    TechnicalLogFieldPolicy.SanitizeState(dictionaryState);
                string structuredMessage = TechnicalLogFieldPolicy.SanitizeMessage(
                    formatter(state, exception));
                _innerLogger.Log(logLevel, eventId, sanitizedState, exception, (_, _) => structuredMessage);
                return;
            }

            _innerLogger.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}

internal static class ProhibitedFieldSanitizingLoggerExtensions
{
    public static ILoggingBuilder AddProhibitedFieldSanitizingJsonConsole(this ILoggingBuilder builder)
    {
        builder.ClearProviders();
        builder.AddProvider(new ProhibitedFieldSanitizingLoggerProvider());
        return builder;
    }
}
