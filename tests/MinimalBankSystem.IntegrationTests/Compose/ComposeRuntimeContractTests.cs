using System.Text.Json;
using MinimalBankSystem.IntegrationTests.Persistence;

namespace MinimalBankSystem.IntegrationTests.Compose;

/// <summary>
/// Runtime oracles for ordering, failure, lifecycle, secret non-disclosure, and
/// mutation-sensitive signatures (M-01..M-10 protected contracts).
/// Evidence comes from docker compose/inspect/psql, not source scans alone.
/// </summary>
[Trait("Category", "PostgreSqlIntegration")]
[Collection(TestExecutionCollections.ComposeRuntime)]
public sealed class ComposeRuntimeContractTests
{
    private static readonly TimeSpan StartBudget = TimeSpan.FromMinutes(6);
    private static readonly TimeSpan FailureBudget = TimeSpan.FromMinutes(4);

    [Fact]
    public async Task CleanStartAppliesMigrationThenStartsApiAfterMigratorSuccess()
    {
        await using ComposeProjectSession session = ComposeProjectSession.Create();

        await session.CleanResetAsync();
        await session.EnsureValidatedAsync();

        ComposeCommandResult up = await session.StartAsync();
        Assert.True(up.ExitCode == 0, $"clean start failed:\n{up.Output}");

        await session.WaitUntilMigratorTerminalAsync(StartBudget);
        await session.WaitUntilApiRunningAsync(StartBudget);

        ComposeRuntimeSnapshot snapshot = await session.CaptureSnapshotAsync();

        Assert.Equal(0, snapshot.Migrator.ExitCode);
        Assert.Contains(
            ComposeContracts.PathMarkerMigratorCompleted,
            snapshot.MigratorLogs,
            StringComparison.Ordinal);
        Assert.True(snapshot.Api.IsRunning, $"API must be running, state={snapshot.Api.State}");
        Assert.True(snapshot.Api.HasEverStarted, "API must have a real StartedAt (M-09).");
        Assert.False(snapshot.Api.IsExited, "Success path must not treat started-then-exited as success (M-09).");

        Assert.NotNull(snapshot.Migrator.FinishedAt);
        Assert.NotNull(snapshot.Api.StartedAt);
        Assert.True(
            snapshot.Api.StartedAt >= snapshot.Migrator.FinishedAt,
            $"API StartedAt {snapshot.Api.StartedAt:o} must not precede Migrator FinishedAt {snapshot.Migrator.FinishedAt:o} (M-01).");

        string[] history = await session.ReadMigrationHistoryAsync();
        string migrationId = Assert.Single(history);
        Assert.EndsWith(ComposeContracts.ExpectedMigrationIdSuffix, migrationId, StringComparison.Ordinal);

        // M-08 invalid signature: exit 0 without expected history must not pass.
        Assert.False(snapshot.Migrator.ExitCode == 0 && history.Length == 0);

        Assert.DoesNotContain(session.Password, snapshot.MigratorLogs, StringComparison.Ordinal);
        Assert.DoesNotContain(session.Password, snapshot.ApiLogs, StringComparison.Ordinal);

        if (snapshot.Migrator.Id is not null)
        {
            string args = await DockerCli.ContainerArgsAsync(snapshot.Migrator.Id);
            string cmd = await DockerCli.ContainerCmdAsync(snapshot.Migrator.Id);
            Assert.DoesNotContain(session.Password, args, StringComparison.Ordinal);
            Assert.DoesNotContain(session.Password, cmd, StringComparison.Ordinal);
        }

        if (snapshot.Api.Id is not null)
        {
            string args = await DockerCli.ContainerArgsAsync(snapshot.Api.Id);
            string cmd = await DockerCli.ContainerCmdAsync(snapshot.Api.Id);
            Assert.DoesNotContain(session.Password, args, StringComparison.Ordinal);
            Assert.DoesNotContain(session.Password, cmd, StringComparison.Ordinal);
        }

        string volumeName = ComposeObservations.ResolveNamedVolumeName(session.ProjectName);
        ComposeCommandResult volumeInspect = await DockerCli.VolumeInspectAsync(volumeName);
        Assert.True(volumeInspect.ExitCode == 0, volumeInspect.Output);
        using JsonDocument volumeJson = JsonDocument.Parse(volumeInspect.StandardOutput);
        JsonElement labels = volumeJson.RootElement[0].GetProperty("Labels");
        Assert.Equal(
            session.ProjectName,
            labels.GetProperty("com.docker.compose.project").GetString());
        Assert.Equal(
            ComposeContracts.NamedVolumeLogicalName,
            labels.GetProperty("com.docker.compose.volume").GetString());
    }

    [Fact]
    public async Task ExistingVolumeRerunKeepsMigrationHistoryAndRestartsApi()
    {
        await using ComposeProjectSession session = ComposeProjectSession.Create();

        await session.CleanResetAsync();
        ComposeCommandResult firstUp = await session.StartAsync();
        Assert.True(firstUp.ExitCode == 0, firstUp.Output);
        await session.WaitUntilApiRunningAsync(StartBudget);

        string[] historyAfterFirst = await session.ReadMigrationHistoryAsync();
        Assert.Single(historyAfterFirst);

        await session.StopRetainDataAsync();

        string volumeName = ComposeObservations.ResolveNamedVolumeName(session.ProjectName);
        ComposeCommandResult volumeInspect = await DockerCli.VolumeInspectAsync(volumeName);
        Assert.True(
            volumeInspect.ExitCode == 0,
            $"named volume must survive stop-with-data-retention:\n{volumeInspect.Output}");

        ComposeCommandResult secondUp = await session.StartAsync();
        Assert.True(secondUp.ExitCode == 0, secondUp.Output);
        await session.WaitUntilMigratorTerminalAsync(StartBudget);
        await session.WaitUntilApiRunningAsync(StartBudget);

        ComposeRuntimeSnapshot snapshot = await session.CaptureSnapshotAsync();
        Assert.Equal(0, snapshot.Migrator.ExitCode);
        Assert.True(snapshot.Api.IsRunning);

        string[] historyAfterSecond = await session.ReadMigrationHistoryAsync();
        Assert.Equal(historyAfterFirst, historyAfterSecond);
    }

    [Fact]
    public async Task MigratorFailurePreventsApiStartWithPathAndReasonMarkers()
    {
        await using ComposeProjectSession session = ComposeProjectSession.Create();
        await session.CleanResetAsync();

        string overridePath = WriteTemporaryOverride(
            """
            services:
              migrator:
                environment:
                  MBS_DB_HOST: 127.0.0.1
                  MBS_DB_PORT: "1"
            """);

        try
        {
            ComposeCommandResult up = await session.StartAsync(extraComposeFiles: [overridePath]);
            // Compose may still exit 0 while services settle; inspect terminal state.
            _ = up;

            await session.WaitUntilMigratorTerminalAsync(FailureBudget);
            // Give Compose a short window to attempt (and correctly refuse) API start.
            await Task.Delay(TimeSpan.FromSeconds(8));

            ComposeRuntimeSnapshot snapshot = await session.CaptureSnapshotAsync();

            Assert.NotNull(snapshot.Migrator.Id);
            Assert.True(
                snapshot.Migrator.ExitCode is > 0,
                $"Expected Migrator non-zero exit, got {snapshot.Migrator.ExitCode}. Logs:\n{snapshot.MigratorLogs}");

            Assert.Contains(
                ComposeContracts.PathMarkerMigratorFailed,
                snapshot.MigratorLogs,
                StringComparison.Ordinal);

            Assert.True(
                snapshot.Api.NeverStarted || snapshot.Api.State is "absent" or "created",
                $"API must never start after Migrator failure. state={snapshot.Api.State}, started={snapshot.Api.HasEverStarted}, startedAt={snapshot.Api.StartedAt:o}");
            Assert.False(
                snapshot.Api.HasEverStarted && snapshot.Api.IsExited,
                "started-then-exited must not be reported as never-started success (M-09 / failure path).");
            Assert.False(snapshot.Api.IsRunning, "API must not be running after Migrator failure (M-02).");

            Assert.DoesNotContain(session.Password, snapshot.MigratorLogs, StringComparison.Ordinal);
            Assert.DoesNotContain(session.Password, snapshot.ApiLogs, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(overridePath);
        }
    }

    [Fact]
    public async Task CleanResetRemovesProjectContainersAndNamedVolume()
    {
        await using ComposeProjectSession session = ComposeProjectSession.Create();
        await session.CleanResetAsync();

        ComposeCommandResult up = await session.StartAsync();
        Assert.True(up.ExitCode == 0, up.Output);
        await session.WaitUntilApiRunningAsync(StartBudget);

        string volumeName = ComposeObservations.ResolveNamedVolumeName(session.ProjectName);
        ComposeCommandResult before = await DockerCli.VolumeInspectAsync(volumeName);
        Assert.True(before.ExitCode == 0, "precondition: named volume must exist before clean reset (M-10).");

        await session.CleanResetAsync();
        await session.AssertProjectResourcesAbsentAsync();
    }

    [Fact]
    public async Task CanonicalLifecycleStopRestartAndResetUseD04Commands()
    {
        await using ComposeProjectSession session = ComposeProjectSession.Create(
            projectName: ComposeContracts.CanonicalProjectName);

        await session.CleanResetAsync();

        ComposeCommandResult validate = await session.Cli.ConfigQuietAsync();
        Assert.Equal(0, validate.ExitCode);

        ComposeCommandResult start = await session.StartAsync();
        Assert.Equal(0, start.ExitCode);
        await session.WaitUntilApiRunningAsync(StartBudget);

        await session.StopRetainDataAsync();
        ComposeRuntimeSnapshot afterStop = await session.CaptureSnapshotAsync();
        Assert.True(
            afterStop.Api.State is "absent" or "exited",
            $"after stop API should not be running: {afterStop.Api.State}");

        ComposeCommandResult restart = await session.StartAsync();
        Assert.Equal(0, restart.ExitCode);
        await session.WaitUntilMigratorTerminalAsync(StartBudget);
        await session.WaitUntilApiRunningAsync(StartBudget);

        ComposeRuntimeSnapshot afterRestart = await session.CaptureSnapshotAsync();
        Assert.Equal(0, afterRestart.Migrator.ExitCode);
        Assert.True(afterRestart.Api.IsRunning);

        await session.CleanResetAsync();
        await session.AssertProjectResourcesAbsentAsync();
    }

    private static string WriteTemporaryOverride(string contents)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"mbs-fnd05-override-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, contents);
        return path;
    }
}
