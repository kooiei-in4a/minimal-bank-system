using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MinimalBankSystem.IntegrationTests.TestInfrastructure;

public sealed class CapturedLogLine
{
    public required string CategoryName { get; init; }

    public required LogLevel LogLevel { get; init; }

    public required string Json { get; init; }
}

public sealed class CapturedJsonLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<CapturedLogLine> _lines = new();
    private IExternalScopeProvider? _scopeProvider;

    public IExternalScopeProvider? ScopeProvider => _scopeProvider;

    public ILogger CreateLogger(string categoryName) => new CapturedJsonLogger(this, categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    internal void WriteLine<TState>(string categoryName, LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        JsonObject line = new()
        {
            ["Category"] = categoryName,
            ["Level"] = logLevel.ToString(),
            ["EventId"] = new JsonObject
            {
                ["Id"] = eventId.Id,
                ["Name"] = eventId.Name ?? string.Empty,
            },
            ["Scopes"] = FormatScopes(_scopeProvider),
            ["Message"] = formatter(state, exception),
        };

        if (state is IEnumerable<KeyValuePair<string, object?>> stateProperties)
        {
            foreach (KeyValuePair<string, object?> property in stateProperties)
            {
                line[property.Key] = SerializeValue(property.Key, property.Value);
            }
        }

        _lines.Enqueue(new CapturedLogLine
        {
            CategoryName = categoryName,
            LogLevel = logLevel,
            Json = line.ToJsonString(),
        });
    }

    public IReadOnlyList<CapturedLogLine> Snapshot() => _lines.ToArray();

    public void Clear() => _lines.Clear();

    public void Dispose()
    {
    }

    private static JsonObject FormatScopes(IExternalScopeProvider? scopeProvider)
    {
        JsonObject scopes = new();
        scopeProvider?.ForEachScope((scope, writer) =>
        {
            if (scope is IEnumerable<KeyValuePair<string, object?>> scopeProperties)
            {
                foreach (KeyValuePair<string, object?> property in scopeProperties)
                {
                    writer[property.Key] = SerializeValue(property.Key, property.Value);
                }
            }
        }, scopes);
        return scopes;
    }

    private static JsonNode? SerializeValue(string key, object? value)
    {
        try
        {
            return JsonSerializer.SerializeToNode(value);
        }
        catch (NotSupportedException)
        {
            return JsonValue.Create($"<unserializable:{key}:{value?.GetType().Name}>");
        }
    }
}

public sealed class CapturedJsonLogger : ILogger
{
    private readonly CapturedJsonLoggerProvider _provider;
    private readonly string _categoryName;

    public CapturedJsonLogger(CapturedJsonLoggerProvider provider, string categoryName)
    {
        _provider = provider;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => _provider.ScopeProvider?.Push(state);

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _provider.WriteLine(_categoryName, logLevel, eventId, state, exception, formatter);
    }
}
