using System.Text.Json;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.IntegrationTests.Persistence;

namespace MinimalBankSystem.IntegrationTests.Compose;

/// <summary>
/// Isolated Compose project session with deterministic cleanup (D-04 / D-06).
/// </summary>
internal sealed class ComposeProjectSession : IAsyncDisposable
{
    private readonly ComposeCli cli;
    private readonly string password;
    private bool cleaned;

    private ComposeProjectSession(ComposeCli cli, string password)
    {
        this.cli = cli;
        this.password = password;
    }

    public ComposeCli Cli => cli;

    public string Password => password;

    public string ProjectName => cli.ProjectName;

    public static ComposeProjectSession Create(
        string? projectName = null,
        string? password = null)
    {
        string resolvedProject = projectName
            ?? $"mbs-fnd05-{Guid.NewGuid():N}"[..20].ToLowerInvariant();
        string resolvedPassword = password ?? ComposeContracts.SecretSentinel;

        Dictionary<string, string> environment = new(StringComparer.Ordinal)
        {
            [ComposeContracts.DatabasePasswordEnvironmentVariable] = resolvedPassword,
        };

        ComposeCli composeCli = new(
            resolvedProject,
            RepositoryLayout.RepositoryRoot.FullName,
            environment);

        return new ComposeProjectSession(composeCli, resolvedPassword);
    }

    public async Task EnsureValidatedAsync(CancellationToken cancellationToken = default)
    {
        ComposeCommandResult result = await cli.ConfigQuietAsync(cancellationToken);
        Assert.True(result.ExitCode == 0, $"compose config --quiet failed:\n{result.Output}");
    }

    public async Task<JsonDocument> LoadConfigDocumentAsync(CancellationToken cancellationToken = default)
    {
        string json = await cli.ConfigJsonAsync(cancellationToken);
        return JsonDocument.Parse(json);
    }

    public async Task CleanResetAsync(CancellationToken cancellationToken = default)
    {
        ComposeCommandResult result = await cli.DownCleanResetAsync(cancellationToken);
        // Compose down should remove project resources; force-remove stragglers by project identity
        // so a previous local probe cannot poison the next run.
        await ForceRemoveProjectResiduesAsync(cancellationToken);

        Assert.True(
            result.ExitCode == 0,
            $"clean reset failed ({result.ExitCode}):\n{result.Output}");
        cleaned = true;
    }

    private async Task ForceRemoveProjectResiduesAsync(CancellationToken cancellationToken)
    {
        ComposeCommandResult containers = await DockerCli.RunRawAsync(
            ["ps", "-aq", "--filter", $"label=com.docker.compose.project={ProjectName}"],
            cancellationToken);
        foreach (string id in containers.StandardOutput.Split(
                     ['\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await DockerCli.RunRawAsync(["rm", "-f", id], cancellationToken);
        }

        string volumeName = ComposeObservations.ResolveNamedVolumeName(ProjectName);
        await DockerCli.RunRawAsync(["volume", "rm", "-f", volumeName], cancellationToken);
        await DockerCli.RunRawAsync(["network", "rm", $"{ProjectName}_runtime"], cancellationToken);
    }

    public async Task StopRetainDataAsync(CancellationToken cancellationToken = default)
    {
        ComposeCommandResult result = await cli.DownRetainDataAsync(cancellationToken);
        Assert.True(
            result.ExitCode == 0,
            $"stop retain-data failed ({result.ExitCode}):\n{result.Output}");
    }

    public async Task<ComposeCommandResult> StartAsync(
        IReadOnlyList<string>? extraComposeFiles = null,
        IReadOnlyDictionary<string, string>? extraEnvironment = null,
        CancellationToken cancellationToken = default)
    {
        if (extraComposeFiles is null && extraEnvironment is null)
        {
            return await cli.UpBuildDetachAsync(cancellationToken);
        }

        return await cli.RunAsync(
            ["up", "--build", "--detach", "--remove-orphans"],
            extraEnvironment,
            extraComposeFiles,
            cancellationToken);
    }

    public async Task WaitUntilMigratorTerminalAsync(
        TimeSpan budget,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + budget;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<JsonElement> ps = ComposeObservations.ParsePsJsonLines(
                await cli.PsJsonAsync(cancellationToken));
            ComposeServiceState? migrator = ComposeObservations.FindService(
                ps,
                ComposeContracts.MigratorServiceName);

            if (migrator?.Id is not null)
            {
                ComposeServiceState inspected = await ComposeObservations.InspectServiceAsync(
                    migrator,
                    ComposeContracts.MigratorServiceName);

                if (!inspected.IsRunning && inspected.HasEverStarted)
                {
                    return;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        throw new TimeoutException(
            $"Migrator did not reach a terminal state within {budget} for project '{ProjectName}'.");
    }

    public async Task WaitUntilApiRunningAsync(
        TimeSpan budget,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + budget;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ComposeRuntimeSnapshot snapshot = await CaptureSnapshotAsync(cancellationToken);
            if (snapshot.Api.IsRunning && snapshot.Api.HasEverStarted)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        ComposeRuntimeSnapshot finalSnapshot = await CaptureSnapshotAsync(cancellationToken);
        throw new TimeoutException(
            $"API did not become running within {budget}. API state={finalSnapshot.Api.State}, " +
            $"migrator exit={finalSnapshot.Migrator.ExitCode}.");
    }

    public async Task<ComposeRuntimeSnapshot> CaptureSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<JsonElement> ps = ComposeObservations.ParsePsJsonLines(
            await cli.PsJsonAsync(cancellationToken));

        ComposeServiceState postgres = await ComposeObservations.InspectServiceAsync(
            ComposeObservations.FindService(ps, ComposeContracts.PostgresServiceName),
            ComposeContracts.PostgresServiceName);
        ComposeServiceState migrator = await ComposeObservations.InspectServiceAsync(
            ComposeObservations.FindService(ps, ComposeContracts.MigratorServiceName),
            ComposeContracts.MigratorServiceName);
        ComposeServiceState api = await ComposeObservations.InspectServiceAsync(
            ComposeObservations.FindService(ps, ComposeContracts.ApiServiceName),
            ComposeContracts.ApiServiceName);

        string migratorLogs = await cli.LogsAsync(ComposeContracts.MigratorServiceName, cancellationToken);
        string apiLogs = await cli.LogsAsync(ComposeContracts.ApiServiceName, cancellationToken);

        return new ComposeRuntimeSnapshot(postgres, migrator, api, migratorLogs, apiLogs);
    }

    public async Task<string[]> ReadMigrationHistoryAsync(CancellationToken cancellationToken = default)
    {
        ComposeCommandResult result = await cli.RunAsync(
            [
                "exec",
                "-T",
                ComposeContracts.PostgresServiceName,
                "psql",
                "-U",
                "mbs",
                "-d",
                "minimal_bank",
                "-v",
                "ON_ERROR_STOP=1",
                "-t",
                "-A",
                "-c",
                $"""SELECT "MigrationId" FROM {BankPersistence.MigrationsHistorySchema}."{BankPersistence.MigrationsHistoryTableName}" ORDER BY "MigrationId";""",
            ],
            cancellationToken: cancellationToken);

        Assert.True(
            result.ExitCode == 0,
            $"migration history query failed ({result.ExitCode}):\n{result.Output}");

        return result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task AssertProjectResourcesAbsentAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<JsonElement> ps = ComposeObservations.ParsePsJsonLines(
            await cli.PsJsonAsync(cancellationToken));
        Assert.True(
            ps.Count == 0,
            $"Expected no project containers after clean reset, found {ps.Count}.");

        string volumeName = ComposeObservations.ResolveNamedVolumeName(ProjectName);
        ComposeCommandResult volumes = await DockerCli.VolumeLsAsync(cancellationToken);
        Assert.True(volumes.ExitCode == 0, volumes.Output);
        Assert.DoesNotContain(
            volumeName,
            volumes.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries),
            StringComparer.Ordinal);
    }

    public async ValueTask DisposeAsync()
    {
        if (cleaned)
        {
            return;
        }

        try
        {
            await cli.DownCleanResetAsync();
            await ForceRemoveProjectResiduesAsync(CancellationToken.None);
            cleaned = true;
        }
        catch
        {
            // Best-effort dispose; tests assert cleanup explicitly where required.
        }
    }
}

internal sealed record ComposeRuntimeSnapshot(
    ComposeServiceState Postgres,
    ComposeServiceState Migrator,
    ComposeServiceState Api,
    string MigratorLogs,
    string ApiLogs);
