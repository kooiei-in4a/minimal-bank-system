using System.Globalization;
using System.Text.Json;

namespace MinimalBankSystem.IntegrationTests.Compose;

internal sealed record ComposeServiceState(
    string Service,
    string? Id,
    string? Name,
    string State,
    string? Status,
    int? ExitCode,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    bool HasEverStarted)
{
    public bool IsRunning =>
        string.Equals(State, "running", StringComparison.OrdinalIgnoreCase);

    public bool IsExited =>
        string.Equals(State, "exited", StringComparison.OrdinalIgnoreCase)
        || string.Equals(State, "dead", StringComparison.OrdinalIgnoreCase);

    public bool NeverStarted => !HasEverStarted;
}

internal static class ComposeObservations
{
    public static IReadOnlyList<JsonElement> ParsePsJsonLines(string psJson)
    {
        List<JsonElement> items = [];

        if (string.IsNullOrWhiteSpace(psJson))
        {
            return items;
        }

        string trimmed = psJson.TrimStart();
        if (trimmed.StartsWith('['))
        {
            using JsonDocument document = JsonDocument.Parse(trimmed);
            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                items.Add(element.Clone());
            }

            return items;
        }

        // Compose may emit NDJSON for --format json.
        using StringReader reader = new(psJson);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using JsonDocument document = JsonDocument.Parse(line);
            items.Add(document.RootElement.Clone());
        }

        return items;
    }

    public static ComposeServiceState? FindService(
        IReadOnlyList<JsonElement> psItems,
        string serviceName)
    {
        foreach (JsonElement item in psItems)
        {
            string? service = ReadString(item, "Service") ?? ReadString(item, "Name");
            if (!string.Equals(service, serviceName, StringComparison.OrdinalIgnoreCase)
                && !ServiceNameMatches(item, serviceName))
            {
                continue;
            }

            string? id = ReadString(item, "ID") ?? ReadString(item, "Id");
            string state = ReadString(item, "State") ?? "unknown";
            string? status = ReadString(item, "Status");
            int? exitCode = ReadInt(item, "ExitCode");

            return new ComposeServiceState(
                serviceName,
                id,
                ReadString(item, "Name"),
                state,
                status,
                exitCode,
                StartedAt: null,
                FinishedAt: null,
                HasEverStarted: !string.Equals(state, "created", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(state, "unknown", StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    public static async Task<ComposeServiceState> InspectServiceAsync(
        ComposeServiceState? fromPs,
        string serviceName)
    {
        if (fromPs?.Id is null)
        {
            return new ComposeServiceState(
                serviceName,
                Id: null,
                Name: null,
                State: "absent",
                Status: null,
                ExitCode: null,
                StartedAt: null,
                FinishedAt: null,
                HasEverStarted: false);
        }

        using JsonDocument inspect = await DockerCli.InspectContainerAsync(fromPs.Id);
        JsonElement root = inspect.RootElement[0];
        JsonElement state = root.GetProperty("State");

        string status = state.GetProperty("Status").GetString() ?? "unknown";
        int exitCode = state.GetProperty("ExitCode").GetInt32();
        DateTimeOffset? startedAt = ParseDockerTime(state.GetProperty("StartedAt").GetString());
        DateTimeOffset? finishedAt = ParseDockerTime(state.GetProperty("FinishedAt").GetString());

        bool hasEverStarted = startedAt is not null
            && startedAt != DateTimeOffset.MinValue
            && startedAt.Value.Year > 1;

        return new ComposeServiceState(
            serviceName,
            fromPs.Id,
            fromPs.Name,
            status,
            status,
            exitCode,
            startedAt,
            finishedAt,
            hasEverStarted);
    }

    public static string ResolveNamedVolumeName(string projectName) =>
        $"{projectName}_{ComposeContracts.NamedVolumeLogicalName}";

    public static bool ConfigContainsDigestQualifiedPostgres(JsonElement configRoot)
    {
        if (!TryGetService(configRoot, ComposeContracts.PostgresServiceName, out JsonElement postgres))
        {
            return false;
        }

        string? image = ReadString(postgres, "image") ?? ReadString(postgres, "Image");
        return image is not null
            && image.Contains(ComposeContracts.PostgresImageDigest, StringComparison.Ordinal);
    }

    public static bool ConfigUsesNamedPostgresVolume(JsonElement configRoot)
    {
        if (!TryGetService(configRoot, ComposeContracts.PostgresServiceName, out JsonElement postgres))
        {
            return false;
        }

        if (!postgres.TryGetProperty("volumes", out JsonElement volumes)
            && !postgres.TryGetProperty("Volumes", out volumes))
        {
            return false;
        }

        foreach (JsonElement volume in volumes.EnumerateArray())
        {
            // Rendered compose JSON may use Source/Target or string form.
            string? source = ReadString(volume, "source") ?? ReadString(volume, "Source");
            string? type = ReadString(volume, "type") ?? ReadString(volume, "Type");
            if (source is not null
                && source.Contains(ComposeContracts.NamedVolumeLogicalName, StringComparison.Ordinal)
                && (type is null || string.Equals(type, "volume", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (volume.ValueKind == JsonValueKind.String)
            {
                string text = volume.GetString() ?? string.Empty;
                if (text.Contains(ComposeContracts.NamedVolumeLogicalName, StringComparison.Ordinal)
                    && !text.StartsWith('/')
                    && !text.Contains(":\\", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool ConfigRendersSecretLiteral(string configJson, string sentinel) =>
        configJson.Contains(sentinel, StringComparison.Ordinal);

    private static bool TryGetService(
        JsonElement configRoot,
        string serviceName,
        out JsonElement service)
    {
        if (configRoot.TryGetProperty("services", out JsonElement services)
            && services.TryGetProperty(serviceName, out service))
        {
            return true;
        }

        service = default;
        return false;
    }

    private static bool ServiceNameMatches(JsonElement item, string serviceName)
    {
        string? labelsService = null;
        if (item.TryGetProperty("Labels", out JsonElement labels)
            && labels.ValueKind == JsonValueKind.Object
            && labels.TryGetProperty("com.docker.compose.service", out JsonElement labelValue))
        {
            labelsService = labelValue.GetString();
        }

        return string.Equals(labelsService, serviceName, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt32(),
            JsonValueKind.String when int.TryParse(value.GetString(), out int parsed) => parsed,
            _ => null,
        };
    }

    private static DateTimeOffset? ParseDockerTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.StartsWith("0001-01-01", StringComparison.Ordinal))
        {
            return null;
        }

        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}
