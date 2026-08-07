using System.Text.Json;

namespace MinimalBankSystem.IntegrationTests.ApiRuntime;

/// <summary>
/// A single technical log event read back from the JSON console stream.
/// </summary>
internal sealed record TechnicalLogEntry(JsonElement Root)
{
    public string Category => Root.GetProperty(nameof(Category)).GetString() ?? string.Empty;

    public string LogLevel => Root.GetProperty(nameof(LogLevel)).GetString() ?? string.Empty;

    /// <summary>
    /// Reads every line of the captured console stream as JSON. A line that is not valid JSON fails
    /// the read, which is what proves the console output is machine readable end to end.
    /// </summary>
    public static IReadOnlyList<TechnicalLogEntry> ReadAll(string consoleOutput)
    {
        List<TechnicalLogEntry> entries = [];

        foreach (string line in consoleOutput.Split('\n'))
        {
            string candidate = line.Trim();
            if (candidate.Length == 0)
            {
                continue;
            }

            using JsonDocument document = JsonDocument.Parse(candidate);
            entries.Add(new TechnicalLogEntry(document.RootElement.Clone()));
        }

        return entries;
    }

    public string? GetStateString(string name) =>
        Root.TryGetProperty("State", out JsonElement state) && state.TryGetProperty(name, out JsonElement value)
            ? value.ToString()
            : null;

    public bool HasScopeValue(string name, string expected) =>
        Root.TryGetProperty("Scopes", out JsonElement scopes)
        && scopes.EnumerateArray().Any(scope =>
            scope.ValueKind == JsonValueKind.Object
            && scope.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() == expected);
}
